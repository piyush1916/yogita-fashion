import React, { useState } from "react";
import { SIZE_OPTIONS, COLOR_OPTIONS } from "../../utils/constants";

const FilterPanel = ({ categories = [], filters, onChange }) => {
  const [local, setLocal] = useState(filters);

  const handle = (field, value) => {
    const updated = { ...local, [field]: value };
    setLocal(updated);
    onChange(updated);
  };

  return (
    <div className="space-y-4">
      <div>
        <h4 className="font-semibold">Category</h4>
        <select
          value={local.category || ""}
          onChange={(e) => handle("category", e.target.value ? [e.target.value] : [])}
          className="mt-1 block w-full border border-gray-300 rounded bg-white text-gray-900"
        >
          <option value="">All</option>
          {categories.map((c) => (
            <option key={c} value={c}>
              {c}
            </option>
          ))}
        </select>
      </div>
      <div>
        <h4 className="font-semibold">Price Range</h4>
        <div className="flex space-x-2">
          <input
            type="number"
            placeholder="Min"
            value={local.price?.min || ""}
            onChange={(e) => handle("price", { ...local.price, min: Number(e.target.value) || 0 })}
            className="w-1/2 border border-gray-300 px-2 py-1 rounded bg-white text-gray-900 placeholder-gray-500"
          />
          <input
            type="number"
            placeholder="Max"
            value={local.price?.max || ""}
            onChange={(e) => handle("price", { ...local.price, max: Number(e.target.value) || 0 })}
            className="w-1/2 border border-gray-300 px-2 py-1 rounded bg-white text-gray-900 placeholder-gray-500"
          />
        </div>
      </div>
      <div>
        <h4 className="font-semibold">Size</h4>
        <div className="flex flex-wrap gap-2">
          {SIZE_OPTIONS.map((s) => (
            <button
              key={s}
              className={`px-2 py-1 border rounded ${local.size?.includes(s) ? "bg-indigo-600 text-white" : ""}`}
              onClick={() => {
                const arr = local.size || [];
                const has = arr.includes(s);
                handle(
                  "size",
                  has ? arr.filter((x) => x !== s) : [...arr, s]
                );
              }}
            >
              {s}
            </button>
          ))}
        </div>
      </div>
      <div>
        <h4 className="font-semibold">Color</h4>
        <div className="flex flex-wrap gap-2">
          {COLOR_OPTIONS.map((c) => (
            <button
              key={c}
              className={`px-2 py-1 border rounded ${local.color?.includes(c) ? "bg-indigo-600 text-white" : ""}`}
              onClick={() => {
                const arr = local.color || [];
                const has = arr.includes(c);
                handle(
                  "color",
                  has ? arr.filter((x) => x !== c) : [...arr, c]
                );
              }}
            >
              {c}
            </button>
          ))}
        </div>
      </div>
      <button
        className="text-sm text-red-500"
        onClick={() => {
          setLocal({});
          onChange({});
        }}
      >
        Clear Filters
      </button>
    </div>
  );
};

export default FilterPanel;
