import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import ProductForm from "../components/products/ProductForm";
import PageHeader from "../components/ui/PageHeader";
import LoadingState from "../components/ui/LoadingState";
import { getProductById, updateProduct, uploadProductImage } from "../services/productService";

export default function EditProductPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [product, setProduct] = useState(null);
  const [error, setError] = useState("");

  useEffect(() => {
    let ignore = false;

    const loadProduct = async () => {
      setLoading(true);
      setError("");
      try {
        const item = await getProductById(id);
        if (!ignore) {
          if (!item) {
            setError("Product not found.");
          } else {
            setProduct(item);
          }
        }
      } catch {
        if (!ignore) {
          setError("Failed to load product.");
        }
      } finally {
        if (!ignore) {
          setLoading(false);
        }
      }
    };

    loadProduct();
    return () => {
      ignore = true;
    };
  }, [id]);

  const handleSubmit = async (values) => {
    setSubmitting(true);
    setError("");
    try {
      await updateProduct(id, values);
      navigate("/products", { replace: true });
    } catch (apiError) {
      const message =
        typeof apiError?.response?.data?.message === "string"
          ? apiError.response.data.message
          : "Failed to update product.";
      setError(message);
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <section>
        <PageHeader title="Edit Product" description="Update product details for your store catalog." />
        <LoadingState label="Loading product..." />
      </section>
    );
  }

  return (
    <section>
      <PageHeader title="Edit Product" description="Update product details for your store catalog." />

      {error ? <p className="form-error-banner">{error}</p> : null}

      {product ? (
        <ProductForm
          initialValues={product}
          onSubmit={handleSubmit}
          onUploadImage={uploadProductImage}
          submitLabel="Update Product"
          submitting={submitting}
          secondaryAction={
            <Link to="/products" className="btn btn-outline">
              Cancel
            </Link>
          }
        />
      ) : (
        <div className="panel">
          <p className="empty-text">Product not found.</p>
          <Link className="btn btn-outline" to="/products">
            Back to Products
          </Link>
        </div>
      )}
    </section>
  );
}
