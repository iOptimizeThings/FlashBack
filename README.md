# ⚡ FlashBack

**High-performance backtesting engine for cryptocurrency strategies**

Processes 210 million ticks per second using C# optimization techniques. Built to test trading strategies honestly - shows real results, not cherry-picked wins.

## 🎯 Real Results

I tested **58 variations of 10 popular strategies** on 7.4M Bitcoin price points (2012-2026):

**📊 Key Finding: 43% of strategies LOST money**

### Top Performers
- **Z-Score Mean Reversion**: +$480k, 77% win rate ✅
- **Bollinger Bands**: +$462k, 70% win rate ✅
- **RSI (Oversold/Overbought)**: +$330k, 72% win rate ✅

### Biggest Losers
- **Fast EMAs (10-20 period)**: -$1M, 20% win rate ❌
- **Fast SMAs**: -$908k, 21% win rate ❌
- **MACD variations**: -$373k, 27% win rate ❌

**Why?** Bitcoin is choppy. Fast trend-following strategies get destroyed by whipsaw. Mean reversion strategies that fade extremes actually work.

## ⚡ Performance

```text
Dataset: 7.4M minute-by-minute Bitcoin ticks (14 years)
Load Time: 1.65 seconds
Processing: 210M ticks/second
58 Strategy Tests: ~8 seconds total
```

**How?**
- `readonly struct` for cache locality
- `Span<T>` for zero-allocation iteration
- No LINQ in hot loops
- Value types over reference types

## 🚀 Quick Start

```bash
# Clone
git clone [https://github.com/iOptimizeThings/FlashBack.git](https://github.com/iOptimizeThings/FlashBack.git)
cd FlashBack

# Run
dotnet run -c Release

# Choose Option 1 to test all strategies
# Or Option 2 for quick single-strategy test
```

Requires: .NET 8.0

## 📊 Test Data

To reproduce the benchmark results (210M ticks/sec), this project was tested using **Bitcoin 1-Minute Historical Data** from Kaggle.

* **Dataset Source:** [Bitcoin Historical Data (Kaggle)](https://www.kaggle.com/datasets/mczielinski/bitcoin-historical-data)
* **Specific File:** `btcusd_1-min_data.csv`
* **Timeframe:** Jan 2012 - Present

**Setup:**
1.  Download the `.csv` file from Kaggle.
2.  Rename it to `1.csv` (optional, for easier typing) or keep the original name.
3.  Place it in the root directory or copy the path when prompted.

## 📈 Strategies Implemented

**Trend Following:**
- Simple Moving Average (SMA)
- Exponential Moving Average (EMA)
- Dual MA Crossover

**Momentum:**
- RSI (Relative Strength Index)
- MACD (Moving Average Convergence Divergence)
- Stochastic Oscillator

**Volatility:**
- Bollinger Bands
- ATR Breakout

**Mean Reversion:**
- Z-Score Statistical Arbitrage

**Volume:**
- VWAP (Volume Weighted Average Price)

Each strategy tested with multiple parameter combinations (fast/slow periods, thresholds, etc.)

## 📊 Output

The engine generates:
- Console summary with top/bottom performers
- Detailed text reports with every trade
- CSV export for Excel analysis
- Sharpe ratios, max drawdown, win rates

## 🎓 What I Learned

1. **Most "textbook" strategies lose money on Bitcoin** - The classic SMA(50) that everyone recommends? Lost $482k.

2. **Mean reversion > Trend following for crypto** - Bitcoin oscillates more than it trends. Strategies that buy dips and sell rips work better.

3. **Parameter optimization matters** - SMA(10) lost $908k but SMA(500) made $46k. Same strategy, different settings.

4. **Win rate ≠ Profitability** - Some strategies had 70%+ win rates but still lost money due to a few huge losing trades.

## 🛠️ Tech Stack

- **Language:** C# 10 / .NET 8.0
- **CSV Parsing:** nietras.SeparatedValues (fastest .NET CSV library)
- **Architecture:** Value types, Span<T>, aggressive inlining
- **Paradigm:** Zero-allocation hot loops

## 🚀 Benchmark & Sharing

Want to see the engine in action? Follow these steps to generate the summary table:

1.  Run the application: `dotnet run -c Release`
2.  Select **Option 1**.
3.  Enter the path to your dataset (e.g., `1.csv`).
4.  Wait for the analysis to complete (approx. 15-20 seconds for 14 years of data).

### 📢 Share the Results

If you find this project useful, feel free to use these templates to share your results!

## 🤝 Contributing

PRs welcome! Especially for:
- Additional strategies (Ichimoku, Fibonacci, etc.)
- Multiple timeframe analysis
- Portfolio optimization
- Walk-forward testing

## 📝 License

MIT

---

**Note:** This is educational software. Past performance ≠ future results. Don't trade with money you can't afford to lose.