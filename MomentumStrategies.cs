using System.Runtime.CompilerServices;

namespace FlashBack;

/// <summary>
/// RSI (Relative Strength Index) Strategy
/// Measures momentum - overbought when > 70, oversold when < 30
/// Buy when oversold, sell when overbought
/// </summary>
public class RSIStrategy : IStrategy
{
    private readonly int _period;
    private readonly double _oversoldThreshold;
    private readonly double _overboughtThreshold;
    
    private readonly double[] _gains;
    private readonly double[] _losses;
    private int _count;
    private double _avgGain;
    private double _avgLoss;
    private double _lastPrice;
    private bool _isInPosition;
    
    private readonly List<Trade> _trades = new();
    private DateTime _entryTime;
    private double _entryPrice;

    public RSIStrategy(int period = 14, double oversoldThreshold = 30, double overboughtThreshold = 70)
    {
        _period = period;
        _oversoldThreshold = oversoldThreshold;
        _overboughtThreshold = overboughtThreshold;
        _gains = new double[period];
        _losses = new double[period];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnTick(in Tick tick, int index)
    {
        if (index == 0)
        {
            _lastPrice = tick.Price;
            return;
        }

        double change = tick.Price - _lastPrice;
        _lastPrice = tick.Price;

        if (_count < _period)
        {
            _gains[_count] = change > 0 ? change : 0;
            _losses[_count] = change < 0 ? -change : 0;
            _count++;

            if (_count == _period)
            {
                double sumGain = 0, sumLoss = 0;
                for (int i = 0; i < _period; i++)
                {
                    sumGain += _gains[i];
                    sumLoss += _losses[i];
                }
                _avgGain = sumGain / _period;
                _avgLoss = sumLoss / _period;
            }
        }
        else
        {
            double gain = change > 0 ? change : 0;
            double loss = change < 0 ? -change : 0;

            _avgGain = (_avgGain * (_period - 1) + gain) / _period;
            _avgLoss = (_avgLoss * (_period - 1) + loss) / _period;

            double rsi = 100;
            if (_avgLoss > 0)
            {
                double rs = _avgGain / _avgLoss;
                rsi = 100 - (100 / (1 + rs));
            }

            if (rsi < _oversoldThreshold && !_isInPosition)
            {
                _isInPosition = true;
                _entryTime = tick.Timestamp;
                _entryPrice = tick.Price;
            }
            else if (rsi > _overboughtThreshold && _isInPosition)
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
        var largestWin = _trades.Count > 0 ? _trades.Max(t => t.ProfitLoss) : 0;
        var largestLoss = _trades.Count > 0 ? _trades.Min(t => t.ProfitLoss) : 0;
        
        return $"RSI({_period}, {_oversoldThreshold}/{_overboughtThreshold}) Results:\n" +
               $"  Completed Trades: {_trades.Count:N0}\n" +
               $"    • Profitable: {profitableTrades:N0} ({winRate:F1}%)\n" +
               $"  Total P&L: ${totalPL:+0.00;-0.00}\n" +
               $"  Average P&L/Trade: ${avgPL:+0.00;-0.00}\n" +
               $"  Largest Win: ${largestWin:+0.00;-0.00}\n" +
               $"  Largest Loss: ${largestLoss:+0.00;-0.00}";
    }
}

/// <summary>
/// MACD (Moving Average Convergence Divergence) Strategy
/// Uses fast EMA, slow EMA, and signal line
/// Buy when MACD crosses above signal, sell when crosses below
/// </summary>
public class MACDStrategy : IStrategy
{
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
    private readonly int _signalPeriod;
    
    private double _fastEMA;
    private double _slowEMA;
    private double _signalEMA;
    private int _count;
    private bool _isInPosition;
    private double _prevMACD;
    
    private readonly List<Trade> _trades = new();
    private DateTime _entryTime;
    private double _entryPrice;

    public MACDStrategy(int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        _fastPeriod = fastPeriod;
        _slowPeriod = slowPeriod;
        _signalPeriod = signalPeriod;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnTick(in Tick tick, int index)
    {
        if (_count == 0)
        {
            _fastEMA = tick.Price;
            _slowEMA = tick.Price;
            _signalEMA = 0;
            _prevMACD = 0;
        }
        else
        {
            double fastAlpha = 2.0 / (_fastPeriod + 1);
            double slowAlpha = 2.0 / (_slowPeriod + 1);
            double signalAlpha = 2.0 / (_signalPeriod + 1);
            
            _fastEMA = (tick.Price * fastAlpha) + (_fastEMA * (1 - fastAlpha));
            _slowEMA = (tick.Price * slowAlpha) + (_slowEMA * (1 - slowAlpha));
            
            double macd = _fastEMA - _slowEMA;
            
            if (_count >= _slowPeriod)
            {
                if (_count == _slowPeriod)
                {
                    _signalEMA = macd;
                }
                else
                {
                    _signalEMA = (macd * signalAlpha) + (_signalEMA * (1 - signalAlpha));
                }
                
                if (_count > _slowPeriod + _signalPeriod)
                {
                    bool bullishCross = _prevMACD <= _signalEMA && macd > _signalEMA;
                    bool bearishCross = _prevMACD >= _signalEMA && macd < _signalEMA;
                    
                    if (bullishCross && !_isInPosition)
                    {
                        _isInPosition = true;
                        _entryTime = tick.Timestamp;
                        _entryPrice = tick.Price;
                    }
                    else if (bearishCross && _isInPosition)
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
                
                _prevMACD = macd;
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
        
        return $"MACD({_fastPeriod},{_slowPeriod},{_signalPeriod}) Results:\n" +
               $"  Completed Trades: {_trades.Count:N0}\n" +
               $"    • Profitable: {profitableTrades:N0} ({winRate:F1}%)\n" +
               $"  Total P&L: ${totalPL:+0.00;-0.00}\n" +
               $"  Average P&L/Trade: ${avgPL:+0.00;-0.00}";
    }
}

/// <summary>
/// Stochastic Oscillator Strategy
/// Compares closing price to price range over N periods
/// Buy when oversold (< 20), sell when overbought (> 80)
/// </summary>
public class StochasticStrategy : IStrategy
{
    private readonly int _period;
    private readonly int _smoothK;
    private readonly int _smoothD;
    private readonly double _oversoldThreshold;
    private readonly double _overboughtThreshold;
    
    private readonly double[] _highBuffer;
    private readonly double[] _lowBuffer;
    private readonly double[] _closeBuffer;
    private readonly double[] _kBuffer;
    private int _bufferIndex;
    private int _count;
    private bool _isInPosition;
    
    private readonly List<Trade> _trades = new();
    private DateTime _entryTime;
    private double _entryPrice;

    public StochasticStrategy(int period = 14, int smoothK = 3, int smoothD = 3, 
                             double oversoldThreshold = 20, double overboughtThreshold = 80)
    {
        _period = period;
        _smoothK = smoothK;
        _smoothD = smoothD;
        _oversoldThreshold = oversoldThreshold;
        _overboughtThreshold = overboughtThreshold;
        
        _highBuffer = new double[period];
        _lowBuffer = new double[period];
        _closeBuffer = new double[period];
        _kBuffer = new double[smoothK];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnTick(in Tick tick, int index)
    {
        int idx = _count % _period;
        _highBuffer[idx] = tick.Price;
        _lowBuffer[idx] = tick.Price;
        _closeBuffer[idx] = tick.Price;
        
        if (_count >= _period)
        {
            double highestHigh = _highBuffer.Max();
            double lowestLow = _lowBuffer.Min();
            
            double rawK = 0;
            if (highestHigh != lowestLow)
            {
                rawK = ((tick.Price - lowestLow) / (highestHigh - lowestLow)) * 100;
            }
            
            _kBuffer[_count % _smoothK] = rawK;
            
            if (_count >= _period + _smoothK)
            {
                double k = _kBuffer.Average();
                
                if (k < _oversoldThreshold && !_isInPosition)
                {
                    _isInPosition = true;
                    _entryTime = tick.Timestamp;
                    _entryPrice = tick.Price;
                }
                else if (k > _overboughtThreshold && _isInPosition)
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
        
        return $"Stochastic({_period},{_smoothK},{_smoothD}) Results:\n" +
               $"  Completed Trades: {_trades.Count:N0}\n" +
               $"    • Profitable: {profitableTrades:N0} ({winRate:F1}%)\n" +
               $"  Total P&L: ${totalPL:+0.00;-0.00}\n" +
               $"  Average P&L/Trade: ${avgPL:+0.00;-0.00}";
    }
}
