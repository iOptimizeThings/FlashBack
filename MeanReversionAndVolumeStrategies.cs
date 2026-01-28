using System.Runtime.CompilerServices;

namespace FlashBack;

/// <summary>
/// Z-Score Mean Reversion Strategy
/// Statistical arbitrage - trades deviations from mean
/// Buy when price is N standard deviations below mean
/// Sell when price reverts to mean or above
/// </summary>
public class ZScoreStrategy : IStrategy
{
    private readonly int _period;
    private readonly double _entryThreshold;
    private readonly double[] _priceBuffer;
    private int _bufferIndex;
    private int _count;
    private double _sum;
    private bool _isInPosition;
    
    private readonly List<Trade> _trades = new();
    private DateTime _entryTime;
    private double _entryPrice;

    public ZScoreStrategy(int period = 20, double entryThreshold = 2.0)
    {
        _period = period;
        _entryThreshold = entryThreshold;
        _priceBuffer = new double[period];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnTick(in Tick tick, int index)
    {
        if (_count >= _period)
        {
            _sum -= _priceBuffer[_bufferIndex];
        }
        
        _priceBuffer[_bufferIndex] = tick.Price;
        _sum += tick.Price;
        _count++;
        
        if (_count >= _period)
        {
            double mean = _sum / _period;
            
            // Calculate standard deviation
            double variance = 0;
            for (int i = 0; i < _period; i++)
            {
                double diff = _priceBuffer[i] - mean;
                variance += diff * diff;
            }
            double stdDev = Math.Sqrt(variance / _period);
            
            // Calculate Z-Score
            double zScore = stdDev > 0 ? (tick.Price - mean) / stdDev : 0;
            
            // Mean reversion logic
            if (zScore < -_entryThreshold && !_isInPosition)
            {
                // Price is significantly below mean - buy expecting reversion
                _isInPosition = true;
                _entryTime = tick.Timestamp;
                _entryPrice = tick.Price;
            }
            else if (zScore > 0 && _isInPosition)
            {
                // Price reverted to or above mean - take profit
                _isInPosition = false;
                
                var trade = new Trade
                {
                    EntryTime = _entryTime,
                    EntryPrice = _entryPrice,
                    ExitTime = tick.Timestamp,
                    ExitPrice = tick.Price,
                    ProfitLoss = tick.Price - _entryPrice,
                    ProfitLossPercent = ((tick.Price - _entryPrice) / _entryPrice) * 100
                };
                _trades.Add(trade);
            }
        }
        
        _bufferIndex = (_bufferIndex + 1) % _period;
    }

    public void OnComplete() { }
    
    public List<Trade> GetTrades() => _trades;

    public string GetStats()
    {
        var profitableTrades = _trades.Count(t => t.ProfitLoss > 0);
        var totalPL = _trades.Sum(t => t.ProfitLoss);
        var avgPL = _trades.Count > 0 ? _trades.Average(t => t.ProfitLoss) : 0;
        var winRate = _trades.Count > 0 ? (profitableTrades * 100.0 / _trades.Count) : 0;
        var largestWin = _trades.Count > 0 ? _trades.Max(t => t.ProfitLoss) : 0;
        var largestLoss = _trades.Count > 0 ? _trades.Min(t => t.ProfitLoss) : 0;
        
        return $"Z-Score Mean Reversion({_period}, {_entryThreshold}σ) Results:\n" +
               $"  Completed Trades: {_trades.Count:N0}\n" +
               $"    • Profitable: {profitableTrades:N0} ({winRate:F1}%)\n" +
               $"  Total P&L: ${totalPL:+0.00;-0.00}\n" +
               $"  Average P&L/Trade: ${avgPL:+0.00;-0.00}\n" +
               $"  Largest Win: ${largestWin:+0.00;-0.00}\n" +
               $"  Largest Loss: ${largestLoss:+0.00;-0.00}";
    }
}

/// <summary>
/// VWAP (Volume Weighted Average Price) Strategy
/// Institutional benchmark - buy below VWAP, sell above
/// Used by large traders to measure execution quality
/// </summary>
public class VWAPStrategy : IStrategy
{
    private double _cumulativePriceVolume;
    private long _cumulativeVolume;
    private bool _isInPosition;
    
    private readonly List<Trade> _trades = new();
    private DateTime _entryTime;
    private double _entryPrice;

    public VWAPStrategy()
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnTick(in Tick tick, int index)
    {
        _cumulativePriceVolume += tick.Price * tick.Volume;
        _cumulativeVolume += tick.Volume;
        
        if (_cumulativeVolume > 0)
        {
            double vwap = _cumulativePriceVolume / _cumulativeVolume;
            
            // Buy below VWAP (expecting price to rise to VWAP)
            // Sell above VWAP (expecting price to fall to VWAP)
            if (tick.Price < vwap && !_isInPosition)
            {
                _isInPosition = true;
                _entryTime = tick.Timestamp;
                _entryPrice = tick.Price;
            }
            else if (tick.Price > vwap && _isInPosition)
            {
                _isInPosition = false;
                
                var trade = new Trade
                {
                    EntryTime = _entryTime,
                    EntryPrice = _entryPrice,
                    ExitTime = tick.Timestamp,
                    ExitPrice = tick.Price,
                    ProfitLoss = tick.Price - _entryPrice,
                    ProfitLossPercent = ((tick.Price - _entryPrice) / _entryPrice) * 100
                };
                _trades.Add(trade);
            }
        }
    }

    public void OnComplete() { }
    
    public List<Trade> GetTrades() => _trades;

    public string GetStats()
    {
        var profitableTrades = _trades.Count(t => t.ProfitLoss > 0);
        var totalPL = _trades.Sum(t => t.ProfitLoss);
        var avgPL = _trades.Count > 0 ? _trades.Average(t => t.ProfitLoss) : 0;
        var winRate = _trades.Count > 0 ? (profitableTrades * 100.0 / _trades.Count) : 0;
        
        var vwap = _cumulativeVolume > 0 ? _cumulativePriceVolume / _cumulativeVolume : 0;
        
        return $"VWAP Strategy Results:\n" +
               $"  Final VWAP: ${vwap:F2}\n" +
               $"  Completed Trades: {_trades.Count:N0}\n" +
               $"    • Profitable: {profitableTrades:N0} ({winRate:F1}%)\n" +
               $"  Total P&L: ${totalPL:+0.00;-0.00}\n" +
               $"  Average P&L/Trade: ${avgPL:+0.00;-0.00}";
    }
}
