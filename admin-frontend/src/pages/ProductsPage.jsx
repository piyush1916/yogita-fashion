import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import PageHeader from "../components/ui/PageHeader";
import LoadingState from "../components/ui/LoadingState";
import { deleteProduct, getProducts } from "../services/productService";
import { formatCurrency } from "../utils/formatters";

export default function ProductsPage() {
  const [products, setProducts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [deletingId, setDeletingId] = useState("");
  const [search, setSearch] = useState("");
  const [error, setError] = useState("");

  const loadProducts = async () => {
    setLoading(true);
    setError("");
    try {
      const items = await getProducts();
      setProducts(items);
    } catch {
      setError("Failed to load products.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadProducts();
  }, []);

  const filteredProducts = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return products;

    return products.filter((product) => {
      const searchableText = [product.name, product.category, product.brand, product.description].join(" ").toLowerCase();
      return searchableText.includes(term);
    });
  }, [products, search]);

  const handleDelete = async (product) => {
    const confirmDelete = window.confirm(`Delete "${product.name}"? This action cannot be undone.`);
    if (!confirmDelete) return;

    setDeletingId(product.id);
    setError("");
    try {
      await deleteProduct(product.id);
      setProducts((prev) => prev.filter((item) => item.id !== product.id));
    } catch {
      setError("Failed to delete product.");
    } finally {
      setDeletingId("");
    }
  };

  return (
    <section>
      <PageHeader
        title="Product Management"
        description="Search, add, edit, and remove products from your catalog."
        action={
          <Link className="btn btn-primary" to="/products/new">
            Add Product
          </Link>
        }
      />

      <div className="panel">
        <div className="panel-toolbar">
          <input
            className="search-input"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search by name, category, brand..."
          />
          <p className="panel-toolbar-text">Showing {filteredProducts.length} products</p>
        </div>

        {error ? <p className="form-error-banner">{error}</p> : null}
        {loading ? (
          <LoadingState label="Loading products..." />
        ) : filteredProducts.length === 0 ? (
          <p className="empty-text">No products found.</p>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Product</th>
                  <th>Category</th>
                  <th>Price</th>
                  <th>Stock</th>
                  <th>Featured</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredProducts.map((product) => (
                  <tr key={product.id}>
                    <td>
                      <div className="product-cell">
                        <img
                          src={product.imageUrl || "https://via.placeholder.com/80x80?text=Product"}
                          alt={product.name}
                          className="product-thumb"
                        />
                        <div>
                          <p className="product-name">{product.name}</p>
                          <p className="product-meta">{product.brand || "No brand"}</p>
                        </div>
                      </div>
                    </td>
                    <td>{product.category || "-"}</td>
                    <td>
                      <p>{formatCurrency(product.price)}</p>
                      {Number(product.originalPrice) > Number(product.price) ? (
                        <p className="strike-text">{formatCurrency(product.originalPrice)}</p>
                      ) : null}
                    </td>
                    <td>
                      <p>{product.stock}</p>
                      {product.isOutOfStock ? (
                        <span className="status-badge status-cancelled">Out of stock</span>
                      ) : product.isLowStock ? (
                        <span className="status-badge status-pending">Low stock</span>
                      ) : null}
                    </td>
                    <td>
                      <span className={`status-badge ${product.featuredProduct ? "status-featured" : "status-neutral"}`}>
                        {product.featuredProduct ? "Yes" : "No"}
                      </span>
                    </td>
                    <td>
                      <div className="table-actions">
                        <Link className="btn btn-sm btn-outline" to={`/products/${product.id}/edit`}>
                          Edit
                        </Link>
                        <button
                          type="button"
                          className="btn btn-sm btn-danger"
                          onClick={() => handleDelete(product)}
                          disabled={deletingId === product.id}
                        >
                          {deletingId === product.id ? "Deleting..." : "Delete"}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}
