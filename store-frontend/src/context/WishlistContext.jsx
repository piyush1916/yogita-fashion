import React, { createContext, useContext, useEffect, useMemo, useState } from "react";
import { useAuth } from "./AuthContext";
import productsService from "../services/productsService";
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
  const productId = normalizeProductId(product?.productId ?? product?.id);
  if (!productId) return null;

  const wishlistItemId = normalizeProductId(
    product?.wishlistItemId ?? product?.wishlistId ?? (product?.productId != null ? product?.id : "")
  );

  return {
    wishlistItemId,
    productId,
    title: product?.title || product?.name || "Untitled Product",
    image: product?.image || product?.images?.[0] || product?.imageUrl || "",
    price: normalizePrice(product?.price),
    mrp: normalizePrice(product?.mrp ?? product?.originalPrice),
    category: product?.category || "",
    addedAt: product?.addedAt || product?.createdAt || new Date().toISOString(),
  };
}

function dedupeItems(items) {
  return items.filter(
    (item, index, list) =>
      Boolean(item?.productId) && list.findIndex((entry) => entry.productId === item.productId) === index
  );
}

async function hydrateItems(rawItems) {
  const normalized = dedupeItems(rawItems.map((item) => toWishlistItem(item)).filter(Boolean));
  const idsNeedingData = normalized
    .filter((item) => !item.title || item.title === "Untitled Product" || !item.image)
    .map((item) => item.productId);

  if (idsNeedingData.length === 0) return normalized;

  const hydrationResults = await Promise.all(
    idsNeedingData.map(async (productId) => {
      try {
        const product = await productsService.getById(productId);
        const hydrated = toWishlistItem(product);
        return hydrated ? { productId, hydrated } : null;
      } catch {
        return null;
      }
    })
  );

  const hydrationMap = hydrationResults
    .filter(Boolean)
    .reduce((acc, entry) => {
      acc[entry.productId] = entry.hydrated;
      return acc;
    }, {});

  return normalized.map((item) => hydrationMap[item.productId] || item);
}

export function WishlistProvider({ children }) {
  const { user } = useAuth();
  const [items, setItems] = useState([]);

  useEffect(() => {
    let ignore = false;

    const loadWishlist = async () => {
      try {
        const rawItems = await wishlistService.getItems();
        const hydrated = await hydrateItems(rawItems);
        if (!ignore) {
          setItems(hydrated);
        }
      } catch {
        if (!ignore) {
          setItems([]);
        }
      }
    };

    loadWishlist();
    return () => {
      ignore = true;
    };
  }, [user]);

  useEffect(() => {
    if (user) return;
    wishlistService.saveLocalItems(items);
  }, [items, user]);

  const addToWishlist = async (product) => {
    const nextItem = toWishlistItem(product);
    if (!nextItem) return;

    if (items.some((item) => item.productId === nextItem.productId)) {
      return;
    }

    try {
      const savedItem = await wishlistService.addItem(nextItem.productId);
      const savedProductId = normalizeProductId(savedItem?.productId ?? nextItem.productId);
      setItems((prev) => [
        {
          ...nextItem,
          wishlistItemId: savedItem?.id || nextItem.wishlistItemId,
          productId: savedProductId,
        },
        ...prev.filter((item) => item.productId !== savedProductId),
      ]);
    } catch {
      // Keep current state if wishlist sync fails.
    }
  };

  const removeFromWishlist = async (productId) => {
    const normalizedProductId = normalizeProductId(productId);
    if (!normalizedProductId) return;

    try {
      await wishlistService.removeItem(normalizedProductId);
      setItems((prev) => prev.filter((item) => item.productId !== normalizedProductId));
    } catch {
      // Keep current state if wishlist sync fails.
    }
  };

  const toggleWishlist = async (product) => {
    const nextItem = toWishlistItem(product);
    if (!nextItem) return;
    const exists = items.some((item) => item.productId === nextItem.productId);
    if (exists) {
      await removeFromWishlist(nextItem.productId);
      return;
    }
    await addToWishlist(nextItem);
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
