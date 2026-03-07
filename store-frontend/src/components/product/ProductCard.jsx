import React from "react";
import { Link } from "react-router-dom";
import { useWishlist } from "../../context/WishlistContext";
import { formatCurrency } from "../../utils/currency";

const ProductCard = ({ product }) => {
  const { isInWishlist, toggleWishlist } = useWishlist();
  const wished = isInWishlist(product.id);
  const totalStock = Array.isArray(product?.variants)
    ? product.variants.reduce((sum, variant) => sum + Math.max(0, Number(variant?.stock) || 0), 0)
    : Math.max(0, Number(product?.stock) || 0);
  const outOfStock = totalStock <= 0;

  return (
    <div className="relative border rounded overflow-hidden hover:shadow-lg">
      <button
        type="button"
        aria-label={wished ? "Remove from wishlist" : "Add to wishlist"}
        className={[
          "absolute right-2 top-2 z-10 rounded-full border px-2 py-1 text-sm",
          wished
            ? "border-rose-300 bg-rose-100 text-rose-600"
            : "border-slate-300 bg-white/90 text-slate-700 hover:bg-white",
        ].join(" ")}
        onClick={() => toggleWishlist(product)}
      >
        <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
          <path
            d="M12 21s-7-4.35-9.33-8.36C.7 9.1 2.19 6 5.6 6c1.85 0 3.16 1 3.9 2.02C10.24 7 11.55 6 13.4 6c3.41 0 4.9 3.1 2.93 6.64C19 16.65 12 21 12 21z"
            fill={wished ? "currentColor" : "none"}
            stroke="currentColor"
            strokeWidth="1.5"
          />
        </svg>
      </button>

      {outOfStock ? (
        <span className="absolute left-2 top-2 z-10 rounded-full border border-rose-300 bg-rose-100 px-2 py-1 text-xs font-semibold text-rose-700">
          Out of Stock
        </span>
      ) : null}

      <Link to={`/product/${product.id}`}>
        <img src={product.images[0]} alt={product.title} className="w-full h-48 object-cover" />
        <div className="p-2">
          <h3 className="text-sm font-medium">{product.title}</h3>
          <div className="flex items-center space-x-2">
            <span className="font-semibold">{formatCurrency(product.price)}</span>
            {product.mrp && product.mrp > product.price && (
              <span className="text-xs line-through text-gray-500">{formatCurrency(product.mrp)}</span>
            )}
          </div>
        </div>
      </Link>
    </div>
  );
};

export default ProductCard;
