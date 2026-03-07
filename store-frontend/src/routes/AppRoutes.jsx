// FILE: src/routes/AppRoutes.jsx
import { Routes, Route } from "react-router-dom";

// Pages
import Home from "../pages/Home";
import Shop from "../pages/Shop";
import Offers from "../pages/Offers";
import ProductDetails from "../pages/ProductDetails";
import Cart from "../pages/Cart";
import Checkout from "../pages/Checkout";
import TrackOrder from "../pages/TrackOrder";
import About from "../pages/About";
import Contact from "../pages/Contact";
import Profile from "../pages/Profile";
import Orders from "../pages/Orders";
import Wishlist from "../pages/Wishlist";
import SavedAddress from "../pages/SavedAddress";
import Support from "../pages/Support";

// Policies
import ReturnRefund from "../pages/policies/ReturnRefund";
import Shipping from "../pages/policies/Shipping";
import Privacy from "../pages/policies/Privacy";
import Terms from "../pages/policies/Terms";

// Optional Auth
import Login from "../pages/Login";
import Register from "../pages/Register";

const AppRoutes = () => {
  return (
    <Routes>
      {/* Main Routes */}
      <Route path="/" element={<Home />} />
      <Route path="/shop" element={<Shop />} />
      <Route path="/offers" element={<Offers />} />
      <Route path="/product/:id" element={<ProductDetails />} />
      <Route path="/cart" element={<Cart />} />
      <Route path="/checkout" element={<Checkout />} />
      <Route path="/track-order" element={<TrackOrder />} />
      <Route path="/about" element={<About />} />
      <Route path="/contact" element={<Contact />} />
      <Route path="/profile" element={<Profile />} />
      <Route path="/orders" element={<Orders />} />
      <Route path="/wishlist" element={<Wishlist />} />
      <Route path="/address" element={<SavedAddress />} />
      <Route path="/saved-address" element={<SavedAddress />} />
      <Route path="/support" element={<Support />} />
      <Route path="/help-support" element={<Support />} />

      {/* Policies */}
      <Route path="/policies/return-refund" element={<ReturnRefund />} />
      <Route path="/policies/shipping" element={<Shipping />} />
      <Route path="/policies/privacy" element={<Privacy />} />
      <Route path="/policies/terms" element={<Terms />} />

      {/* Optional Auth */}
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />

      {/* Fallback */}
      <Route path="*" element={<div className="p-10 text-center">Page Not Found</div>} />
    </Routes>
  );
};

export default AppRoutes;
