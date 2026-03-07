import apiClient from "../api/axios";
import { API } from "../api/endpoints";

export async function getAnalyticsSummary(days = 7) {
  const response = await apiClient.get(API.ANALYTICS_SUMMARY, { params: { days } });
  return response?.data ?? {
    days,
    totalRevenue: 0,
    totalOrders: 0,
    totalUsers: 0,
    totalProducts: 0,
    repeatCustomerCount: 0,
    dailySales: [],
    topProducts: [],
    repeatCustomers: [],
    statusBreakdown: [],
  };
}
