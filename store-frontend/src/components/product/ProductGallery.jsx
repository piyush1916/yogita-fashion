import React, { useState } from "react";

const ProductGallery = ({ images = [] }) => {
  const [current, setCurrent] = useState(0);
  
  if (!images.length) {
    return (
      <div className="w-full bg-gray-200 rounded-lg overflow-hidden flex items-center justify-center" style={{ aspectRatio: "1" }}>
        <span className="text-gray-500">No images</span>
      </div>
    );
  }

  return (
    <div className="w-full space-y-3">
      <div className="w-full bg-gray-100 rounded-lg overflow-hidden" style={{ aspectRatio: "1" }}>
        <img 
          src={images[current]} 
          alt="Product" 
          className="w-full h-full object-cover" 
        />
      </div>

      {images.length > 1 && (
        <div className="flex gap-2 overflow-x-auto pb-2">
          {images.map((img, idx) => (
            <button
              key={idx}
              onClick={() => setCurrent(idx)}
              className={`flex-shrink-0 w-20 h-20 rounded-lg overflow-hidden border-2 transition ${
                idx === current 
                  ? "border-indigo-600 ring-2 ring-indigo-300" 
                  : "border-gray-300 hover:border-gray-400"
              }`}
            >
              <img 
                src={img} 
                alt={`Product ${idx + 1}`} 
                className="w-full h-full object-cover" 
              />
            </button>
          ))}
        </div>
      )}
    </div>
  );
};

export default ProductGallery;
