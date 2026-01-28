using System.Text;

namespace FlashBack;

/// <summary>
/// Runs multiple strategies with parameter variations
/// Generates comprehensive comparison report
/// </summary>
public class StrategyRunner
{
    public class StrategyResult
    {
        public string StrategyName { get; set; } = "";
        public int TotalTrades { get; set; }
        public int ProfitableTrades { get; set; }
        public double WinRate { get; set; }
        public double TotalPL { get; set; }
        public double AvgPL { get; set; }
        public double LargestWin { get; set; }
        public double LargestLoss { get; set; }
        public double SharpeRatio { get; set; }
        public double MaxDrawdown { get; set; }
    }

    public static List<StrategyResult> RunAllStrategies(TickEngine engine)
    {
        var results = new List<StrategyResult>();
        
        Console.WriteLine("\n" + new string('═', 70));
        Console.WriteLine("  RUNNING ALL STRATEGIES - COMPREHENSIVE BACKTEST");
        Console.WriteLine(new string('═', 70));
        Console.WriteLine($"\nDataset: {engine.TickCount:N0} ticks\n");

        // 1. SMA variations
        Console.WriteLine("📊 Testing SMA (Simple Moving Average)...");
        foreach (var period in new[] { 10, 20, 50, 100, 200, 500 })
        {
            var strategy = new SimpleMovingAverageStrategy(period);
            engine.Run(strategy);
            results.Add(AnalyzeStrategy($"SMA({period})", strategy.GetTrades()));
            Console.Write(".");
        }
        Console.WriteLine(" ✓");

        // 2. EMA variations
        Console.WriteLine("📊 Testing EMA (Exponential Moving Average)...");
        foreach (var period in new[] { 10, 20, 50, 100, 200 })
        {
            var strategy = new EMAStrategy(period);
            engine.Run(strategy);
            results.Add(AnalyzeStrategy($"EMA({period})", strategy.GetTrades()));
            Console.Write(".");
        }
        Console.WriteLine(" ✓");

        // 3. Dual MA variations
        Console.WriteLine("📊 Testing Dual MA (Moving Average Crossover)...");
        foreach (var (fast, slow) in new[] { (10, 50), (20, 100), (50, 200) })
        {
            var strategy = new DualMAStrategy(fast, slow);
            engine.Run(strategy);
            results.Add(AnalyzeStrategy($"DualMA({fast}/{slow})", strategy.GetTrades()));
            Console.Write(".");
        }
        Console.WriteLine(" ✓");

        // 4. RSI variations
        Console.WriteLine("📊 Testing RSI (Relative Strength Index)...");
        foreach (var period in new[] { 7, 14, 21 })
        {
            foreach (var (low, high) in new[] { (20, 80), (30, 70), (25, 75) })
            {
                var strategy = new RSIStrategy(period, low, high);
                engine.Run(strategy);
                results.Add(AnalyzeStrategy($"RSI({period},{low}/{high})", strategy.GetTrades()));
                Console.Write(".");
            }
        }
        Console.WriteLine(" ✓");

        // 5. MACD variations
        Console.WriteLine("📊 Testing MACD (Moving Average Convergence Divergence)...");
        foreach (var (fast, slow, signal) in new[] { (12, 26, 9), (8, 21, 7), (10, 24, 9) })
        {
            var strategy = new MACDStrategy(fast, slow, signal);
            engine.Run(strategy);
            results.Add(AnalyzeStrategy($"MACD({fast},{slow},{signal})", strategy.GetTrades()));
            Console.Write(".");
        }
        Console.WriteLine(" ✓");

        // 6. Stochastic variations
        Console.WriteLine("📊 Testing Stochastic Oscillator...");
        foreach (var period in new[] { 14, 21 })
        {
            foreach (var (low, high) in new[] { (20, 80), (30, 70) })
            {
                var strategy = new StochasticStrategy(period, 3, 3, low, high);
                engine.Run(strategy);
                results.Add(AnalyzeStrategy($"Stochastic({period},{low}/{high})", strategy.GetTrades()));
                Console.Write(".");
            }
        }
        Console.WriteLine(" ✓");

        // 7. Bollinger Bands variations
        Console.WriteLine("📊 Testing Bollinger Bands...");
        foreach (var period in new[] { 10, 20, 30 })
        {
            foreach (var stdDev in new[] { 1.5, 2.0, 2.5 })
            {
                var strategy = new BollingerBandsStrategy(period, stdDev);
                engine.Run(strategy);
                results.Add(AnalyzeStrategy($"Bollinger({period},{stdDev:F1}σ)", strategy.GetTrades()));
                Console.Write(".");
            }
        }
        Console.WriteLine(" ✓");

        // 8. ATR Breakout variations
        Console.WriteLine("📊 Testing ATR Breakout...");
        foreach (var period in new[] { 10, 14, 20 })
        {
            foreach (var mult in new[] { 1.5, 2.0, 2.5 })
            {
                var strategy = new ATRBreakoutStrategy(period, mult);
                engine.Run(strategy);
                results.Add(AnalyzeStrategy($"ATR({period},{mult:F1}x)", strategy.GetTrades()));
                Console.Write(".");
            }
        }
        Console.WriteLine(" ✓");

        // 9. Z-Score Mean Reversion variations
        Console.WriteLine("📊 Testing Z-Score Mean Reversion...");
        foreach (var period in new[] { 20, 30, 50 })
        {
            foreach (var threshold in new[] { 1.5, 2.0, 2.5 })
            {
                var strategy = new ZScoreStrategy(period, threshold);
                engine.Run(strategy);
                results.Add(AnalyzeStrategy($"ZScore({period},{threshold:F1}σ)", strategy.GetTrades()));
                Console.Write(".");
            }
        }
        Console.WriteLine(" ✓");

        // 10. VWAP
        Console.WriteLine("📊 Testing VWAP (Volume Weighted Average Price)...");
        var vwapStrategy = new VWAPStrategy();
        engine.Run(vwapStrategy);
        results.Add(AnalyzeStrategy("VWAP", vwapStrategy.GetTrades()));
        Console.WriteLine(" ✓");

        Console.WriteLine($"\n✅ Completed {results.Count} strategy variations!");
        
        return results;
    }

