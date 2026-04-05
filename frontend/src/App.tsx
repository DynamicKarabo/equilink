import { useState, useEffect } from 'react';
import { OrderBookDisplay } from './components/OrderBookDisplay';
import { OrderStatusPanel } from './components/OrderStatusPanel';
import { RiskLimitGauges } from './components/RiskLimitGauges';
import { marketDataService } from './services/MarketDataService';

function App() {
  const [connected, setConnected] = useState(false);
  const [selectedSymbol, setSelectedSymbol] = useState('AAPL');
  const [selectedFundId, setSelectedFundId] = useState<string>('');

  useEffect(() => {
    marketDataService.connect()
      .then(() => setConnected(true))
      .catch(err => {
        console.error('Failed to connect:', err);
        setConnected(false);
      });

    return () => {
      marketDataService.disconnect();
    };
  }, []);

  const symbols = ['AAPL', 'MSFT', 'GOOGL', 'AMZN', 'TSLA'];

  return (
    <div className="min-h-screen bg-gray-950 text-gray-100">
      {/* Header */}
      <header className="bg-gray-900 border-b border-gray-700 px-6 py-4 flex justify-between items-center">
        <h1 className="text-xl font-semibold">EquiLink Trading Gateway</h1>
        <div className="flex items-center gap-2 text-sm">
          <span className={`w-2 h-2 rounded-full ${connected ? 'bg-green-500 shadow-[0_0_8px_rgba(34,197,94,0.8)]' : 'bg-red-500'}`} />
          <span className="text-gray-400">{connected ? 'Connected' : 'Disconnected'}</span>
        </div>
      </header>

      {/* Controls */}
      <div className="bg-gray-900 border-b border-gray-700 px-6 py-3 flex gap-6">
        <div className="flex items-center gap-2">
          <label className="text-sm text-gray-400">Symbol:</label>
          <select 
            value={selectedSymbol} 
            onChange={(e) => setSelectedSymbol(e.target.value)}
            className="px-3 py-1.5 bg-gray-800 border border-gray-600 rounded text-sm focus:outline-none focus:border-blue-500"
          >
            {symbols.map(s => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        </div>
        <div className="flex items-center gap-2">
          <label className="text-sm text-gray-400">Fund ID:</label>
          <input 
            type="text" 
            value={selectedFundId}
            onChange={(e) => setSelectedFundId(e.target.value)}
            placeholder="Enter fund ID..."
            className="w-48 px-3 py-1.5 bg-gray-800 border border-gray-600 rounded text-sm focus:outline-none focus:border-blue-500 placeholder:text-gray-600"
          />
        </div>
      </div>

      {/* Main Grid */}
      <main className="p-4 grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Order Book - spans full height on large screens */}
        <section className="lg:row-span-2">
          <OrderBookDisplay symbol={selectedSymbol} />
        </section>

        {/* Orders */}
        <section className="min-h-[300px]">
          <OrderStatusPanel fundId={selectedFundId || undefined} />
        </section>

        {/* Risk */}
        <section className="min-h-[300px]">
          {selectedFundId ? (
            <RiskLimitGauges fundId={selectedFundId} />
          ) : (
            <div className="bg-gray-900 border border-gray-700 rounded-lg p-4 flex items-center justify-center min-h-[200px]">
              <span className="text-gray-500">Enter a Fund ID to view risk limits</span>
            </div>
          )}
        </section>
      </main>
    </div>
  );
}

export default App;
