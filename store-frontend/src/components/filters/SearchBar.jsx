import React from "react";

const SearchBar = ({ value, onChange, placeholder = "Search products..." }) => (
  <input
    type="text"
    value={value}
    onChange={(e) => onChange(e.target.value)}
    placeholder={placeholder}
    className="w-full border border-gray-300 px-3 py-2 rounded bg-white text-gray-900 placeholder-gray-500"
  />
);

export default SearchBar;
