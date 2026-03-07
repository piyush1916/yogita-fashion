import React, { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useCart } from "../context/CartContext";
import { useToast } from "../hooks/useToast";
import couponsService from "../services/couponsService";
import { formatCurrency } from "../utils/currency";

const Cart = () => {
  const { user } = useAuth();
  const { items, updateQty, removeFromCart, subtotal } = useCart();
  const [coupon, setCoupon] = useState("");
  const [discount, setDiscount] = useState(0);
  const [applying, setApplying] = useState(false);
  const toast = useToast();
  const navigate = useNavigate();

  const apply = async () => {
    const itemCount = items.reduce((sum, item) => sum + (Number(item?.qty) || 0), 0);
    setApplying(true);
    const res = await couponsService.applyCoupon(coupon, {
      subtotal,
      itemCount,
      userId: Number(user?.id) || 0,
    });
    setApplying(false);

    if (res.valid) {
      setDiscount(res.discount);
      toast.success(res.message);
    } else {
      toast.error(res.message);
    }
  };

  const total = subtotal * (1 - discount);

  return (
    <div className="container mx-auto px-4 py-10">
      <h1 className="text-2xl font-bold mb-4">Your Cart</h1>
      {items.length === 0 ? (
        <p>
          Cart is empty.{" "}
          <Link to="/shop" className="text-indigo-600">
            Shop now
          </Link>
        </p>
      ) : (
        <>
          <div className="space-y-4">
            {items.map((i) => (
              <div key={i.key} className="flex items-center space-x-4 border p-4 rounded">
                <img src={i.image} alt={i.title} className="w-24 h-24 object-cover" />
                <div className="flex-1">
                  <h3 className="font-semibold">{i.title}</h3>
                  <p>
                    Size: {i.size} | Color: {i.color}
                  </p>
                  <p>{formatCurrency(i.price)}</p>
                  <div className="flex items-center space-x-2 mt-2">
                    <button onClick={() => updateQty(i.key, i.qty - 1)}>-</button>
                    <span>{i.qty}</span>
                    <button onClick={() => updateQty(i.key, i.qty + 1)}>+</button>
                  </div>
                </div>
                <button onClick={() => removeFromCart(i.key)} className="text-red-600">
                  Remove
                </button>
              </div>
            ))}
          </div>
          <div className="mt-6">
            <h3 className="font-semibold">Subtotal: {formatCurrency(subtotal)}</h3>
            <div className="mt-2 flex space-x-2 items-center">
              <input
                type="text"
                placeholder="Coupon code"
                value={coupon}
                onChange={(e) => setCoupon(e.target.value)}
                className="border px-2 py-1 rounded"
              />
              <button onClick={apply} disabled={applying} className="bg-indigo-600 text-white px-4 py-1 rounded">
                {applying ? "Applying..." : "Apply"}
              </button>
            </div>
            {discount > 0 && <p className="text-green-600">Discount applied: {Math.round(discount * 100)}%</p>}
            <p className="mt-1 font-semibold">Total after discount: {formatCurrency(total)}</p>
            <button onClick={() => navigate("/checkout")} className="mt-4 bg-indigo-600 text-white px-4 py-2 rounded">
              Continue to Checkout
            </button>
          </div>
        </>
      )}
    </div>
  );
};

export default Cart;
