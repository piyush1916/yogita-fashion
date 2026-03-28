# Yogita Fashion Store Frontend

A complete React + Vite store frontend for a women's fashion e-commerce experience.

## Tech Stack

- React (Vite)
- React Router
- Tailwind CSS
- Axios
- Context API (Cart, Auth, Toast)
- Mock JSON data (API-ready service layer)

## Setup

1. Install dependencies:

```bash
npm install
```

Create environment file from example:

```bash
cp .env.example .env
```

On Windows PowerShell:

```powershell
Copy-Item .env.example .env
```

Run development server:

```bash
npm run dev
```

## Build

```bash
npm run build
```

## Environment Variables

`.env.example` contains:

```env
VITE_API_BASE_URL=https://yogita-fashion-btx2bxd32-piyush-patils-projects-765e81f9.vercel.app
```

`VITE_API_BASE_URL` is used in `src/api/axios.js` and is ready for ASP.NET Core API integration.

## ASP.NET Core API + CORS Note

When you connect this frontend to ASP.NET Core later, enable CORS for your frontend origin (example: `https://yogita-fashion-btx2bxd32-piyush-patils-projects-765e81f9.vercel.app`) in the backend:

- Register a named CORS policy in `Program.cs`
- Allow methods and headers
- Apply `app.UseCors(...)` before auth/endpoints middleware as needed

## Features

- Home, shop, product details, cart, checkout, track order
- Policy pages and contact/about
- Search (debounced), filtering, sorting, pagination
- Variant-based cart (`productId + size + color`)
- Coupon support (`FIRSTORDER` 10% once)
- Order placement and tracking timeline (mock)
- WhatsApp floating CTA
