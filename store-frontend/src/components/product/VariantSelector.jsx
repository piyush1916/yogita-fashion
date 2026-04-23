import React from "react";
import { SIZE_OPTIONS, COLOR_OPTIONS } from "../../utils/constants";

const colorMap = {
  red: "#DC2626",
  pink: "#EC4899",
  yellow: "#EAB308",
  blue: "#2563EB",
  black: "#000000",
  white: "#FFFFFF",
  green: "#16A34A",
  purple: "#A855F7",
  orange: "#EA580C",
  brown: "#92400E",
  gray: "#6B7280",
  navy: "#001F3F",
  beige: "#F5E6D3",
  gold: "#D97706",
  silver: "#C0C0C0",
};

const getColorValue = (colorName) => {
  const lower = String(colorName || "").toLowerCase().trim();
  return colorMap[lower] || "#CCCCCC";
};

const VariantSelector = ({ size, color, sizes = [], colors = [], onChange }) => (
  <div className="space-y-6">
    {sizes.length > 0 && (
      <div>
        <label className="block text-sm font-medium mb-2">Size</label>
        <div className="flex flex-wrap gap-2">
          {sizes.map((s) => (
            <button
              key={s}
              onClick={() => onChange({ size: s, color })}
              className={`px-4 py-2 border rounded font-medium transition ${
                size === s
                  ? "bg-indigo-600 text-white border-indigo-600"
                  : "bg-white text-gray-700 border-gray-300 hover:border-indigo-300"
              }`}
            >
              {s}
            </button>
          ))}
        </div>
      </div>
    )}
    
    {colors.length > 0 && (
      <div>
        <label className="block text-sm font-medium mb-2">Color</label>
        <div className="flex flex-wrap gap-3">
          {colors.map((c) => (
            <button
              key={c}
              onClick={() => onChange({ size, color: c })}
              className={`w-10 h-10 rounded-full border-2 transition ${
                color === c ? "border-gray-900 ring-2 ring-offset-2 ring-gray-400" : "border-gray-300"
              }`}
              style={{ backgroundColor: getColorValue(c) }}
              title={c}
            />
          ))}
        </div>
      </div>
    )}
  </div>
);

export default VariantSelector;
