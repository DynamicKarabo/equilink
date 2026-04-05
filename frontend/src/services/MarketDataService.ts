import * as signalR from '@microsoft/signalr';
import type { OrderBook, Order, MarketDataSubscription } from '../types';

type OrderUpdateCallback = (order: Order) => void;
type OrderBookCallback = (orderBook: OrderBook) => void;

class MarketDataService {
  private connection: signalR.HubConnection | null = null;
  private orderUpdateCallbacks: OrderUpdateCallback[] = [];
  private orderBookCallbacks: Map<string, OrderBookCallback> = new Map();
  private subscribedSymbols: Set<string> = new Set();

  connect() {
    const hubUrl = 'wss://localhost:7001/hubs/marketdata';
    
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.setupHandlers();
    
    return this.connection.start();
  }

  private setupHandlers() {
    if (!this.connection) return;

    this.connection.on('OrderUpdated', (order: Order) => {
      this.orderUpdateCallbacks.forEach(cb => cb(order));
    });

    this.connection.on('OrderBookUpdate', (symbol: string, orderBook: OrderBook) => {
      const cb = this.orderBookCallbacks.get(symbol);
      if (cb) cb(orderBook);
    });
  }

  subscribeToSymbol(symbol: string, callback: OrderBookCallback) {
    if (!this.connection) {
      throw new Error('Not connected to hub');
    }

    if (!this.subscribedSymbols.has(symbol)) {
      const subscription: MarketDataSubscription = {
        symbol,
        action: 'subscribe',
      };
      this.connection.invoke('SubscribeToSymbol', subscription);
      this.subscribedSymbols.add(symbol);
    }

    this.orderBookCallbacks.set(symbol, callback);
  }

  unsubscribeFromSymbol(symbol: string) {
    if (!this.connection) return;

    if (this.subscribedSymbols.has(symbol)) {
      const subscription: MarketDataSubscription = {
        symbol,
        action: 'unsubscribe',
      };
      this.connection.invoke('UnsubscribeFromSymbol', subscription);
      this.subscribedSymbols.delete(symbol);
    }

    this.orderBookCallbacks.delete(symbol);
  }

  onOrderUpdate(callback: OrderUpdateCallback) {
    this.orderUpdateCallbacks.push(callback);
  }

  removeOrderUpdateCallback(callback: OrderUpdateCallback) {
    const index = this.orderUpdateCallbacks.indexOf(callback);
    if (index > -1) {
      this.orderUpdateCallbacks.splice(index, 1);
    }
  }

  disconnect() {
    if (this.connection) {
      this.connection.stop();
      this.connection = null;
    }
  }

  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  getSubscribedSymbols(): string[] {
    return Array.from(this.subscribedSymbols);
  }
}

export const marketDataService = new MarketDataService();
