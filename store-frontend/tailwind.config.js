/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,jsx}"],
  theme: {
    extend: {
      colors: {
        brand: {
          50: "#fff1f4",
          100: "#ffe3e9",
          200: "#ffc7d4",
          300: "#ff9db4",
          400: "#ff7092",
          500: "#fc3d72",
          600: "#e91d5f",
          700: "#c40f4d",
          800: "#a21045",
          900: "#87133f",
        },
      },
      boxShadow: {
        soft: "0 8px 30px rgba(15, 23, 42, 0.08)",
      },
    },
  },
  plugins: [],
};
