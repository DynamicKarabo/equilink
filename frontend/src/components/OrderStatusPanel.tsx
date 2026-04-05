import { useState, useEffect, useRef } from 'react';
import type { Order, OrderStatus } from '../types';
import { marketDataService } from '../services/MarketDataService';

interface OrderStatusPanelProps {
  fundId?: string;
}

const statusColors: Record<OrderStatus, string> = {
  New: 'bg-yellow-500/20 text-yellow-400 border-yellow-500/30',
  RiskValidating: 'bg-yellow-500/20 text-yellow-400 border-yellow-500/30',
  Approved: 'bg-blue-500/20 text-blue-400 border-blue-500/30',
  Rejected: 'bg-red-500/20 text-red-400 border-red-500/30',
  Submitted: 'bg-blue-500/20 text-blue-400 border-blue-500/30',
  PartiallyFilled: 'bg-orange-500/20 text-orange-400 border-orange-500/30',
  Filled: 'bg-green-500/20 text-green-400 border-green-500/30',
  Cancelled: 'bg-red-500/20 text-red-400 border-red-500/30',
};

const demoOrders: Order[] = [
  {
    orderId: 'ord-001',
    fundId: 'fund-123',
    symbol: 'AAPL',
    side: 'BUY',
    quantity: 100,
    limitPrice: 150.50,
    status: 'Filled',
    filledQuantity: 100,
    averagePrice: 150.45,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
  {
    orderId: 'ord-002',
    fundId: 'fund-123',
    symbol: 'MSFT',
    side: 'SELL',
    quantity: 50,
    limitPrice: 380.00,
    status: 'PartiallyFilled',
    filledQuantity: 25,
    averagePrice: 379.80,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
  {
    orderId: 'ord-003',
    fundId: 'fund-456',
    symbol: 'GOOGL',
    side: 'BUY',
    quantity: 200,
    status: 'RiskValidating',
    filledQuantity: 0,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
];

export function OrderStatusPanel({ fundId }: OrderStatusPanelProps) {
  const [orders, setOrders] = useState<Order[]>([]);
  const [filter, setFilter] = useState<OrderStatus | 'ALL'>('ALL');
  const [isConnected, setIsConnected] = useState(false);
  const tableRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setIsConnected(marketDataService.isConnected());

    const handleOrderUpdate = (updatedOrder: Order) => {
      setOrders(prev => {
        const index = prev.findIndex(o => o.orderId === updatedOrder.orderId);
        if (index >= 0) {
          const newOrders = [...prev];
          newOrders[index] = updatedOrder;
          return newOrders;
        }
        return [updatedOrder, ...prev];
      });
    };

    try {
      marketDataService.onOrderUpdate(handleOrderUpdate);
    } catch (e) {
      // Not connected yet
    }
  }, []);

  const displayOrders = orders.length > 0 ? orders : (isConnected ? [] : demoOrders);

  const filteredOrders = displayOrders
    .filter(o => !fundId || o.fundId === fundId)
    .filter(o => filter === 'ALL' || o.status === filter);

  const latestOrder = filteredOrders[0];
  useEffect(() => {
    if (latestOrder && tableRef.current) {
      tableRef.current.scrollTop = 0;
    }
  }, [latestOrder]);

  return (
    <div className="bg-gray-900 border border-gray-700 rounded-lg p-4 flex flex-col h-full">
      <div className="flex justify-between items-center mb-3">
        <div className="flex items-center gap-2">
          <h3 className="text-sm font-medium text-gray-400">Order Status</h3>
          {!isConnected && orders.length === 0 && (
            <span className="text-xs text-yellow-500">Demo</span>
          )}
        </div>
        <select 
          value={filter} 
          onChange={(e) => setFilter(e.target.value as OrderStatus | 'ALL')}
          className="px-2 py-1 bg-gray-800 border border-gray-600 rounded text-sm text-gray-300 focus:outline-none focus:border-blue-500"
        >
          <option value="ALL">All Orders</option>
          <option value="New">New</option>
          <option value="RiskValidating">Risk Validating</option>
          <option value="Approved">Approved</option>
          <option value="Submitted">Submitted</option>
          <option value="PartiallyFilled">Partially Filled</option>
          <option value="Filled">Filled</option>
          <option value="Rejected">Rejected</option>
          <option value="Cancelled">Cancelled</option>
        </select>
      </div>

      <div className="flex-1 overflow-auto" ref={tableRef}>
        <table className="w-full text-xs">
          <thead>
            <tr className="text-gray-500 border-b border-gray-700 sticky top-0 bg-gray-900">
              <th className="text-left px-2 py-2 font-medium">Order ID</th>
              <th className="text-left px-2 py-2 font-medium">Symbol</th>
              <th className="text-left px-2 py-2 font-medium">Side</th>
              <th className="text-right px-2 py-2 font-medium">Qty</th>
              <th className="text-right px-2 py-2 font-medium">Filled</th>
              <th className="text-right px-2 py-2 font-medium">Price</th>
              <th className="text-center px-2 py-2 font-medium">Status</th>
              <th className="text-right px-2 py-2 font-medium">Updated</th>
            </tr>
          </thead>
          <tbody>
            {filteredOrders.length === 0 ? (
              <tr>
                <td colSpan={8} className="text-center py-8 text-gray-500">No orders to display</td>
              </tr>
            ) : (
              filteredOrders.map(order => (
                <tr key={order.orderId} className="border-b border-gray-800 hover:bg-gray-800/50">
                  <td className="px-2 py-2 font-mono text-gray-400">{order.orderId.slice(0, 8)}...</td>
                  <td className="px-2 py-2 font-medium">{order.symbol}</td>
                  <td className={`px-2 py-2 font-medium ${order.side === 'BUY' ? 'text-green-400' : 'text-red-400'}`}>
                    {order.side}
                  </td>
                  <td className="px-2 py-2 text-right font-mono">{order.quantity}</td>
                  <td className="px-2 py-2 text-right font-mono text-gray-400">{order.filledQuantity}</td>
                  <td className="px-2 py-2 text-right font-mono text-gray-400">
                    {order.limitPrice?.toFixed(2) ?? 'Market'}
                  </td>
                  <td className="px-2 py-2 text-center">
                    <span className={`inline-flex px-2 py-0.5 rounded-full text-xs font-medium border ${statusColors[order.status]}`}>
                      {order.status}
                    </span>
                  </td>
                  <td className="px-2 py-2 text-right text-gray-500">{new Date(order.updatedAt).toLocaleTimeString()}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="mt-3 pt-3 border-t border-gray-700 text-xs text-gray-500">
        Total: {filteredOrders.length} orders
      </div>
    </div>
  );
}
