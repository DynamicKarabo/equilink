export interface Order {
  orderId: string;
  fundId: string;
  symbol: string;
  side: 'BUY' | 'SELL';
  quantity: number;
  limitPrice?: number;
  status: OrderStatus;
  filledQuantity: number;
  averagePrice?: number;
  createdAt: string;
  updatedAt: string;
}

export type OrderStatus = 
  | 'New'
  | 'RiskValidating'
  | 'Approved'
  | 'Rejected'
  | 'Submitted'
  | 'PartiallyFilled'
  | 'Filled'
  | 'Cancelled';

export interface OrderBookEntry {
  price: number;
  quantity: number;
  orders: number;
}

export interface OrderBook {
  symbol: string;
  bids: OrderBookEntry[];
  asks: OrderBookEntry[];
  lastUpdate: string;
}

export interface RiskLimits {
  fundId: string;
  maxOrderSize: number;
  dailyLossLimit: number;
  concentrationLimit: number;
}

export interface RiskUtilization {
  fundId: string;
  currentExposure: number;
  dailyPnL: number;
  positionConcentration: number;
  maxOrderSizeUsed: number;
}

export interface MarketDataSubscription {
  symbol: string;
  action: 'subscribe' | 'unsubscribe';
}