    private static StrategyResult AnalyzeStrategy(string name, List<Trade> trades)
    {
        if (trades.Count == 0)
        {
            return new StrategyResult
            {
                StrategyName = name,
                TotalTrades = 0
            };
        }

        var profitableTrades = trades.Count(t => t.ProfitLoss > 0);
        var totalPL = trades.Sum(t => t.ProfitLoss);
        var avgPL = trades.Average(t => t.ProfitLoss);
        var winRate = (profitableTrades * 100.0) / trades.Count;

        // Calculate Sharpe Ratio (simplified - assumes risk-free rate = 0)
        var returns = trades.Select(t => t.ProfitLossPercent).ToList();
        var avgReturn = returns.Average();
        var stdDev = Math.Sqrt(returns.Average(r => Math.Pow(r - avgReturn, 2)));
        var sharpe = stdDev > 0 ? avgReturn / stdDev : 0;

        // Calculate max drawdown
        var equity = 0.0;
        var peak = 0.0;
        var maxDrawdown = 0.0;
        foreach (var trade in trades)
        {
            equity += trade.ProfitLoss;
            peak = Math.Max(peak, equity);
            maxDrawdown = Math.Min(maxDrawdown, equity - peak);
        }

        return new StrategyResult
        {
            StrategyName = name,
            TotalTrades = trades.Count,
            ProfitableTrades = profitableTrades,
            WinRate = winRate,
            TotalPL = totalPL,
            AvgPL = avgPL,
            LargestWin = trades.Max(t => t.ProfitLoss),
            LargestLoss = trades.Min(t => t.ProfitLoss),
            SharpeRatio = sharpe,
            MaxDrawdown = maxDrawdown
        };
    }

    public static void GenerateMasterReport(List<StrategyResult> results, TickEngine engine)
    {
        // Sort by total P&L
        var sorted = results.OrderByDescending(r => r.TotalPL).ToList();

        var report = new StringBuilder();
        
        report.AppendLine("╔═══════════════════════════════════════════════════════════════════╗");
        report.AppendLine("║          FLASHBACK - COMPREHENSIVE STRATEGY ANALYSIS              ║");
        report.AppendLine("╚═══════════════════════════════════════════════════════════════════╝");
        report.AppendLine();
        report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"Dataset: {engine.TickCount:N0} ticks");
        
        var ticks = engine.GetTicks();
        report.AppendLine($"Date Range: {ticks[0].Timestamp:yyyy-MM-dd} to {ticks[^1].Timestamp:yyyy-MM-dd}");
        report.AppendLine();
        
        // Summary statistics
        var profitable = sorted.Count(r => r.TotalPL > 0);
        var unprofitable = sorted.Count(r => r.TotalPL <= 0);
        
