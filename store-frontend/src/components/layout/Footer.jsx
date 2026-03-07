import React from "react";
import { Link } from "react-router-dom";

export default function Footer() {
  return (
    <footer className="footer">
      <div className="footer-container">
        <div className="footer-col">
          <h3>Contact</h3>
          <p>Phone: +91 8805971681</p>
          <p>Email: support@yogitafashion.com</p>
          <p>Location: a-24 mathura nagar, lonkheda, shahada, maharashtra, india</p>
        </div>

        <div className="footer-col">
          <h3>Shop</h3>
          <p>
            <Link to="/shop">Shop</Link>
          </p>
          <p>
            <Link to="/shop?sort=newest">New In</Link>
          </p>
          <p>
            <Link to="/offers">Offers</Link>
          </p>
        </div>

        <div className="footer-col">
          <h3>Company</h3>
          <p>
            <Link to="/policies/terms">Terms</Link>
          </p>
          <p>
            <Link to="/policies/privacy">Privacy</Link>
          </p>
          <p>
            <Link to="/support">Support</Link>
          </p>
          <p>
            <Link to="/address">Address</Link>
          </p>
        </div>

        <div className="footer-col">
          <h3>Newsletter</h3>
          <p>Enter your email</p>
          <div className="newsletter">
            <input type="text" placeholder="Email address" />
            <button aria-label="Subscribe">-&gt;</button>
          </div>
        </div>
      </div>

      <div className="footer-bottom">
        <p>Copyright (c) Yogita Fashion</p>
        <div className="social">
          <span>IG</span>
          <span>FB</span>
          <span>X</span>
        </div>
      </div>
    </footer>
  );
}
