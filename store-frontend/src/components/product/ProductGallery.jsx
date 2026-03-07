import React, { useState } from "react";

const ProductGallery = ({ images = [] }) => {
  const [current, setCurrent] = useState(0);
  if (!images.length) return null;
  return (
    <div>
      <img src={images[current]} alt="" className="w-full h-96 object-cover mb-2" />
      <div className="flex gap-2">
        {images.map((img, idx) => (
          <img
            key={idx}
            src={img}
            alt=""
            className={`w-16 h-16 object-cover cursor-pointer border ${
              idx === current ? "border-indigo-600" : "border-gray-300"
            }`}
            onClick={() => setCurrent(idx)}
          />
        ))}
      </div>
    </div>
  );
};

export default ProductGallery;
