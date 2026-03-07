import React from "react";
import { Link, useLocation } from "react-router-dom";
import { PROMO_CONFIG } from "../../utils/constants";

function formatAmount(amount) {
  const num = Number(amount) || 0;
  return num.toLocaleString("en-IN");
}

function getUpcomingLabel() {
  const now = Date.now();
  const startTime = Date.parse(PROMO_CONFIG.UPCOMING_START_DATE_ISO);

  if (!Number.isFinite(startTime)) {
    return `${PROMO_CONFIG.UPCOMING_SALE_NAME} coming soon`;
  }

  if (now >= startTime) {
    return `${PROMO_CONFIG.UPCOMING_SALE_NAME} is live now`;
  }

  const diffMs = startTime - now;
  const daysLeft = Math.ceil(diffMs / (24 * 60 * 60 * 1000));
  return `${PROMO_CONFIG.UPCOMING_SALE_NAME} starts in ${daysLeft} day${daysLeft === 1 ? "" : "s"}`;
}

export default function PromoBanner() {
  const location = useLocation();
  const isHomePage = location.pathname === "/";
  const isOffersPage = location.pathname === "/offers";

  if (!isHomePage && !isOffersPage) return null;

  const tierText = [...PROMO_CONFIG.ITEM_COUPON_TIERS]
    .sort((a, b) => Number(b.minItems) - Number(a.minItems))
    .map((tier) => `${tier.minItems}+ items: ${tier.discountPercent}% off`)
    .join(" | ");

  if (isHomePage) {
    return (
      <div className="promoBar" role="status" aria-live="polite">
        <div className="promoBar__inner promoBar__inner--compact">
          <p className="promoBar__text">
            {getUpcomingLabel()} | Use {PROMO_CONFIG.ACTIVE_OFFER_CODE}: {PROMO_CONFIG.ACTIVE_OFFER_DISCOUNT_PERCENT}% off
            above Rs {formatAmount(PROMO_CONFIG.ACTIVE_OFFER_MIN_SUBTOTAL)}
          </p>
          <Link to="/offers" className="promoBar__cta">
            More Offer
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="promoBar" role="status" aria-live="polite">
      <div className="promoBar__inner">
        <div className="promoBar__grid">
          <p className="promoBar__pill">
            <span>Upcoming Sale</span>
            <strong>{getUpcomingLabel()}</strong>
          </p>

          <p className="promoBar__pill">
            <span>Live Offer</span>
            <strong>
              Use {PROMO_CONFIG.ACTIVE_OFFER_CODE}: {PROMO_CONFIG.ACTIVE_OFFER_DISCOUNT_PERCENT}% off above Rs{" "}
              {formatAmount(PROMO_CONFIG.ACTIVE_OFFER_MIN_SUBTOTAL)}
            </strong>
          </p>

          <p className="promoBar__pill">
            <span>Item Coupon</span>
            <strong>
              {PROMO_CONFIG.ITEM_COUPON_CODE}: {tierText}
            </strong>
          </p>

          <Link to="/offers" className="promoBar__cta">
            View Offers
          </Link>
        </div>
      </div>
    </div>
  );
}
