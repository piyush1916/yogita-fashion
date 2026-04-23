import axios from "axios";

const RAW_API_BASE_URL = (
  import.meta.env.VITE_API_BASE_URL || "http://127.0.0.1:5037"
).trim();
const API_BASE_URL = (
  /^https?:\/\//i.test(RAW_API_BASE_URL) ? RAW_API_BASE_URL : `http://${RAW_API_BASE_URL}`
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