        report.AppendLine("═══════════════════════════════════════════════════════════════════");
        report.AppendLine("OVERALL SUMMARY");
        report.AppendLine("═══════════════════════════════════════════════════════════════════");
        report.AppendLine($"  Total Strategies Tested: {results.Count}");
        report.AppendLine($"  Profitable: {profitable} ({profitable * 100.0 / results.Count:F1}%)");
        report.AppendLine($"  Unprofitable: {unprofitable} ({unprofitable * 100.0 / results.Count:F1}%)");
        report.AppendLine();

        // Top 10 Winners
        report.AppendLine("═══════════════════════════════════════════════════════════════════");
        report.AppendLine("TOP 10 BEST PERFORMERS");
        report.AppendLine("═══════════════════════════════════════════════════════════════════");
        report.AppendLine();
        report.AppendLine(string.Format("{0,-25} {1,8} {2,12} {3,10} {4,10}",
            "Strategy", "Win%", "Total P&L", "Sharpe", "Trades"));
        report.AppendLine(new string('─', 70));
        
        foreach (var result in sorted.Take(10))
        {
            report.AppendLine(string.Format("{0,-25} {1,7:F1}% ${2,10:N0} {3,9:F2} {4,10:N0}",
                result.StrategyName,
                result.WinRate,
                result.TotalPL,
                result.SharpeRatio,
                result.TotalTrades));
        }
        report.AppendLine();

        // Bottom 10 Losers
        report.AppendLine("═══════════════════════════════════════════════════════════════════");
        report.AppendLine("WORST 10 PERFORMERS");
        report.AppendLine("═══════════════════════════════════════════════════════════════════");
        report.AppendLine();
        report.AppendLine(string.Format("{0,-25} {1,8} {2,12} {3,10} {4,10}",
            "Strategy", "Win%", "Total P&L", "Sharpe", "Trades"));
        report.AppendLine(new string('─', 70));
        
        foreach (var result in sorted.TakeLast(10).Reverse())
        {
            report.AppendLine(string.Format("{0,-25} {1,7:F1}% ${2,10:N0} {3,9:F2} {4,10:N0}",
                result.StrategyName,
                result.WinRate,
                result.TotalPL,
                result.SharpeRatio,
                result.TotalTrades));
        }
        report.AppendLine();

        // Key insights
        report.AppendLine("═══════════════════════════════════════════════════════════════════");
        report.AppendLine("KEY FINDINGS");
        report.AppendLine("═══════════════════════════════════════════════════════════════════");
        
        var bestStrategy = sorted.First();
        var worstStrategy = sorted.Last();
        
        report.AppendLine($"✅ Best Strategy: {bestStrategy.StrategyName}");
        report.AppendLine($"   P&L: ${bestStrategy.TotalPL:N2} | Win Rate: {bestStrategy.WinRate:F1}% | Sharpe: {bestStrategy.SharpeRatio:F2}");
        report.AppendLine();
        report.AppendLine($"❌ Worst Strategy: {worstStrategy.StrategyName}");
        report.AppendLine($"   P&L: ${worstStrategy.TotalPL:N2} | Win Rate: {worstStrategy.WinRate:F1}%");
        report.AppendLine();
        report.AppendLine($"📊 {unprofitable}/{results.Count} ({unprofitable * 100.0 / results.Count:F0}%) strategies lost money");
        report.AppendLine();
        report.AppendLine("═══════════════════════════════════════════════════════════════════");

        // Save to file
        Directory.CreateDirectory("results");
        var filename = $"results/master_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        File.WriteAllText(filename, report.ToString());

        // Also print to console
        Console.WriteLine("\n" + report.ToString());
        Console.WriteLine($"\n💾 Master report saved: {filename}");
    }

    public static void ExportToCSV(List<StrategyResult> results)
    {
        Directory.CreateDirectory("results");
        var filename = $"results/all_strategies_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        var csv = new StringBuilder();
        csv.AppendLine("Strategy,Total Trades,Profitable,Win Rate %,Total P&L,Avg P&L,Largest Win,Largest Loss,Sharpe Ratio,Max Drawdown");

        foreach (var result in results.OrderByDescending(r => r.TotalPL))
        {
            csv.AppendLine($"{result.StrategyName},{result.TotalTrades},{result.ProfitableTrades}," +
                          $"{result.WinRate:F2},{result.TotalPL:F2},{result.AvgPL:F2}," +
                          $"{result.LargestWin:F2},{result.LargestLoss:F2}," +
                          $"{result.SharpeRatio:F2},{result.MaxDrawdown:F2}");
        }

        File.WriteAllText(filename, csv.ToString());
        Console.WriteLine($"📊 CSV exported: {filename}");
    }
}
