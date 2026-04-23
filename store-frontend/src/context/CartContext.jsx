// FILE: src/context/CartContext.jsx
import React, { createContext, useContext, useEffect, useMemo, useState } from "react";
import cartService from "../services/cartService";
import couponsService from "../services/couponsService";
import { STORAGE_KEYS } from "../utils/constants";

const CartContext = createContext(null);

function normalizeProductId(productId) {
  return String(productId ?? "").trim();
}

function makeItemKey(productId, size, color) {
  return `${normalizeProductId(productId)}__${size || "NA"}__${color || "NA"}`;
}

function normalizeItem(item) {
  const productId = normalizeProductId(item?.productId ?? item?.id);
  if (!productId) return null;
  return {
    ...item,
    productId,
    key: makeItemKey(productId, item?.size, item?.color),
    qty: Math.max(1, Number(item?.qty) || 1),
  };
}

function normalizeItems(items) {
  if (!Array.isArray(items)) return [];
  return items.map(normalizeItem).filter(Boolean);
}

function getSessionUserId() {
  try {
    const raw = localStorage.getItem(STORAGE_KEYS.AUTH_SESSION);
    if (!raw) return 0;
    const parsed = JSON.parse(raw);
    const id = Number(parsed?.id ?? parsed?.Id ?? 0);
    return Number.isFinite(id) && id > 0 ? id : 0;
  } catch {
    return 0;
  }
}

export function CartProvider({ children }) {
  const [items, setItems] = useState(() => normalizeItems(cartService.getCartItems()));
  const [coupon, setCoupon] = useState(() => cartService.getCoupon());
  const [discount, setDiscount] = useState(() => cartService.getDiscount()); // number
  const [couponError, setCouponError] = useState("");

  // persist
  useEffect(() => {
    cartService.saveCartItems(items);
  }, [items]);

  useEffect(() => {
    cartService.saveCoupon(coupon);
    cartService.saveDiscount(discount);
  }, [coupon, discount]);

  const addToCart = (product, selectedSize, selectedColor, qty = 1) => {
    const productId = normalizeProductId(product?.id ?? product?.productId);
    if (!productId) return;
    const key = makeItemKey(productId, selectedSize, selectedColor);

    setItems((prev) => {
      const existing = prev.find((x) => x.key === key);
      const safeQty = Math.max(1, Number(qty) || 1);

      if (existing) {
        return prev.map((x) =>
          x.key === key
            ? { ...x, qty: Math.max(1, x.qty + safeQty) }
            : x
        );
      }

      return [
        ...prev,
        {
          key,
          productId,
          title: product.title,
          image: product.images?.[0] || product.image || "",
          price: product.price,
          mrp: product.mrp,
          category: product.category,
          size: selectedSize,
          color: selectedColor,
          qty: safeQty,
        },
      ];
    });
  };

  const addItem = (item) => {
    if (!item) return;
    addToCart(
      {
        id: item.productId ?? item.id,
        title: item.title,
        images: item.image ? [item.image] : [],
        image: item.image,
        price: item.price,
        mrp: item.mrp,
        category: item.category,
      },
      item.size,
      item.color,
      item.qty
    );
  };

  const removeFromCart = (keyOrProductId, size, color) => {
    const key =
      typeof size !== "undefined" || typeof color !== "undefined"
        ? makeItemKey(keyOrProductId, size, color)
        : keyOrProductId;
    setItems((prev) => prev.filter((x) => x.key !== key));
  };

  const removeItem = (productId, size, color) => {
    removeFromCart(productId, size, color);
  };

  const updateQty = (...args) => {
    const [keyOrProductId, sizeOrQty, color, nextQtyLegacy] = args;
    const key =
      args.length >= 4
        ? makeItemKey(keyOrProductId, sizeOrQty, color)
        : keyOrProductId;
    const nextQty = args.length >= 4 ? nextQtyLegacy : sizeOrQty;
    const q = Math.max(1, Number(nextQty) || 1);
    setItems((prev) => prev.map((x) => (x.key === key ? { ...x, qty: q } : x)));
  };

  const incQty = (key) => {
    setItems((prev) => prev.map((x) => (x.key === key ? { ...x, qty: x.qty + 1 } : x)));
  };

  const decQty = (key) => {
    setItems((prev) =>
      prev.map((x) =>
        x.key === key ? { ...x, qty: Math.max(1, x.qty - 1) } : x
      )
    );
  };

  const clearCart = () => {
    setItems([]);
    setCoupon("");
    setDiscount(0);
    setCouponError("");
    cartService.clearAll();
  };

  const subtotal = useMemo(() => {
    return items.reduce((sum, x) => sum + x.price * x.qty, 0);
  }, [items]);

  const totalItems = useMemo(() => {
    return items.reduce((sum, x) => sum + (Number(x.qty) || 0), 0);
  }, [items]);

  const total = useMemo(() => {
    const t = subtotal - discount;
    return t < 0 ? 0 : t;
  }, [subtotal, discount]);

  const applyCoupon = async (code) => {
    const trimmed = (code || "").trim().toUpperCase();
    setCouponError("");

    if (!trimmed) {
      setCoupon("");
      setDiscount(0);
      return { ok: false, message: "Enter a coupon code." };
    }

    const res = await couponsService.applyCoupon(trimmed, {
      subtotal,
      itemCount: totalItems,
      currentCoupon: coupon,
      userId: getSessionUserId(),
    });
    const isSuccess = Boolean(res?.ok ?? res?.valid);
    const discountAmount = Number.isFinite(Number(res?.discountAmount))
      ? Number(res.discountAmount)
      : Number.isFinite(Number(res?.discount))
      ? subtotal * Number(res.discount)
      : 0;

    if (!isSuccess) {
      setCoupon("");
      setDiscount(0);
      setCouponError(res?.message || "Invalid coupon.");
      return { ok: false, message: res?.message || "Invalid coupon.", discountAmount: 0 };
    }

    setCoupon(trimmed);
    setDiscount(discountAmount);
    setCouponError("");
    return { ok: true, message: res?.message || "Coupon applied.", discountAmount };
  };

  const removeCoupon = () => {
    setCoupon("");
    setDiscount(0);
    setCouponError("");
  };

  const value = useMemo(
    () => ({
      items,
      coupon,
      discount,
      couponError,
      subtotal,
      totalItems,
      total,
      addToCart,
      addItem,
      removeFromCart,
      removeItem,
      updateQty,
      incQty,
      decQty,
      clearCart,
      applyCoupon,
      removeCoupon,
    }),
    [items, coupon, discount, couponError, subtotal, totalItems, total]
  );

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart() {
  const ctx = useContext(CartContext);
  if (!ctx) throw new Error("useCart must be used within <CartProvider>");
  return ctx;
}
