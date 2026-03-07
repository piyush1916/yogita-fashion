export const required = (value) =>
  String(value ?? "")
    .trim()
    .length > 0;

export const validateName = (value) =>
  /^[a-zA-Z ]{2,50}$/.test(String(value ?? "").trim());

export const validatePhone = (value) =>
  /^[6-9]\d{9}$/.test(String(value ?? "").trim());

export const validateEmail = (value) =>
  /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(value ?? "").trim());

export const validatePincode = (value) =>
  /^\d{6}$/.test(String(value ?? "").trim());

export const validateCheckoutForm = (form) => {
  const errors = {};

  if (!required(form.name)) {
    errors.name = "Name is required.";
  } else if (!validateName(form.name)) {
    errors.name = "Enter a valid name.";
  }

  if (!required(form.phone)) {
    errors.phone = "Phone is required.";
  } else if (!validatePhone(form.phone)) {
    errors.phone = "Enter a valid 10-digit phone number.";
  }

  if (!required(form.email)) {
    errors.email = "Email is required.";
  } else if (!validateEmail(form.email)) {
    errors.email = "Enter a valid email address.";
  }

  if (!required(form.address)) {
    errors.address = "Address is required.";
  }

  if (!required(form.city)) {
    errors.city = "City is required.";
  }

  if (!required(form.pincode)) {
    errors.pincode = "Pincode is required.";
  } else if (!validatePincode(form.pincode)) {
    errors.pincode = "Enter a valid 6-digit pincode.";
  }

  return errors;
};

export const validateTrackOrderForm = (form) => {
  const errors = {};

  if (!required(form.orderId)) {
    errors.orderId = "Order ID is required.";
  }

  if (!required(form.contact)) {
    errors.contact = "Phone or email is required.";
  } else {
    const contactValue = String(form.contact).trim();
    const isEmail = contactValue.includes("@");

    if (isEmail && !validateEmail(contactValue)) {
      errors.contact = "Enter a valid email.";
    }

    if (!isEmail && !validatePhone(contactValue)) {
      errors.contact = "Enter a valid 10-digit phone.";
    }
  }

  return errors;
};
