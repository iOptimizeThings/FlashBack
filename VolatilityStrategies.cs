using System.Runtime.CompilerServices;

namespace FlashBack;

/// <summary>
/// Bollinger Bands Strategy
/// Bands are N standard deviations above/below moving average
/// Mean reversion: Buy at lower band, sell at upper band
/// </summary>
public class BollingerBandsStrategy : IStrategy
{
    private readonly int _period;
    private readonly double _numStdDev;
    private readonly double[] _priceBuffer;
    private int _bufferIndex;
    private int _count;
    private double _sum;
    private bool _isInPosition;
    
    private readonly List<Trade> _trades = new();
    private DateTime _entryTime;
    private double _entryPrice;

    public BollingerBandsStrategy(int period = 20, double numStdDev = 2.0)
    {
        _period = period;
        _numStdDev = numStdDev;
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
            double sma = _sum / _period;
            
            double variance = 0;
            for (int i = 0; i < _period; i++)
            {
                double diff = _priceBuffer[i] - sma;
                variance += diff * diff;
            }
            double stdDev = Math.Sqrt(variance / _period);
            
            double upperBand = sma + (_numStdDev * stdDev);
            double lowerBand = sma - (_numStdDev * stdDev);
            
            if (tick.Price <= lowerBand && !_isInPosition)
            {
                _isInPosition = true;
                _entryTime = tick.Timestamp;
                _entryPrice = tick.Price;
            }
            else if (tick.Price >= upperBand && _isInPosition)
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
        
        return $"Bollinger Bands({_period}, {_numStdDev}σ) Results:\n" +
               $"  Completed Trades: {_trades.Count:N0}\n" +
               $"    • Profitable: {profitableTrades:N0} ({winRate:F1}%)\n" +
               $"  Total P&L: ${totalPL:+0.00;-0.00}\n" +
               $"  Average P&L/Trade: ${avgPL:+0.00;-0.00}\n" +
               $"  Largest Win: ${largestWin:+0.00;-0.00}\n" +
               $"  Largest Loss: ${largestLoss:+0.00;-0.00}";
    }
}

/// <summary>
/// ATR (Average True Range) Breakout Strategy
/// Measures volatility and trades breakouts
/// Buy when price moves > N × ATR above recent low
/// Sell when price moves > N × ATR below recent high
/// </summary>
public class ATRBreakoutStrategy : IStrategy
{
    private readonly int _period;
    private readonly double _multiplier;
    private readonly double[] _trBuffer;
    private int _bufferIndex;
    private int _count;
    private double _atrSum;
    private double _prevClose;
    private double _recentHigh;
    private double _recentLow;
    private bool _isInPosition;
    
    private readonly List<Trade> _trades = new();
    private DateTime _entryTime;
    private double _entryPrice;

    public ATRBreakoutStrategy(int period = 14, double multiplier = 2.0)
    {
        _period = period;
        _multiplier = multiplier;
        _trBuffer = new double[period];
        _recentHigh = double.MinValue;
        _recentLow = double.MaxValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnTick(in Tick tick, int index)
    {
        if (index == 0)
        {
            _prevClose = tick.Price;
            _recentHigh = tick.Price;
            _recentLow = tick.Price;
            return;
        }

        // Calculate True Range
        double high = tick.Price;
        double low = tick.Price;
        double trueRange = Math.Max(
            high - low,
            Math.Max(
                Math.Abs(high - _prevClose),
                Math.Abs(low - _prevClose)
            )
        );
        
        if (_count >= _period)
        {
            _atrSum -= _trBuffer[_bufferIndex];
        }
        
        _trBuffer[_bufferIndex] = trueRange;
        _atrSum += trueRange;
        _count++;
        
        _prevClose = tick.Price;
        
        // Update recent high/low
        _recentHigh = Math.Max(_recentHigh, tick.Price);
        _recentLow = Math.Min(_recentLow, tick.Price);
        
        if (_count >= _period)
        {
            double atr = _atrSum / _period;
            double threshold = atr * _multiplier;
            
            // Breakout logic
            if (tick.Price > _recentHigh - threshold && !_isInPosition)
            {
                // Bullish breakout
                _isInPosition = true;
                _entryTime = tick.Timestamp;
                _entryPrice = tick.Price;
                _recentHigh = tick.Price; // Reset for trailing
            }
            else if (tick.Price < _recentLow + threshold && _isInPosition)
            {
                // Bearish breakdown / stop
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
                _recentLow = tick.Price; // Reset
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
        
        return $"ATR Breakout({_period}, {_multiplier}x) Results:\n" +
               $"  Completed Trades: {_trades.Count:N0}\n" +
               $"    • Profitable: {profitableTrades:N0} ({winRate:F1}%)\n" +
               $"  Total P&L: ${totalPL:+0.00;-0.00}\n" +
               $"  Average P&L/Trade: ${avgPL:+0.00;-0.00}";
    }
}
