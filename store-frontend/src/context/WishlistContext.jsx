import React, { createContext, useContext, useEffect, useMemo, useState } from "react";
import wishlistService from "../services/wishlistService";

const WishlistContext = createContext(null);

function normalizeProductId(productId) {
  return String(productId ?? "").trim();
}

function normalizePrice(value) {
  const num = Number(value);
  return Number.isFinite(num) ? num : 0;
}

function toWishlistItem(product) {
  const productId = normalizeProductId(product?.id ?? product?.productId);
  if (!productId) return null;

  return {
    productId,
    title: product?.title || "Untitled Product",
    image: product?.image || product?.images?.[0] || "",
    price: normalizePrice(product?.price),
    mrp: normalizePrice(product?.mrp),
    category: product?.category || "",
    addedAt: product?.addedAt || new Date().toISOString(),
  };
}

function normalizeItems(items) {
  if (!Array.isArray(items)) return [];
  return items
    .map((item) => toWishlistItem(item))
    .filter(Boolean)
    .filter((item, index, list) => list.findIndex((x) => x.productId === item.productId) === index);
}

export function WishlistProvider({ children }) {
  const [items, setItems] = useState(() => normalizeItems(wishlistService.getItems()));

  useEffect(() => {
    wishlistService.saveItems(items);
  }, [items]);

  const addToWishlist = (product) => {
    const nextItem = toWishlistItem(product);
    if (!nextItem) return;

    setItems((prev) => {
      if (prev.some((item) => item.productId === nextItem.productId)) return prev;
      return [nextItem, ...prev];
    });
  };

  const removeFromWishlist = (productId) => {
    const normalizedProductId = normalizeProductId(productId);
    if (!normalizedProductId) return;
    setItems((prev) => prev.filter((item) => item.productId !== normalizedProductId));
  };

  const toggleWishlist = (product) => {
    const nextItem = toWishlistItem(product);
    if (!nextItem) return;

    setItems((prev) => {
      if (prev.some((item) => item.productId === nextItem.productId)) {
        return prev.filter((item) => item.productId !== nextItem.productId);
      }
      return [nextItem, ...prev];
    });
  };

  const clearWishlist = () => {
    setItems([]);
    wishlistService.clear();
  };

  const isInWishlist = (productId) => {
    const normalizedProductId = normalizeProductId(productId);
    if (!normalizedProductId) return false;
    return items.some((item) => item.productId === normalizedProductId);
  };

  const value = useMemo(
    () => ({
      items,
      wishlistCount: items.length,
      addToWishlist,
      removeFromWishlist,
      toggleWishlist,
      clearWishlist,
      isInWishlist,
    }),
    [items]
  );

  return <WishlistContext.Provider value={value}>{children}</WishlistContext.Provider>;
}

export function useWishlist() {
  const ctx = useContext(WishlistContext);
  if (!ctx) throw new Error("useWishlist must be used within <WishlistProvider>");
  return ctx;
}
