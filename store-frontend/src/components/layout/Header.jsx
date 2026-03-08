import React, { useEffect, useRef, useState } from "react";
import { Link, NavLink, useNavigate } from "react-router-dom";
import { useCart } from "../../context/CartContext";
import { useWishlist } from "../../context/WishlistContext";
import { useAuth } from "../../context/AuthContext";
import logo from "../../assets/logo-transparent.png";

const Header = () => {
  const { totalItems } = useCart();
  const { wishlistCount } = useWishlist();
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const [menuOpen, setMenuOpen] = useState(false); // ≡ left dropdown
  const [userOpen, setUserOpen] = useState(false); // user dropdown
  const [q, setQ] = useState("");

  const menuRef = useRef(null);
  const userRef = useRef(null);

  // close dropdowns on outside click
  useEffect(() => {
    const onDown = (e) => {
      if (menuRef.current && !menuRef.current.contains(e.target)) setMenuOpen(false);
      if (userRef.current && !userRef.current.contains(e.target)) setUserOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, []);

  // close on ESC
  useEffect(() => {
    const onKey = (e) => {
      if (e.key === "Escape") {
        setMenuOpen(false);
        setUserOpen(false);
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, []);

  const onSearch = (e) => {
    e.preventDefault();
    const query = q.trim();
    // Shop page pe search query bhej do (simple)
    navigate(query ? `/shop?q=${encodeURIComponent(query)}` : "/shop");
  };

  const onAuthClick = () => {
    if (user) {
      logout();
    }
    setUserOpen(false);
  };

  return (
    <header className="yfHeader">
      <div className="yfHeader__inner">
        {/* LEFT: ≡ + Home + Shop */}
        <div className="yfHeader__left">
          <div className="yfDrop" ref={menuRef}>
            <button
              type="button"
              className="yfIconBtn"
              aria-label="Open menu"
              aria-expanded={menuOpen}
              onClick={() => setMenuOpen((v) => !v)}
            >
              {/* hamburger */}
              <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
                <path
                  d="M4 7h16a1 1 0 000-2H4a1 1 0 000 2zm16 4H4a1 1 0 000 2h16a1 1 0 000-2zm0 6H4a1 1 0 000 2h16a1 1 0 000-2z"
                  fill="currentColor"
                />
              </svg>
            </button>

            <div className={`yfDrop__menu ${menuOpen ? "isOpen" : ""}`}>
              <Link className="yfDrop__item" to="/offers" onClick={() => setMenuOpen(false)}>
                Offers
              </Link>
              <Link className="yfDrop__item" to="/contact" onClick={() => setMenuOpen(false)}>
                Contact
              </Link>
              <Link className="yfDrop__item" to="/policies/terms" onClick={() => setMenuOpen(false)}>
                Terms
              </Link>
              <Link className="yfDrop__item" to="/policies/privacy" onClick={() => setMenuOpen(false)}>
                Privacy
              </Link>
            </div>
          </div>

          <div className="navLinks">
            <NavLink to="/" className={({ isActive }) => `yfNavLink ${isActive ? "isActive" : ""}`}>
              Home
            </NavLink>
            <NavLink to="/shop" className={({ isActive }) => `yfNavLink ${isActive ? "isActive" : ""}`}>
              Shop
            </NavLink>
          </div>
        </div>

        {/* CENTER: Logo */}
        <div className="yfHeader__center">
          <Link to="/" className="yfLogo" aria-label="Go to home">
            {/* Put logo in: public/logo.png  (safe; build break nahi hoga) */}
            <img className="yfLogo__img" src={logo} alt="Yogita Fashion" />
          </Link>
        </div>

        {/* RIGHT: Search + icons */}
        <div className="yfHeader__right">
          <form className="yfSearch" onSubmit={onSearch}>
            <input
              className="yfSearch__input"
              placeholder="Search..."
              value={q}
              onChange={(e) => setQ(e.target.value)}
            />
            <button className="yfSearch__btn" type="submit" aria-label="Search">
              <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
                <path
                  d="M10.5 3a7.5 7.5 0 105.02 13.08l3.2 3.2a1 1 0 001.41-1.42l-3.2-3.2A7.5 7.5 0 0010.5 3zm0 2a5.5 5.5 0 110 11 5.5 5.5 0 010-11z"
                  fill="currentColor"
                />
              </svg>
            </button>
          </form>

          {/* USER dropdown */}
          <div className="yfDrop" ref={userRef}>
            <button
              type="button"
              className="yfIconBtn"
              aria-label="Account"
              aria-expanded={userOpen}
              onClick={() => setUserOpen((v) => !v)}
            >
              <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
                <path
                  d="M12 12a4.5 4.5 0 10-4.5-4.5A4.5 4.5 0 0012 12zm0 2c-4.42 0-8 2.24-8 5a1 1 0 001 1h14a1 1 0 001-1c0-2.76-3.58-5-8-5z"
                  fill="currentColor"
                />
              </svg>
            </button>

            <div className={`yfDrop__menu yfDrop__menu--right ${userOpen ? "isOpen" : ""}`}>
              <Link className="yfDrop__item" to="/profile" onClick={() => setUserOpen(false)}>
                My Profile
              </Link>
              <Link className="yfDrop__item" to="/orders" onClick={() => setUserOpen(false)}>
                My Orders
              </Link>
              <Link className="yfDrop__item" to="/wishlist" onClick={() => setUserOpen(false)}>
                Wishlist
              </Link>
              <Link className="yfDrop__item" to="/address" onClick={() => setUserOpen(false)}>
                Saved Address
              </Link>
              <Link className="yfDrop__item" to="/support" onClick={() => setUserOpen(false)}>
                Help / Support
              </Link>

              <div className="yfDrop__sep" />

              <Link className="yfDrop__item" to={user ? "/" : "/login"} onClick={onAuthClick}>
                {user ? "Logout" : "Login"}
              </Link>
            </div>
          </div>

          <Link className="yfIconBtn yfCartBtn" to="/wishlist" aria-label="Wishlist">
            <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
              <path
                d="M12 21s-7-4.35-9.33-8.36C.7 9.1 2.19 6 5.6 6c1.85 0 3.16 1 3.9 2.02C10.24 7 11.55 6 13.4 6c3.41 0 4.9 3.1 2.93 6.64C19 16.65 12 21 12 21z"
                fill="currentColor"
              />
            </svg>

            {wishlistCount > 0 && <span className="yfCartCount">{wishlistCount}</span>}
          </Link>

          <Link to="/cart" className="yfIconBtn yfCartBtn" aria-label="Cart">
            <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
              <path
                d="M7 18a2 2 0 104 0H7zm8 0a2 2 0 104 0h-4zM6.2 6h15.2a1 1 0 01.98 1.2l-1.2 6A1 1 0 0120.2 14H8a1 1 0 01-.98-.8L5.3 4H3a1 1 0 010-2h3a1 1 0 01.98.8L6.2 6z"
                fill="currentColor"
              />
            </svg>

            {totalItems > 0 && <span className="yfCartCount">{totalItems}</span>}
          </Link>
        </div>
      </div>

      <form className="yfMobileSearch" onSubmit={onSearch}>
        <input
          className="yfMobileSearch__input"
          placeholder="Search products"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
        <button className="yfMobileSearch__btn" type="submit" aria-label="Search">
          Search
        </button>
      </form>

      <nav className="yfMobileDock" aria-label="Mobile quick navigation">
        <NavLink to="/" className={({ isActive }) => `yfMobileDock__item ${isActive ? "isActive" : ""}`}>
          <span>Home</span>
        </NavLink>
        <NavLink to="/shop" className={({ isActive }) => `yfMobileDock__item ${isActive ? "isActive" : ""}`}>
          <span>Shop</span>
        </NavLink>
        <NavLink to="/wishlist" className={({ isActive }) => `yfMobileDock__item ${isActive ? "isActive" : ""}`}>
          <span>Wishlist</span>
          {wishlistCount > 0 ? <small className="yfMobileDock__badge">{wishlistCount}</small> : null}
        </NavLink>
        <NavLink to="/cart" className={({ isActive }) => `yfMobileDock__item ${isActive ? "isActive" : ""}`}>
          <span>Cart</span>
          {totalItems > 0 ? <small className="yfMobileDock__badge">{totalItems}</small> : null}
        </NavLink>
        <NavLink to={user ? "/profile" : "/login"} className={({ isActive }) => `yfMobileDock__item ${isActive ? "isActive" : ""}`}>
          <span>{user ? "Profile" : "Login"}</span>
        </NavLink>
      </nav>
    </header>
  );
};

export default Header;
