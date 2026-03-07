import React, { useState } from "react";
import { Link } from "react-router-dom";
import { useCart } from "../context/CartContext";
import { useWishlist } from "../context/WishlistContext";
import { useToast } from "../hooks/useToast";
import productsService from "../services/productsService";
import { formatCurrency } from "../utils/currency";

function getPreferredVariant(product) {
  if (Array.isArray(product?.variants) && product.variants.length > 0) {
    return product.variants.find((variant) => Number(variant?.stock) > 0) || product.variants[0];
  }

  if (Array.isArray(product?.sizes) && product.sizes.length && Array.isArray(product?.colors) && product.colors.length) {
    return { size: product.sizes[0], color: product.colors[0], stock: 1 };
  }

  return null;
}

export default function Wishlist() {
  const { items, removeFromWishlist, clearWishlist } = useWishlist();
  const { addToCart } = useCart();
  const toast = useToast();
  const [movingProductId, setMovingProductId] = useState("");

  const onMoveToCart = async (item) => {
    if (!item?.productId) return;
    setMovingProductId(item.productId);

    try {
      const product = await productsService.getById(item.productId);
      if (!product) {
        toast.error("This product is no longer available.");
        return;
      }

      const selectedVariant = getPreferredVariant(product);
      if (!selectedVariant || !selectedVariant.size || !selectedVariant.color) {
        toast.error("Could not find an available size/color for this product.");
        return;
      }

      if (Number(selectedVariant.stock) <= 0) {
        toast.error("This product is currently out of stock.");
        return;
      }

      addToCart(product, selectedVariant.size, selectedVariant.color, 1);
      removeFromWishlist(item.productId);
      toast.success("Moved to cart.");
    } catch {
      toast.error("Could not move this item to cart.");
    } finally {
      setMovingProductId("");
    }
  };

  if (items.length === 0) {
    return (
      <section className="ordersPage">
        <div className="container ordersWrap">
          <div className="profileCard profileEmpty">
            <h1 className="ordersTitle">Your Wishlist</h1>
            <p className="profileSubtext">Save your favorites here and revisit them anytime.</p>
            <div className="profileActions">
              <Link to="/shop" className="profileBtn profileBtnPrimary">
                Explore Products
              </Link>
            </div>
          </div>
        </div>
      </section>
    );
  }

  return (
    <section className="ordersPage">
      <div className="container ordersWrap">
        <p className="ordersBreadcrumb">
          <Link to="/profile">Your Account</Link>
          <span aria-hidden="true">{">"}</span>
          <span>Your Wishlist</span>
        </p>

        <div className="ordersHeader">
          <h1 className="ordersTitle">Your Wishlist</h1>
          <div className="profileActions">
            <button type="button" className="profileBtn profileBtnGhost" onClick={clearWishlist}>
              Clear Wishlist
            </button>
          </div>
        </div>

        <div className="ordersList">
          {items.map((item) => (
            <article key={item.productId} className="ordersCard">
              <div className="ordersCardBody">
                <div className="ordersCardImageWrap">
                  {item.image ? (
                    <img src={item.image} alt={item.title} className="ordersCardImage" />
                  ) : (
                    <div className="ordersCardFallback" aria-hidden="true" />
                  )}
                </div>

                <div className="ordersCardContent">
                  <h2>{item.title}</h2>
                  <p>{item.category || "Fashion"}</p>

                  <div className="ordersPriceRow">
                    <strong>{formatCurrency(item.price)}</strong>
                    {Number(item.mrp) > Number(item.price) && <span>{formatCurrency(item.mrp)}</span>}
                  </div>

                  <div className="profileActions">
                    <Link to={`/product/${item.productId}`} className="profileBtn profileBtnGhost">
                      View Product
                    </Link>
                    <button
                      type="button"
                      className="profileBtn profileBtnPrimary"
                      onClick={() => onMoveToCart(item)}
                      disabled={movingProductId === item.productId}
                    >
                      {movingProductId === item.productId ? "Moving..." : "Move to Cart"}
                    </button>
                    <button
                      type="button"
                      className="profileBtn profileBtnDanger"
                      onClick={() => removeFromWishlist(item.productId)}
                    >
                      Remove
                    </button>
                  </div>
                </div>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
