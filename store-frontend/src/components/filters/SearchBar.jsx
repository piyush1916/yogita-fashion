import React from "react";

const SearchBar = ({ value, onChange, placeholder = "Search products..." }) => (
  <input
    type="text"
    value={value}
    onChange={(e) => onChange(e.target.value)}
    placeholder={placeholder}
    className="w-full border px-3 py-2 rounded"
  />
);

export default SearchBar;
