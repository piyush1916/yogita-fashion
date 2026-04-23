import axios from "axios";

const RAW_API_BASE_URL = (
  import.meta.env.VITE_API_BASE_URL || "http://127.0.0.1:5037"
).trim();
const API_BASE_URL = (
  /^https?:\/\//i.test(RAW_API_BASE_URL) ? RAW_API_BASE_URL : `http://${RAW_API_BASE_URL}`
).replace(/\/+$/, "");

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
  headers: {
    "Content-Type": "application/json",
  },
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem("yf_admin_token");
  if (token) {
    config.headers = config.headers ?? {};
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error?.response?.status;
    const requestUrl = String(error?.config?.url || "").toLowerCase();
    const isLoginRequest = requestUrl.includes("/api/auth/login");

    if (status === 401 && !isLoginRequest) {
      localStorage.removeItem("yf_admin_session");
      localStorage.removeItem("yf_admin_token");
      if (typeof window !== "undefined" && !window.location.pathname.startsWith("/login")) {
        window.location.href = "/login";
      }
    }

    return Promise.reject(error);
  }
);

export default apiClient;
