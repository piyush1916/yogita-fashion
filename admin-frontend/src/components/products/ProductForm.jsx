import { useEffect, useState } from "react";

const EMPTY_FORM = {
  name: "",
  description: "",
  category: "",
  price: "",
  originalPrice: "",
  size: "",
  color: "",
  stock: "",
  brand: "",
  imageUrl: "",
  featuredProduct: false,
  detailsMaterial: "Cotton blend",
  detailsFit: "Regular",
  detailsStyle: "Casual",
  detailsSleeve: "Half Sleeve",
  detailsWashCare: "Machine wash",
};

function validateForm(values) {
  const errors = {};

  if (!values.name.trim()) errors.name = "Product name is required.";
  if (!values.description.trim()) errors.description = "Description is required.";
  if (!values.category.trim()) errors.category = "Category is required.";
  if (!values.brand.trim()) errors.brand = "Brand is required.";
  if (!values.size.trim()) errors.size = "Size is required.";
  if (!values.color.trim()) errors.color = "Color is required.";
  if (!values.imageUrl.trim()) errors.imageUrl = "Image URL is required.";

  const price = Number(values.price);
  if (!Number.isFinite(price) || price <= 0) {
    errors.price = "Price must be greater than 0.";
  }

  if (values.originalPrice !== "") {
    const originalPrice = Number(values.originalPrice);
    if (!Number.isFinite(originalPrice) || originalPrice <= 0) {
      errors.originalPrice = "Original price must be greater than 0.";
    } else if (Number.isFinite(price) && originalPrice < price) {
      errors.originalPrice = "Original price should be equal to or higher than price.";
    }
  }

  const stock = Number(values.stock);
  if (!Number.isFinite(stock) || stock < 0) {
    errors.stock = "Stock cannot be negative.";
  }

  return errors;
}

function FieldError({ message }) {
  if (!message) return null;
  return <p className="form-error-text">{message}</p>;
}

