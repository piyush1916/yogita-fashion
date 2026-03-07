import React from "react";
import { Link } from "react-router-dom";
import { PROMO_CONFIG } from "../utils/constants";

function formatAmount(amount) {
  return (Number(amount) || 0).toLocaleString("en-IN");
}

function getUpcomingText() {
  const now = Date.now();
  const start = Date.parse(PROMO_CONFIG.UPCOMING_START_DATE_ISO);
  if (!Number.isFinite(start)) return `${PROMO_CONFIG.UPCOMING_SALE_NAME} coming soon`;
  if (now >= start) return `${PROMO_CONFIG.UPCOMING_SALE_NAME} is live now`;
  const days = Math.ceil((start - now) / (24 * 60 * 60 * 1000));
  return `${PROMO_CONFIG.UPCOMING_SALE_NAME} starts in ${days} day${days === 1 ? "" : "s"}`;
}

export default function Offers() {
  const itemCouponText = [...PROMO_CONFIG.ITEM_COUPON_TIERS]
    .sort((a, b) => Number(b.minItems) - Number(a.minItems))
    .map((tier) => `${tier.minItems}+ items: ${tier.discountPercent}% off`)
    .join(" | ");

  return (
    <section className="ordersPage">
      <div className="container ordersWrap">
        <div className="ordersHeader">
          <h1 className="ordersTitle">All Offers</h1>
        </div>

        <div className="grid gap-4 md:grid-cols-3">
          <article className="profileCard">
            <h2 className="profileSectionTitle">Upcoming Sale</h2>
            <p className="profileSubtext">{getUpcomingText()}</p>
            <div className="profileActions">
              <Link to="/shop" className="profileBtn profileBtnGhost">
                Shop Collection
              </Link>
            </div>
          </article>

          <article className="profileCard">
            <h2 className="profileSectionTitle">Live Offer</h2>
            <p className="profileSubtext">
              Use coupon <strong>{PROMO_CONFIG.ACTIVE_OFFER_CODE}</strong> and get{" "}
              {PROMO_CONFIG.ACTIVE_OFFER_DISCOUNT_PERCENT}% off on orders above Rs{" "}
              {formatAmount(PROMO_CONFIG.ACTIVE_OFFER_MIN_SUBTOTAL)}.
            </p>
            <div className="profileActions">
              <Link to="/shop" className="profileBtn profileBtnPrimary">
                Shop Now
              </Link>
            </div>
          </article>

          <article className="profileCard">
            <h2 className="profileSectionTitle">Item Coupon</h2>
            <p className="profileSubtext">
              Use <strong>{PROMO_CONFIG.ITEM_COUPON_CODE}</strong> | {itemCouponText}
            </p>
            <div className="profileActions">
              <Link to="/cart" className="profileBtn profileBtnGhost">
                Apply In Cart
              </Link>
            </div>
          </article>
        </div>
      </div>
    </section>
  );
}
