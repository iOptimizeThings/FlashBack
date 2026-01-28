using System.Runtime.CompilerServices;

namespace FlashBack;

/// <summary>
/// Simple Moving Average (SMA) Strategy
/// Averages price over N periods with equal weight
/// Buy when price crosses above SMA, sell when crosses below
/// </summary>
public class SimpleMovingAverageStrategy : IStrategy
{
    private readonly int _period;
    private readonly double[] _priceBuffer;
    private int _bufferIndex;
    private int _tickCount;
    private double _sum;
    private bool _isInPosition;
    
    private readonly List<Trade> _trades = new();
    private DateTime _entryTime;
    private double _entryPrice;

    public SimpleMovingAverageStrategy(int period = 20)
    {
        _period = period;
        _priceBuffer = new double[period];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnTick(in Tick tick, int index)
    {
        if (_tickCount >= _period)
        {
            _sum -= _priceBuffer[_bufferIndex];
        }

        _priceBuffer[_bufferIndex] = tick.Price;
        _sum += tick.Price;
        _tickCount++;

        if (_tickCount >= _period)
        {
            double sma = _sum / _period;
            
            if (tick.Price > sma && !_isInPosition)
            {
                _isInPosition = true;
                _entryTime = tick.Timestamp;
                _entryPrice = tick.Price;
            }
            else if (tick.Price < sma && _isInPosition)
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
        
        return $"SMA({_period}) Results:\n" +
               $"  Completed Trades: {_trades.Count:N0}\n" +
               $"    • Profitable: {profitableTrades:N0} ({winRate:F1}%)\n" +
               $"  Total P&L: ${totalPL:+0.00;-0.00}\n" +
               $"  Average P&L/Trade: ${avgPL:+0.00;-0.00}\n" +
               $"  Largest Win: ${largestWin:+0.00;-0.00}\n" +
               $"  Largest Loss: ${largestLoss:+0.00;-0.00}";
    }
}

/// <summary>
/// Exponential Moving Average (EMA) Strategy
/// Gives more weight to recent prices than SMA
/// Buy when price crosses above EMA, sell when crosses below
/// </summary>
public class EMAStrategy : IStrategy
{
    private readonly int _period;
    private double _ema;
    private int _count;
    private bool _isInPosition;
    
    private readonly List<Trade> _trades = new();
    private DateTime _entryTime;
    private double _entryPrice;

    public EMAStrategy(int period = 20)
    {
        _period = period;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnTick(in Tick tick, int index)
    {
        if (_count == 0)
        {
            _ema = tick.Price;
        }
        else
        {
            double alpha = 2.0 / (_period + 1);
            _ema = (tick.Price * alpha) + (_ema * (1 - alpha));
        }
        
        if (_count >= _period)
        {
            if (tick.Price > _ema && !_isInPosition)
            {
                _isInPosition = true;
                _entryTime = tick.Timestamp;
                _entryPrice = tick.Price;
            }
            else if (tick.Price < _ema && _isInPosition)
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
        
        _count++;
    }

    public void OnComplete() { }
    
    public List<Trade> GetTrades() => _trades;

    public string GetStats()
    {
        var profitableTrades = _trades.Count(t => t.ProfitLoss > 0);
        var totalPL = _trades.Sum(t => t.ProfitLoss);
        var avgPL = _trades.Count > 0 ? _trades.Average(t => t.ProfitLoss) : 0;
        var winRate = _trades.Count > 0 ? (profitableTrades * 100.0 / _trades.Count) : 0;
        
        return $"EMA({_period}) Results:\n" +
               $"  Completed Trades: {_trades.Count:N0}\n" +
               $"    • Profitable: {profitableTrades:N0} ({winRate:F1}%)\n" +
               $"  Total P&L: ${totalPL:+0.00;-0.00}\n" +
               $"  Average P&L/Trade: ${avgPL:+0.00;-0.00}";
    }
}

/// <summary>
/// Dual Moving Average Crossover Strategy
/// Uses two EMAs - fast and slow
/// Buy when fast crosses above slow, sell when fast crosses below slow
/// Classic trend-following strategy
/// </summary>
public class DualMAStrategy : IStrategy
{
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
    private double _fastEMA;
    private double _slowEMA;
    private int _count;
    private bool _isInPosition;
    private bool _prevCross; // true if fast was above slow in previous tick
    
    private readonly List<Trade> _trades = new();
    private DateTime _entryTime;
    private double _entryPrice;

    public DualMAStrategy(int fastPeriod = 10, int slowPeriod = 50)
    {
        _fastPeriod = fastPeriod;
        _slowPeriod = slowPeriod;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnTick(in Tick tick, int index)
    {
        if (_count == 0)
        {
            _fastEMA = tick.Price;
            _slowEMA = tick.Price;
            _prevCross = false;
        }
        else
        {
            double fastAlpha = 2.0 / (_fastPeriod + 1);
            double slowAlpha = 2.0 / (_slowPeriod + 1);
            
            _fastEMA = (tick.Price * fastAlpha) + (_fastEMA * (1 - fastAlpha));
            _slowEMA = (tick.Price * slowAlpha) + (_slowEMA * (1 - slowAlpha));
        }
        
        if (_count >= _slowPeriod)
        {
            bool currentCross = _fastEMA > _slowEMA;
            
            // Detect crossover
            if (currentCross && !_prevCross && !_isInPosition)
            {
                // Golden cross - fast crossed above slow
                _isInPosition = true;
                _entryTime = tick.Timestamp;
                _entryPrice = tick.Price;
            }
            else if (!currentCross && _prevCross && _isInPosition)
            {
                // Death cross - fast crossed below slow
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
            
            _prevCross = currentCross;
        }
        
        _count++;
    }

    public void OnComplete() { }
    
    public List<Trade> GetTrades() => _trades;

    public string GetStats()
    {
        var profitableTrades = _trades.Count(t => t.ProfitLoss > 0);
        var totalPL = _trades.Sum(t => t.ProfitLoss);
        var avgPL = _trades.Count > 0 ? _trades.Average(t => t.ProfitLoss) : 0;
        var winRate = _trades.Count > 0 ? (profitableTrades * 100.0 / _trades.Count) : 0;
        
        return $"Dual MA({_fastPeriod}/{_slowPeriod}) Results:\n" +
               $"  Completed Trades: {_trades.Count:N0}\n" +
               $"    • Profitable: {profitableTrades:N0} ({winRate:F1}%)\n" +
               $"  Total P&L: ${totalPL:+0.00;-0.00}\n" +
               $"  Average P&L/Trade: ${avgPL:+0.00;-0.00}";
    }
}