export default function ProductForm({ initialValues, onSubmit, submitLabel, submitting, secondaryAction, onUploadImage }) {
  const [values, setValues] = useState({ ...EMPTY_FORM, ...(initialValues || {}) });
  const [errors, setErrors] = useState({});
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState("");

  useEffect(() => {
    setValues({ ...EMPTY_FORM, ...(initialValues || {}) });
    setErrors({});
    setUploadError("");
  }, [initialValues]);

  const handleChange = (event) => {
    const { name, value, type, checked } = event.target;
    setValues((prev) => ({
      ...prev,
      [name]: type === "checkbox" ? checked : value,
    }));
    setErrors((prev) => ({ ...prev, [name]: "" }));
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    const nextErrors = validateForm(values);

    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors);
      return;
    }

    await onSubmit(values);
  };

  const handleImageUpload = async (event) => {
    const file = event.target.files?.[0];
    if (!file || typeof onUploadImage !== "function") return;
    setUploading(true);
    setUploadError("");
    try {
      const result = await onUploadImage(file);
      if (result?.url) {
        setValues((prev) => ({ ...prev, imageUrl: result.url }));
        setErrors((prev) => ({ ...prev, imageUrl: "" }));
      } else {
        setUploadError("Upload did not return image URL.");
      }
    } catch (error) {
      const message =
        typeof error?.response?.data?.message === "string"
          ? error.response.data.message
          : "Failed to upload image.";
      setUploadError(message);
    } finally {
      setUploading(false);
      event.target.value = "";
    }
  };

  return (
    <form className="panel form-panel" onSubmit={handleSubmit} noValidate>
      <div className="form-grid">
        <label className="form-field">
          <span>Product Name *</span>
          <input name="name" value={values.name} onChange={handleChange} placeholder="Ex: Printed Rayon Kurti" />
          <FieldError message={errors.name} />
        </label>

        <label className="form-field">
          <span>Category *</span>
          <input name="category" value={values.category} onChange={handleChange} placeholder="Ex: Women" />
          <FieldError message={errors.category} />
        </label>

        <label className="form-field form-field-wide">
          <span>Description *</span>
          <textarea
            name="description"
            rows={4}
            value={values.description}
            onChange={handleChange}
            placeholder="Write a short, clear description"
          />
          <FieldError message={errors.description} />
        </label>

        <label className="form-field">
          <span>Price (INR) *</span>
          <input name="price" type="number" min="0" step="0.01" value={values.price} onChange={handleChange} />
          <FieldError message={errors.price} />
        </label>

        <label className="form-field">
          <span>Original Price (INR)</span>
          <input
            name="originalPrice"
            type="number"
            min="0"
            step="0.01"
            value={values.originalPrice}
            onChange={handleChange}
          />
          <FieldError message={errors.originalPrice} />
        </label>

        <label className="form-field">
          <span>Size *</span>
          <input name="size" value={values.size} onChange={handleChange} placeholder="Ex: S, M, L" />
          <FieldError message={errors.size} />
        </label>

        <label className="form-field">
          <span>Color *</span>
          <input name="color" value={values.color} onChange={handleChange} placeholder="Ex: Pink, White" />
          <FieldError message={errors.color} />
        </label>

        <label className="form-field">
          <span>Stock *</span>
          <input name="stock" type="number" min="0" step="1" value={values.stock} onChange={handleChange} />
          <FieldError message={errors.stock} />
        </label>

        <label className="form-field">
          <span>Brand *</span>
          <input name="brand" value={values.brand} onChange={handleChange} placeholder="Ex: Yogita Fashion" />
          <FieldError message={errors.brand} />
        </label>

        <label className="form-field form-field-wide">
          <span>Image URL *</span>
          <div className="inline-field-group">
            <input
              name="imageUrl"
              value={values.imageUrl}
              onChange={handleChange}
              placeholder="https://example.com/image.jpg"
            />
            <label className="btn btn-outline btn-sm file-btn">
              {uploading ? "Uploading..." : "Upload Image"}
              <input type="file" accept=".jpg,.jpeg,.png,.webp" onChange={handleImageUpload} disabled={uploading} hidden />
            </label>
          </div>
          {uploadError ? <p className="form-error-text">{uploadError}</p> : null}
          <FieldError message={errors.imageUrl} />
        </label>

        {values.imageUrl ? (
          <div className="form-field form-field-wide">
            <span>Image Preview</span>
            <img src={values.imageUrl} alt="Product preview" className="form-image-preview" />
          </div>
        ) : null}

        <div className="form-field form-field-wide">
          <h3 className="form-section-title">Product Details</h3>
          <p className="form-section-desc">These details will be shown to customers</p>
        </div>

        <label className="form-field">
          <span>Material</span>
          <input name="detailsMaterial" value={values.detailsMaterial} onChange={handleChange} placeholder="Ex: Cotton blend" />
        </label>

        <label className="form-field">
          <span>Fit</span>
          <input name="detailsFit" value={values.detailsFit} onChange={handleChange} placeholder="Ex: Regular" />
        </label>

        <label className="form-field">
          <span>Style</span>
          <input name="detailsStyle" value={values.detailsStyle} onChange={handleChange} placeholder="Ex: Casual" />
        </label>

        <label className="form-field">
          <span>Sleeve</span>
          <input name="detailsSleeve" value={values.detailsSleeve} onChange={handleChange} placeholder="Ex: Half Sleeve" />
        </label>

        <label className="form-field">
          <span>Wash Care</span>
          <input name="detailsWashCare" value={values.detailsWashCare} onChange={handleChange} placeholder="Ex: Machine wash" />
        </label>

        <label className="form-field form-checkbox">
          <input
            type="checkbox"
            name="featuredProduct"
            checked={Boolean(values.featuredProduct)}
            onChange={handleChange}
          />
          <span>Featured Product</span>
        </label>
      </div>

      <div className="form-actions">
        {secondaryAction}
        <button type="submit" className="btn btn-primary" disabled={submitting}>
          {submitting ? "Saving..." : submitLabel}
        </button>
      </div>
    </form>
  );
}
