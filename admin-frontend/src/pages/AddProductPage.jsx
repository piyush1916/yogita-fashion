import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import ProductForm from "../components/products/ProductForm";
import PageHeader from "../components/ui/PageHeader";
import { createProduct, uploadProductImage } from "../services/productService";

export default function AddProductPage() {
  const navigate = useNavigate();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  const handleSubmit = async (values) => {
    setSubmitting(true);
    setError("");
    try {
      await createProduct(values);
      navigate("/products", { replace: true });
    } catch (apiError) {
      const message =
        typeof apiError?.response?.data?.message === "string"
          ? apiError.response.data.message
          : "Failed to add product.";
      setError(message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section>
      <PageHeader
        title="Add Product"
        description="Fill product details clearly so they are easy for customers to understand."
      />

      {error ? <p className="form-error-banner">{error}</p> : null}

      <ProductForm
        initialValues={{}}
        onSubmit={handleSubmit}
        onUploadImage={uploadProductImage}
        submitLabel="Save Product"
        submitting={submitting}
        secondaryAction={
          <Link to="/products" className="btn btn-outline">
            Cancel
          </Link>
        }
      />
    </section>
  );
}
