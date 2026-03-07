import React from "react";
import { SIZE_OPTIONS, COLOR_OPTIONS } from "../../utils/constants";

const VariantSelector = ({ size, color, sizes = [], colors = [], onChange }) => (
  <div className="space-y-4">
    {sizes.length > 0 && (
      <div>
        <label className="block text-sm font-medium">Size</label>
        <select
          value={size}
          onChange={(e) => onChange({ size: e.target.value, color })}
          className="mt-1 block w-full border-gray-300 rounded-md"
        >
          <option value="">Select size</option>
          {sizes.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
      </div>
    )}
    {colors.length > 0 && (
      <div>
        <label className="block text-sm font-medium">Color</label>
        <select
          value={color}
          onChange={(e) => onChange({ size, color: e.target.value })}
          className="mt-1 block w-full border-gray-300 rounded-md"
        >
          <option value="">Select color</option>
          {colors.map((c) => (
            <option key={c} value={c}>
              {c}
            </option>
          ))}
        </select>
      </div>
    )}
  </div>
);

export default VariantSelector;
