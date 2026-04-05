import { useState, useEffect } from 'react';
import type { OrderBook, OrderBookEntry } from '../types';
import { marketDataService } from '../services/MarketDataService';

interface OrderBookDisplayProps {
  symbol: string;
}

export function OrderBookDisplay({ symbol }: OrderBookDisplayProps) {
  const [orderBook, setOrderBook] = useState<OrderBook | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    setIsConnected(marketDataService.isConnected());
    
    const handleUpdate = (book: OrderBook) => {
      setOrderBook(book);
    };

    try {
      marketDataService.subscribeToSymbol(symbol, handleUpdate);
    } catch (e) {
      console.warn('SignalR not connected, showing placeholder');
    }

    return () => {
      try {
        marketDataService.unsubscribeFromSymbol(symbol);
      } catch (e) {}
    };
  }, [symbol]);

  // Show demo data if not connected
  const demoData: OrderBook = {
    symbol,
    bids: [
      { price: 150.25, quantity: 500, orders: 3 },
      { price: 150.20, quantity: 1000, orders: 7 },
      { price: 150.15, quantity: 750, orders: 5 },
      { price: 150.10, quantity: 2000, orders: 12 },
      { price: 150.05, quantity: 1500, orders: 8 },
    ],
    asks: [
      { price: 150.30, quantity: 300, orders: 2 },
      { price: 150.35, quantity: 800, orders: 6 },
      { price: 150.40, quantity: 1200, orders: 9 },
      { price: 150.45, quantity: 600, orders: 4 },
      { price: 150.50, quantity: 900, orders: 7 },
    ],
    lastUpdate: new Date().toISOString(),
  };

  const displayBook = orderBook ?? (isConnected ? null : demoData);

  if (!displayBook) {
    return (
      <div className="bg-gray-900 border border-gray-700 rounded-lg p-4 flex items-center justify-center min-h-[400px]">
        <span className="text-gray-400">Loading order book for {symbol}...</span>
      </div>
    );
  }

  const maxQuantity = Math.max(
    ...displayBook.bids.map(b => b.quantity),
    ...displayBook.asks.map(a => a.quantity),
    1
  );

  return (
    <div className="bg-gray-900 border border-gray-700 rounded-lg p-4">
      <div className="flex justify-between items-center mb-3">
        <h3 className="text-sm font-medium text-gray-400">L2 Order Book - {symbol}</h3>
        {!isConnected && (
          <span className="text-xs text-yellow-500">Demo Mode</span>
        )}
      </div>
      
      <div className="flex flex-col">
        <div className="flex flex-col">
          <div className="grid grid-cols-3 gap-2 px-2 py-1 text-xs text-gray-500 border-b border-gray-700">
            <span>Price</span>
            <span className="text-right">Quantity</span>
            <span className="text-right">Orders</span>
          </div>
          {[...displayBook.asks].reverse().slice(0, 10).map((entry, i) => (
            <OrderBookRow key={`ask-${i}`} entry={entry} side="ask" maxQuantity={maxQuantity} />
          ))}
        </div>
        
        <div className="flex justify-between px-2 py-2 bg-gray-800 text-xs">
          <span className="text-gray-500">Spread</span>
          <span className="text-gray-300">
            {displayBook.asks[0] && displayBook.bids[0]
              ? (displayBook.asks[0].price - displayBook.bids[0].price).toFixed(2)
              : '-'}
          </span>
        </div>
        
        <div className="flex flex-col">
          {[...displayBook.bids].slice(0, 10).map((entry, i) => (
            <OrderBookRow key={`bid-${i}`} entry={entry} side="bid" maxQuantity={maxQuantity} />
          ))}
        </div>
      </div>
      
      <div className="mt-2 text-xs text-gray-500">
        Last update: {new Date(displayBook.lastUpdate).toLocaleTimeString()}
      </div>
    </div>
  );
}

interface OrderBookRowProps {
  entry: OrderBookEntry;
  side: 'bid' | 'ask';
  maxQuantity: number;
}

function OrderBookRow({ entry, side, maxQuantity }: OrderBookRowProps) {
  const depthPercent = (entry.quantity / maxQuantity) * 100;
  
  return (
    <div className={`grid grid-cols-3 gap-2 px-2 py-0.5 text-sm relative ${side === 'bid' ? 'hover:bg-gray-800' : ''}`}>
      <div 
        className={`absolute inset-y-0 right-0 opacity-20 ${side === 'bid' ? 'bg-green-900' : 'bg-red-900'}`}
        style={{ width: `${depthPercent}%` }}
      />
      <span className={`font-mono ${side === 'bid' ? 'text-green-400' : 'text-red-400'}`}>
        {entry.price.toFixed(2)}
      </span>
      <span className="font-mono text-right text-gray-300">{entry.quantity}</span>
      <span className="font-mono text-right text-gray-400">{entry.orders}</span>
    </div>
  );
}
