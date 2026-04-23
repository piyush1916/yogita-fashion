import React from "react";

const SortDropdown = ({ value, onChange, options = [] }) => (
  <select
    value={value || ""}
    onChange={(e) => onChange(e.target.value)}
    className="border border-gray-300 px-2 py-1 rounded bg-white text-gray-900"
  >
    <option value="">Sort By</option>
    {options.map((opt) => (
      <option key={opt.value} value={opt.value}>
        {opt.label}
      </option>
    ))}
  </select>
);

export default SortDropdown;
