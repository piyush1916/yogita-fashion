import React, { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import productsService from "../services/productsService";
import Loader from "../components/common/Loader";
import ProductGallery from "../components/product/ProductGallery";
import VariantSelector from "../components/product/VariantSelector";
import { useCart } from "../context/CartContext";
import { useWishlist } from "../context/WishlistContext";
import { useToast } from "../hooks/useToast";
import { formatCurrency } from "../utils/currency";
import { useAuth } from "../context/AuthContext";

function normalizePhone(value) {
  return String(value || "").replace(/\D+/g, "");
}

function isValidEmail(value) {
  if (!value) return false;
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(value).trim());
}

const ProductDetails = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const [product, setProduct] = useState(null);
  const [variant, setVariant] = useState({ size: "", color: "" });
  const [stock, setStock] = useState(0);
  const [notifyForm, setNotifyForm] = useState({ email: "", whatsAppNumber: "" });
  const [notifySubmitting, setNotifySubmitting] = useState(false);
  const { addToCart } = useCart();
  const { isInWishlist, toggleWishlist } = useWishlist();
  const { user } = useAuth();
  const toast = useToast();

  useEffect(() => {
    const load = async () => {
      const p = await productsService.getById(id);
      setProduct(p);
    };
    load();
  }, [id]);

  useEffect(() => {
    if (!product) return;
    const firstAvailableVariant =
      product.variants.find((item) => Number(item?.stock) > 0) || product.variants[0] || null;

    if (firstAvailableVariant) {
      setVariant({ size: firstAvailableVariant.size, color: firstAvailableVariant.color });
    }
  }, [product]);

  useEffect(() => {
    if (product && variant.size && variant.color) {
      const selected = product.variants.find((item) => item.size === variant.size && item.color === variant.color);
      setStock(selected ? Math.max(0, Number(selected.stock) || 0) : 0);
    }
  }, [product, variant]);

  useEffect(() => {
    const profileEmail = String(user?.email || "")
      .trim()
      .toLowerCase();
    if (!profileEmail) return;
    setNotifyForm((prev) => ({ ...prev, email: profileEmail }));
  }, [user?.email]);

  const allOutOfStock = useMemo(() => {
    if (!product) return false;
    return !product.variants.some((item) => Number(item?.stock) > 0);
  }, [product]);

  const handleAdd = () => {
    if (!variant.size || !variant.color) {
      toast.error("Select size and color");
      return;
    }
    if (stock <= 0) {
      toast.error("Selected variant is out of stock.");
      return;
    }
    addToCart(product, variant.size, variant.color, 1);
    toast.success("Added to cart");
    navigate("/cart");
  };

  const handleWishlist = () => {
    if (!product) return;
    const wasWishlisted = isInWishlist(product.id);
    toggleWishlist(product);
    toast.info(wasWishlisted ? "Removed from wishlist." : "Added to wishlist.");
  };

  const handleNotifyChange = (event) => {
    const { name, value } = event.target;
    setNotifyForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleNotifySubmit = async () => {
    if (!product) return;

    const email = String(notifyForm.email || "")
      .trim()
      .toLowerCase();
    const whatsAppNumber = normalizePhone(notifyForm.whatsAppNumber);

    if (!email && !whatsAppNumber) {
      toast.error("Enter profile email or WhatsApp number.");
      return;
    }
    if (email && !isValidEmail(email)) {
      toast.error("Please enter a valid email address.");
      return;
    }
    if (whatsAppNumber && whatsAppNumber.length < 10) {
      toast.error("Please enter a valid WhatsApp number.");
      return;
    }

    setNotifySubmitting(true);
    try {
      await productsService.requestStockAlert(product.id, { email, whatsAppNumber });
      toast.success("Stock alert request saved. We will notify you on availability.");
    } catch (error) {
      const message =
        typeof error?.response?.data?.message === "string"
          ? error.response.data.message
          : typeof error?.response?.data === "string"
          ? error.response.data
          : error?.message || "Failed to save stock alert request.";
      toast.error(message);
    } finally {
      setNotifySubmitting(false);
    }
  };

  if (!product) return <Loader />;

  const wished = isInWishlist(product.id);
  const showOutOfStock = allOutOfStock || stock <= 0;

  return (
    <div className="container mx-auto px-4 py-10">
      <div className="flex flex-col lg:flex-row gap-10">
        <div className="min-w-0 lg:w-1/2">
          <ProductGallery images={product.images} />
        </div>
        <div className="min-w-0 space-y-4 lg:w-1/2">
          <h1 className="text-2xl font-bold">{product.title}</h1>
          <div className="flex items-center space-x-2">
            <span className="text-xl font-semibold">{formatCurrency(product.price)}</span>
            {product.mrp && product.mrp > product.price && (
              <span className="text-sm line-through text-gray-500">{formatCurrency(product.mrp)}</span>
            )}
          </div>
          <p>{product.shortDescription}</p>
          <VariantSelector
            size={variant.size}
            color={variant.color}
            sizes={product.sizes}
            colors={product.colors}
            onChange={setVariant}
          />

          {showOutOfStock ? (
            <p className="inline-flex rounded-full border border-rose-300 bg-rose-100 px-3 py-1 text-sm font-semibold text-rose-700">
              Out of Stock
            </p>
          ) : (
            <p className="inline-flex rounded-full border border-emerald-300 bg-emerald-100 px-3 py-1 text-sm font-semibold text-emerald-700">
              In Stock: {stock}
            </p>
          )}

          <div className="mt-4 flex flex-wrap gap-3">
            <button
              onClick={handleAdd}
              disabled={!variant.size || !variant.color || stock <= 0}
              className={[
                "w-full rounded px-4 py-2 text-white sm:w-auto",
                !variant.size || !variant.color || stock <= 0
                  ? "bg-slate-400 cursor-not-allowed"
                  : "bg-indigo-600",
              ].join(" ")}
            >
              {stock <= 0 ? "Out of Stock" : "Add to Cart"}
            </button>
            <button
              onClick={handleWishlist}
              className={[
                "w-full rounded border px-4 py-2 sm:w-auto",
                wished
                  ? "border-rose-300 bg-rose-100 text-rose-700"
                  : "border-slate-300 bg-white text-slate-700",
              ].join(" ")}
            >
              {wished ? "Remove from Wishlist" : "Add to Wishlist"}
            </button>
          </div>

          {showOutOfStock ? (
            <div className="rounded-xl border border-amber-300 bg-amber-50 p-4 space-y-3">
              <h3 className="font-semibold text-amber-900">Get Notified When Available</h3>
              <p className="text-sm text-amber-800">
                Product available hote hi hum aapko mail aur WhatsApp par alert bhej denge.
              </p>
              <div className="grid gap-3 md:grid-cols-2">
                <div>
                  <label className="block text-sm font-medium text-amber-900">Email (from profile)</label>
                  <input
                    type="email"
                    name="email"
                    value={notifyForm.email}
                    onChange={handleNotifyChange}
                    disabled={Boolean(user?.email)}
                    placeholder="you@example.com"
                    className="mt-1 w-full rounded border border-amber-300 bg-white px-3 py-2 text-sm"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-amber-900">WhatsApp Number</label>
                  <input
                    type="tel"
                    name="whatsAppNumber"
                    value={notifyForm.whatsAppNumber}
                    onChange={handleNotifyChange}
                    placeholder="10 digit number"
                    className="mt-1 w-full rounded border border-amber-300 bg-white px-3 py-2 text-sm"
                  />
                </div>
              </div>
              {user?.email ? <p className="text-xs text-amber-700">Email auto-picked from your profile.</p> : null}
              <button
                type="button"
                onClick={handleNotifySubmit}
                disabled={notifySubmitting}
                className={[
                  "rounded px-4 py-2 text-sm font-semibold text-white",
                  notifySubmitting ? "bg-amber-400 cursor-not-allowed" : "bg-amber-600 hover:bg-amber-700",
                ].join(" ")}
              >
                {notifySubmitting ? "Saving..." : "Notify Me"}
              </button>
            </div>
          ) : null}

          <div className="mt-6">
            <h3 className="font-semibold">Product Details</h3>
            <ul className="list-disc list-inside space-y-1">
              {product.details?.material && <li>Material: {product.details.material}</li>}
              {product.details?.fit && <li>Fit: {product.details.fit}</li>}
              {product.details?.style && <li>Style: {product.details.style}</li>}
              {product.details?.sleeve && <li>Sleeve: {product.details.sleeve}</li>}
              {product.details?.washCare && <li>Wash care: {product.details.washCare}</li>}
            </ul>
          </div>
          <div className="mt-6">
            <h3 className="font-semibold">Size Chart</h3>
            <p>Tops: S 34/24, M 36/25, L 38/26, XL 40/27</p>
            <p>Kurtis: S 34/44, M 36/44, L 38/45, XL 40/45</p>
            <p>Jeans waist: 28/30/32/34/36</p>
          </div>
          <div className="mt-6">
            <h3 className="font-semibold">Return info</h3>
            <p>7 days return policy.</p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default ProductDetails;
