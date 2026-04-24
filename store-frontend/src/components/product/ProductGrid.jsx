import React from "react";
import ProductCard from "./ProductCard";

const ProductGrid = ({ products }) => (
  <div className="grid grid-cols-1 min-[420px]:grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-4">
    {products.map((p) => (
      <ProductCard key={p.id} product={p} />
    ))}
  </div>
);

export default ProductGrid;
