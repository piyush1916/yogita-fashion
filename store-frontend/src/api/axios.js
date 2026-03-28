import axios from "axios";

const RAW_API_BASE_URL = (
  import.meta.env.VITE_API_BASE_URL || "https://yogita-fashion-btx2bxd32-piyush-patils-projects-765e81f9.vercel.app"
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
