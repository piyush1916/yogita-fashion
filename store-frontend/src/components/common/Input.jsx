import React from "react";

const Input = ({ label, error, ...props }) => (
  <div className="flex flex-col">
    {label && <label className="mb-1 text-sm font-medium">{label}</label>}
    <input
      {...props}
      className={`border rounded px-3 py-2 focus:outline-none focus:ring ${
        error ? "border-red-500" : "border-gray-300"
      }`}
    />
    {error && <span className="text-red-600 text-xs mt-1">{error}</span>}
  </div>
);

export default Input;
