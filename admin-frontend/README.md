# Yogita Fashion Admin Frontend

Admin panel built with React + Vite for managing products, orders, and users from the same API used by the store frontend.

## Run locally

1. Install dependencies:

```bash
npm install
```

2. Create `.env` from `.env.example` and adjust API URL if needed:

```env
VITE_API_BASE_URL=https://yogita-fashion-btx2bxd32-piyush-patils-projects-765e81f9.vercel.app
```

3. Start admin app:

```bash
npm run dev
```

The dev server runs on port `5174`.

## Pages

- `/login`
- `/dashboard`
- `/products`
- `/products/new`
- `/products/:id/edit`
- `/orders`
- `/users`
