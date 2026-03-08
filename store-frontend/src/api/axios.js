import axios from "axios";

const RAW_API_BASE_URL = (
  import.meta.env.VITE_API_BASE_URL || "yogita-fashion-api-production.up.railway.app"
).trim();
const API_BASE_URL = (
  /^https?:\/\//i.test(RAW_API_BASE_URL) ? RAW_API_BASE_URL : `https://${RAW_API_BASE_URL}`
).replace(/\/+$/, "");

const axiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 10000,
  headers: {
    "Content-Type": "application/json",
  },
});

axiosInstance.interceptors.request.use((config) => {
  const token = localStorage.getItem("yf_auth_token");
  if (token) {
    config.headers = config.headers ?? {};
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export default axiosInstance;
