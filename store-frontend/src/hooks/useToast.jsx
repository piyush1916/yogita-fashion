// FILE: src/hooks/useToast.jsx
import React, { createContext, useCallback, useContext, useMemo, useState } from "react";

const ToastContext = createContext(null);

const TOAST_TYPES = {
  success: "success",
  error: "error",
  info: "info",
};

function uid() {
  return Math.random().toString(36).slice(2) + Date.now().toString(36);
}

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);

  const removeToast = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const showToast = useCallback(
    (message, type = TOAST_TYPES.info, options = {}) => {
      const id = uid();
      const duration = typeof options.duration === "number" ? options.duration : 2500;

      const toast = { id, message, type };
      setToasts((prev) => [...prev, toast]);

      window.setTimeout(() => removeToast(id), duration);
      return id;
    },
    [removeToast]
  );

  const api = useMemo(
    () => ({
      showToast,
      removeToast,
      success: (msg, opts) => showToast(msg, TOAST_TYPES.success, opts),
      error: (msg, opts) => showToast(msg, TOAST_TYPES.error, opts),
      info: (msg, opts) => showToast(msg, TOAST_TYPES.info, opts),
    }),
    [showToast, removeToast]
  );

  return (
    <ToastContext.Provider value={api}>
      {children}

      {/* Toast UI */}
      <div className="fixed right-4 top-4 z-[9999] flex w-[92vw] max-w-sm flex-col gap-2 sm:w-full">
        {toasts.map((t) => (
          <div
            key={t.id}
            role="status"
            className={[
              "rounded-xl px-4 py-3 text-sm shadow-lg ring-1",
              "backdrop-blur bg-white/95",
              t.type === "success" ? "ring-emerald-200" : "",
              t.type === "error" ? "ring-rose-200" : "",
              t.type === "info" ? "ring-slate-200" : "",
            ].join(" ")}
          >
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="font-semibold">
                  {t.type === "success" && "Success"}
                  {t.type === "error" && "Error"}
                  {t.type === "info" && "Info"}
                </div>
                <div className="text-slate-700 break-words">{t.message}</div>
              </div>

              <button
                type="button"
                onClick={() => removeToast(t.id)}
                className="shrink-0 rounded-lg px-2 py-1 text-slate-600 hover:bg-slate-100"
                aria-label="Close toast"
              >
                X
              </button>
            </div>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) {
    throw new Error("useToast must be used within <ToastProvider>");
  }
  return ctx;
}
