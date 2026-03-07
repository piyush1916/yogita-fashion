import React from "react";
import { SUPPORT } from "../../utils/constants";

const WhatsAppFloat = () => {
  const message = encodeURIComponent(
    "Hi! I want to order: [Product Name] | Size: [S/M/L/XL] | Color: [ ] | City: [ ]"
  );
  const url = `https://wa.me/${SUPPORT.WHATSAPP_NUMBER}?text=${message}`;
  return (
    <a
      href={url}
      target="_blank"
      rel="noreferrer"
      className="fixed bottom-4 right-4 bg-green-500 p-3 rounded-full shadow-lg text-white z-50"
    >
      <svg xmlns="http://www.w3.org/2000/svg" className="h-6 w-6" fill="currentColor" viewBox="0 0 24 24">
        <path d="M20.52 3.48a11.476 11.476 0 00-16.24 0c-4.49 4.48-4.49 11.75 0 16.23l-1.9 5.53 5.7-1.88a11.463 11.463 0 0016.24-16.24zm-8.52 17.02c-5.18 0-9.4-4.21-9.4-9.4 0-5.18 4.21-9.4 9.4-9.4s9.4 4.21 9.4 9.4-4.22 9.4-9.4 9.4zm5.1-7.95l-1.54-.77c-.21-.11-.45-.17-.69-.17-.47 0-.88.24-1.11.63l-.5 1.08c-.13.29-.38.5-.69.6l-2.23.74c-.29.1-.6.05-.85-.13l-1.02-.77c-.26-.2-.4-.52-.36-.84l.17-1.34c.03-.26-.05-.52-.23-.72l-1.33-1.33c-.2-.2-.46-.26-.71-.19l-1.3.3c-.36.09-.69-.11-.8-.44l-.36-1.04c-.10-.33.04-.68.34-.85l3.76-2.26c.30-.18.67-.20.98-.04l1.06.48c.33.15.59.44.69.79l.34 1.17c.10.35.34.64.66.82l1.12.59c.33.17.74.16 1.06-.02l1.56-.93c.29-.17.65-.17.94 0l1.17.7c.31.19.67.19.97.01l1.4-.78c.35-.2.74-.02.92.33l.57 1.09c.18.35.06.76-.31.97z"/>
      </svg>
    </a>
  );
};

export default WhatsAppFloat;
