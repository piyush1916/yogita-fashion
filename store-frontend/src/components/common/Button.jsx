import React from "react";

const Button = ({ children, className = "", ...props }) => (
  <button
    {...props}
    className={`bg-indigo-600 hover:bg-indigo-700 text-white py-2 px-4 rounded ${className}`}
  >
    {children}
  </button>
);

export default Button;
