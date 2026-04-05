import axios, { type AxiosInstance } from 'axios';
import type { Order, RiskLimits, RiskUtilization } from '../types';

const API_BASE_URL = 'https://localhost:7001';

class ApiClient {
  private client: AxiosInstance;

  constructor() {
    this.client = axios.create({
      baseURL: API_BASE_URL,
      headers: {
        'Content-Type': 'application/json',
        'Accept-Encoding': 'br', // Brotli compression
      },
      timeout: 30000,
    });
  }

  // Orders
  async getOrders(): Promise<Order[]> {
    const response = await this.client.get<Order[]>('/orders');
    return response.data;
  }

  async getOrder(id: string): Promise<Order> {
    const response = await this.client.get<Order>(`/orders/${id}`);
    return response.data;
  }

  async createOrder(order: Partial<Order>): Promise<Order> {
    const response = await this.client.post<Order>('/orders', order);
    return response.data;
  }

  // Funds
  async getFunds(): Promise<any[]> {
    const response = await this.client.get('/funds');
    return response.data;
  }

  // Risk
  async getRiskLimits(fundId: string): Promise<RiskLimits> {
    const response = await this.client.get<RiskLimits>(`/funds/${fundId}/risk-limits`);
    return response.data;
  }

  async getRiskUtilization(fundId: string): Promise<RiskUtilization> {
    const response = await this.client.get<RiskUtilization>(`/risk/${fundId}/utilization`);
    return response.data;
  }

  // Audit
  async getAuditRecords(from: string, to: string, format: 'csv' | 'pdf'): Promise<any> {
    const response = await this.client.get('/audit/orders', {
      params: { from, to, format },
      responseType: 'blob',
    });
    return response.data;
  }
}

export const apiClient = new ApiClient();
