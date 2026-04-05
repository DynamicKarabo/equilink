import { useState, useEffect } from 'react';
import type { RiskLimits, RiskUtilization } from '../types';
import { apiClient } from '../api/client';

interface RiskLimitGaugesProps {
  fundId: string;
}

const demoLimits: RiskLimits = {
  fundId: 'demo-fund',
  maxOrderSize: 100000,
  dailyLossLimit: 50000,
  concentrationLimit: 0.25,
};

const demoUtilization: RiskUtilization = {
  fundId: 'demo-fund',
  currentExposure: 35000,
  dailyPnL: -28500,
  positionConcentration: 0.18,
  maxOrderSizeUsed: 72000,
};

export function RiskLimitGauges({ fundId }: RiskLimitGaugesProps) {
  const [limits, setLimits] = useState<RiskLimits | null>(null);
  const [utilization, setUtilization] = useState<RiskUtilization | null>(null);
  const [loading, setLoading] = useState(true);
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    const fetchData = async () => {
      try {
        setLoading(true);
        const [limitsData, utilData] = await Promise.all([
          apiClient.getRiskLimits(fundId),
          apiClient.getRiskUtilization(fundId),
        ]);
        setLimits(limitsData);
        setUtilization(utilData);
        setIsConnected(true);
      } catch (err) {
        // API not available, use demo data
        setLimits(demoLimits);
        setUtilization({
          ...demoUtilization,
          maxOrderSizeUsed: Math.random() * 80000,
          dailyPnL: -Math.random() * 40000,
          positionConcentration: Math.random() * 0.2,
        });
        setIsConnected(false);
      } finally {
        setLoading(false);
      }
    };

    fetchData();
    const interval = setInterval(fetchData, 5000);
    return () => clearInterval(interval);
  }, [fundId]);

  if (loading && !limits) {
    return (
      <div className="bg-gray-900 border border-gray-700 rounded-lg p-4 flex items-center justify-center min-h-[200px]">
        <span className="text-gray-400">Loading risk limits...</span>
      </div>
    );
  }

  if (!limits || !utilization) {
    return (
      <div className="bg-gray-900 border border-gray-700 rounded-lg p-4 flex items-center justify-center min-h-[200px]">
        <span className="text-red-400">No data</span>
      </div>
    );
  }

  const maxOrderSizePercent = Math.min((utilization.maxOrderSizeUsed / limits.maxOrderSize) * 100, 100);
  const dailyPnLPercent = Math.min((Math.abs(utilization.dailyPnL) / limits.dailyLossLimit) * 100, 100);
  const concentrationPercent = Math.min((utilization.positionConcentration / limits.concentrationLimit) * 100, 100);

  const isDailyLossBreached = utilization.dailyPnL <= -limits.dailyLossLimit;

  return (
    <div className="bg-gray-900 border border-gray-700 rounded-lg p-4">
      <div className="flex justify-between items-center mb-4">
        <h3 className="text-sm font-medium text-gray-400">Risk Limits - Fund {fundId.slice(0, 8)}...</h3>
        {!isConnected && (
          <span className="text-xs text-yellow-500">Demo</span>
        )}
      </div>
      
      <div className="space-y-4">
        <GaugeBar 
          label="Max Order Size"
          value={utilization.maxOrderSizeUsed}
          max={limits.maxOrderSize}
          percent={maxOrderSizePercent}
          formatValue={(v) => v.toLocaleString(undefined, { maximumFractionDigits: 0 })}
        />
        
        <GaugeBar 
          label="Daily P&L"
          value={Math.abs(utilization.dailyPnL)}
          max={limits.dailyLossLimit}
          percent={dailyPnLPercent}
          prefix="$"
          isNegative={isDailyLossBreached}
          formatValue={(v) => v.toLocaleString(undefined, { maximumFractionDigits: 0 })}
        />
        
        <GaugeBar 
          label="Position Concentration"
          value={utilization.positionConcentration * 100}
          max={limits.concentrationLimit * 100}
          percent={concentrationPercent}
          formatValue={(v) => v.toFixed(1)}
        />
      </div>
    </div>
  );
}

interface GaugeBarProps {
  label: string;
  value: number;
  max: number;
  percent: number;
  prefix?: string;
  isNegative?: boolean;
  formatValue: (v: number) => string;
}

function GaugeBar({ label, value, max, percent, prefix = '', isNegative, formatValue }: GaugeBarProps) {
  const getColor = (): string => {
    if (isNegative) return 'bg-red-500';
    if (percent >= 80) return 'bg-red-500';
    if (percent >= 60) return 'bg-yellow-500';
    return 'bg-green-500';
  };

  const getTextColor = (): string => {
    if (isNegative) return 'text-red-400';
    if (percent >= 80) return 'text-red-400';
    if (percent >= 60) return 'text-yellow-400';
    return 'text-green-400';
  };

  return (
    <div className="space-y-1">
      <div className="flex justify-between items-center text-xs">
        <span className="text-gray-400">{label}</span>
        <span className={`font-mono ${getTextColor()}`}>
          {prefix}{formatValue(value)} / {prefix}{formatValue(max)}
        </span>
      </div>
      <div className="h-2 bg-gray-800 rounded-full overflow-hidden">
        <div 
          className={`h-full transition-all duration-300 ${getColor()}`}
          style={{ width: `${percent}%` }}
        />
      </div>
      <div className="text-right text-xs text-gray-500">
        {percent.toFixed(0)}% utilized
      </div>
    </div>
  );
}
