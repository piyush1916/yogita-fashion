import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useToast } from "../hooks/useToast";
import supportService from "../services/supportService";
import { SUPPORT } from "../utils/constants";
import { required, validateEmail, validatePhone } from "../utils/validators";

const SUBJECT_OPTIONS = [
  "General Support",
  "Order Issue",
  "Payment Issue",
  "Delivery Delay",
  "Return / Refund",
];

const initialForm = {
  name: "",
  contact: "",
  subject: SUBJECT_OPTIONS[0],
  orderId: "",
  message: "",
};

function isValidContact(value) {
  const input = String(value || "").trim();
  if (!input) return false;
  if (input.includes("@")) return validateEmail(input);
  return validatePhone(input);
}

function formatDate(value) {
  const ts = Date.parse(value || "");
  if (!Number.isFinite(ts)) return "N/A";
  return new Date(ts).toLocaleString("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export default function Support() {
  const { user } = useAuth();
  const toast = useToast();
  const [form, setForm] = useState(initialForm);
  const [errors, setErrors] = useState({});
  const [requests, setRequests] = useState([]);

  useEffect(() => {
    if (!user) return;
    setForm((prev) => ({
      ...prev,
      name: prev.name || user.name || "",
      contact: prev.contact || user.email || user.phone || "",
    }));

    let ignore = false;
    const loadRequests = async () => {
      try {
        const userRequests = await supportService.listRequestsByUser();
        if (!ignore) setRequests(userRequests);
      } catch {
        if (!ignore) setRequests([]);
      }
    };
    loadRequests();

    return () => {
      ignore = true;
    };
  }, [user]);

  const onChange = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
    if (errors[name]) {
      setErrors((prev) => ({ ...prev, [name]: "" }));
    }
  };

  const onSubmit = async (e) => {
    e.preventDefault();
    const nextErrors = {};

    if (!required(form.name)) nextErrors.name = "Name is required.";
    if (!required(form.contact)) nextErrors.contact = "Phone or email is required.";
    else if (!isValidContact(form.contact)) nextErrors.contact = "Enter a valid phone or email.";
    if (!required(form.message)) nextErrors.message = "Please enter your issue.";
    else if (String(form.message).trim().length < 10) nextErrors.message = "Message should be at least 10 characters.";

    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      toast.error("Please fix the highlighted fields.");
      return;
    }

    try {
      const ticket = await supportService.createRequest(form, user);
      toast.success(`Support ticket created: ${ticket.id}`);
      setRequests((prev) => [ticket, ...prev]);
      setForm((prev) => ({
        ...prev,
        subject: SUBJECT_OPTIONS[0],
        orderId: "",
        message: "",
      }));
    } catch (error) {
      toast.error(error?.message || "Unable to submit support request.");
    }
  };

  return (
    <section className="ordersPage">
      <div className="container ordersWrap">
        <p className="ordersBreadcrumb">
          <Link to="/profile">Your Account</Link>
          <span aria-hidden="true">{">"}</span>
          <span>Help / Support</span>
        </p>

        <div className="ordersHeader">
          <h1 className="ordersTitle">Help / Support</h1>
        </div>

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <div className="profileCard">
            <h2 className="profileSectionTitle">Raise a Support Request</h2>
            <p className="profileSubtext">Tell us your issue and we will get back to you soon.</p>

            <form className="profileForm mt-4" onSubmit={onSubmit}>
              <label className="profileField">
                <span>Name</span>
                <input name="name" value={form.name} onChange={onChange} placeholder="Your name" />
                {errors.name && <small>{errors.name}</small>}
              </label>

              <label className="profileField">
                <span>Phone or Email</span>
                <input
                  name="contact"
                  value={form.contact}
                  onChange={onChange}
                  placeholder="10-digit phone or email"
                />
                {errors.contact && <small>{errors.contact}</small>}
              </label>

              <label className="profileField">
                <span>Subject</span>
                <select className="ordersRange w-full" name="subject" value={form.subject} onChange={onChange}>
                  {SUBJECT_OPTIONS.map((option) => (
                    <option key={option} value={option}>
                      {option}
                    </option>
                  ))}
                </select>
              </label>

              <label className="profileField">
                <span>Order ID (optional)</span>
                <input name="orderId" value={form.orderId} onChange={onChange} placeholder="Example: 1735634982211" />
              </label>

              <label className="profileField" style={{ gridColumn: "1 / -1" }}>
                <span>Message</span>
                <textarea
                  name="message"
                  value={form.message}
                  onChange={onChange}
                  placeholder="Describe the issue in detail"
                  rows={5}
                  className="rounded-xl border border-white/20 bg-white/10 px-3 py-3 text-[color:var(--text)] outline-none"
                />
                {errors.message && <small>{errors.message}</small>}
              </label>

              <div className="profileFormActions">
                <button type="submit" className="profileBtn profileBtnPrimary">
                  Submit Request
                </button>
              </div>
            </form>
          </div>

          <div className="profileCard">
            <h2 className="profileSectionTitle">Direct Contact</h2>
            <p className="profileSubtext">For urgent help, contact us directly.</p>

            <div className="mt-4 grid gap-3">
              <a
                className="profileBtn profileBtnGhost justify-start"
                href={`https://wa.me/${SUPPORT.WHATSAPP_NUMBER}`}
                target="_blank"
                rel="noreferrer"
              >
                WhatsApp: +{SUPPORT.WHATSAPP_NUMBER}
              </a>
              <a className="profileBtn profileBtnGhost justify-start" href={`tel:${SUPPORT.PHONE.replace(/\s+/g, "")}`}>
                Call: {SUPPORT.PHONE}
              </a>
              <a className="profileBtn profileBtnGhost justify-start" href={`mailto:${SUPPORT.EMAIL}`}>
                Email: {SUPPORT.EMAIL}
              </a>
            </div>

            <div className="mt-6">
              <h3 className="profileSectionTitle" style={{ fontSize: "20px" }}>
                Previous Requests
              </h3>
              {!user ? (
                <div className="ordersPlaceholder mt-3">Login to view your previous support requests.</div>
              ) : requests.length === 0 ? (
                <div className="ordersPlaceholder mt-3">No requests yet. Submit your first support request.</div>
              ) : (
                <div className="mt-3 grid gap-3">
                  {requests.map((item) => (
                    <article
                      key={item.id}
                      className="rounded-xl border border-white/20 bg-white/5 p-4 shadow-[0_12px_28px_rgba(0,0,0,0.25)]"
                    >
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <strong>{item.id}</strong>
                        <span className="rounded-full bg-emerald-500/20 px-3 py-1 text-xs font-semibold text-emerald-200">
                          {item.status}
                        </span>
                      </div>
                      <p className="mt-2 text-sm text-[color:var(--muted)]">Subject: {item.subject}</p>
                      <p className="mt-1 text-sm text-[color:var(--muted)]">Created: {formatDate(item.createdAt)}</p>
                      {item.orderId && <p className="mt-1 text-sm text-[color:var(--muted)]">Order: {item.orderId}</p>}
                    </article>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
