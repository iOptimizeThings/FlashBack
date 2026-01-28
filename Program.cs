using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using nietras.SeparatedValues;

namespace FlashBack;

/// <summary>
/// Core tick data structure - MUST be readonly struct for cache locality
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Tick
{
    public readonly long TimestampTicks;
    public readonly double Price;
    public readonly long Volume;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tick(long timestampTicks, double price, long volume)
    {
        TimestampTicks = timestampTicks;
        Price = price;
        Volume = volume;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tick(DateTime timestamp, double price, long volume)
    {
        TimestampTicks = timestamp.Ticks;
        Price = price;
        Volume = volume;
    }

    public DateTime Timestamp => new(TimestampTicks);

    public override string ToString() =>
        $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} | Price: {Price:F2} | Vol: {Volume}";
}

/// <summary>
/// Strategy interface - implement OnTick for your trading logic
/// </summary>
public interface IStrategy
{
    void OnTick(in Tick tick, int index);
    void OnComplete();
    List<Trade> GetTrades();
    string GetStats();
}

/// <summary>
/// Represents a completed trade
/// </summary>
public struct Trade
{
    public DateTime EntryTime;
    public double EntryPrice;
    public DateTime ExitTime;
    public double ExitPrice;
    public double ProfitLoss;
    public double ProfitLossPercent;
}

/// <summary>
/// The core backtesting engine - optimized for raw speed
/// </summary>
public class TickEngine
{
    private Tick[] _ticks;
    private int _tickCount;

    public TickEngine(int initialCapacity = 10_000_000)
    {
        _ticks = new Tick[initialCapacity];
        _tickCount = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddTick(in Tick tick)
    {
        if (_tickCount >= _ticks.Length)
        {
            Array.Resize(ref _ticks, _ticks.Length * 2);
        }
        _ticks[_tickCount++] = tick;
    }

    public void Run(IStrategy strategy)
    {
        var span = new ReadOnlySpan<Tick>(_ticks, 0, _tickCount);
        
        for (int i = 0; i < span.Length; i++)
        {
            strategy.OnTick(in span[i], i);
        }
        
        strategy.OnComplete();
    }

    public int TickCount => _tickCount;

    public ReadOnlySpan<Tick> GetTicks() => new(_ticks, 0, _tickCount);
}

/// <summary>
/// High-performance CSV loader using Sep library
/// </summary>
public static class DataLoader
{
    public static TickEngine LoadBitcoinKaggleCsv(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        Console.WriteLine($"🔍 Analyzing CSV format...");
        var engine = new TickEngine();
        var sw = Stopwatch.StartNew();

        using var reader = Sep.Reader().FromFile(filePath);
        
        int rowCount = 0;
        int skippedRows = 0;
        string detectedFormat = "Unknown";
        bool formatDetected = false;
        
        foreach (var row in reader)
        {
            if (!formatDetected)
            {
                var headers = reader.Header.ColNames;
                Console.WriteLine($"📋 CSV Headers: {string.Join(", ", headers)}");
                
                var firstCol = row[0].ToString();
                
                if (firstCol.Contains('.') && double.TryParse(firstCol, out double unixFloat))
                {
                    detectedFormat = "Unix Timestamp (Float)";
                    Console.WriteLine($"✓ Detected Format: {detectedFormat}");
                }
                else if (long.TryParse(firstCol, out long unixLong))
                {
                    detectedFormat = "Unix Timestamp (Integer)";
                    Console.WriteLine($"✓ Detected Format: {detectedFormat}");
                }
                else if (DateTime.TryParse(firstCol, out _))
                {
                    detectedFormat = "ISO DateTime String";
                    Console.WriteLine($"✓ Detected Format: {detectedFormat}");
                }
                else
                {
                    detectedFormat = "Date/Time Separate Columns";
                    Console.WriteLine($"✓ Detected Format: {detectedFormat}");
                }
                
                Console.WriteLine($"\n🔄 Loading data...\n");
                formatDetected = true;
            }
            
            try
            {
                DateTime timestamp;
                double close;
                long volume;
                
                var firstValue = row[0].ToString();
                
                if (firstValue.Contains('.') && double.TryParse(firstValue, out double unixFloat))
                {
                    timestamp = DateTimeOffset.FromUnixTimeSeconds((long)unixFloat).UtcDateTime;
                }
                else if (long.TryParse(firstValue, out long unixLong))
                {
                    timestamp = DateTimeOffset.FromUnixTimeSeconds(unixLong).UtcDateTime;
                }
                else if (DateTime.TryParse(firstValue, out DateTime parsedDate))
                {
                    timestamp = parsedDate;
                }
                else
                {
                    var dateStr = row[0].ToString();
                    var timeStr = row[1].ToString();
                    timestamp = DateTime.Parse($"{dateStr} {timeStr}", CultureInfo.InvariantCulture);
                }
                
                if (reader.Header.ColNames.Count >= 5)
                {
                    close = row[4].Parse<double>();
                }
                else
                {
                    close = row[1].Parse<double>();
                }
                
                if (reader.Header.ColNames.Count >= 6)
                {
                    var volumeRaw = row[5].Parse<double>();
                    volume = (long)(volumeRaw * 100_000_000);
                }
                else
                {
                    volume = 0;
                }
                
                if (close <= 0 || double.IsNaN(close) || double.IsInfinity(close))
                {
                    skippedRows++;
                    continue;
                }
                
                engine.AddTick(new Tick(timestamp, close, volume));
                rowCount++;
                
                if (rowCount % 1_000_000 == 0)
                {
                    Console.WriteLine($"  ✓ Loaded {rowCount:N0} rows... ({sw.Elapsed.TotalSeconds:F1}s)");
                }
            }
            catch
            {
                skippedRows++;
            }
        }

        sw.Stop();
        
        Console.WriteLine($"\n{'═',60}");
        Console.WriteLine($"✅ CSV Loading Complete");
        Console.WriteLine($"{'═',60}");
        Console.WriteLine($"  Format: {detectedFormat}");
        Console.WriteLine($"  Loaded: {rowCount:N0} ticks");
        Console.WriteLine($"  Skipped: {skippedRows:N0} rows");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"  Speed: {rowCount / sw.Elapsed.TotalSeconds:N0} rows/sec");
        
        if (rowCount > 0)
        {
            var firstTick = engine.GetTicks()[0];
            var lastTick = engine.GetTicks()[^1];
            Console.WriteLine($"  Date Range: {firstTick.Timestamp:yyyy-MM-dd} to {lastTick.Timestamp:yyyy-MM-dd}");
            Console.WriteLine($"  Price Range: ${firstTick.Price:N2} to ${lastTick.Price:N2}");
        }
        Console.WriteLine($"{'═',60}\n");
        
        if (rowCount == 0)
        {
            throw new Exception($"No valid data loaded! All {skippedRows:N0} rows were skipped.");
        }
        
        return engine;
    }
}

/// <summary>
/// Main program entry point
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════╗
║   ⚡ FlashBack - High-Performance Backtesting Engine ⚡   ║
║              .NET 8.0 | C# | Bitcoin | HFT                ║
╚═══════════════════════════════════════════════════════════╝
");

        try
        {
            ShowMenu();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }

    static void ShowMenu()
    {
        Console.WriteLine("\nWelcome to FlashBack Backtesting Engine\n");
        Console.WriteLine("Options:");
        Console.WriteLine("  1) Run ALL 10 strategies with parameter grid (~60 tests)");
        Console.WriteLine("  2) Run single SMA strategy (quick test)");
        Console.WriteLine("  3) Exit");
        Console.Write("\nChoose (1-3): ");
        
        var choice = Console.ReadLine()?.Trim();
        
        if (choice == "3" || choice?.ToLower() == "exit")
        {
            Console.WriteLine("\nGoodbye! 👋");
            Environment.Exit(0);
        }
        
        Console.Write("\nEnter CSV filename: ");
        var filename = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrWhiteSpace(filename))
        {
            Console.WriteLine("\n❌ No filename provided!");
            ShowMenu();
            return;
        }
        
        if (!File.Exists(filename))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ File not found: {filename}");
            Console.WriteLine($"\nLooking in: {Directory.GetCurrentDirectory()}");
            Console.ResetColor();
            Console.WriteLine("\nPress any key to try again...");
            Console.ReadKey();
            ShowMenu();
            return;
        }
        
        if (choice == "1")
        {
            RunComprehensiveBacktest(filename);
        }
        else
        {
            RunSingleSMABacktest(filename);
        }
        
        Console.WriteLine("\n✅ Backtesting complete!");
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
    
    static void RunComprehensiveBacktest(string csvFilePath)
    {
        Console.WriteLine($"\n🔄 Loading: {csvFilePath}\n");
        
        var engine = DataLoader.LoadBitcoinKaggleCsv(csvFilePath);
        
        Console.WriteLine($"\n📊 Dataset Info:");
        Console.WriteLine($"  Total ticks: {engine.TickCount:N0}");
        
        var ticks = engine.GetTicks();
        Console.WriteLine($"  Date Range: {ticks[0].Timestamp:yyyy-MM-dd} to {ticks[^1].Timestamp:yyyy-MM-dd}");
        Console.WriteLine($"  Price Range: ${ticks[0].Price:N2} to ${ticks[^1].Price:N2}");
        
        var results = StrategyRunner.RunAllStrategies(engine);
        
        StrategyRunner.GenerateMasterReport(results, engine);
        StrategyRunner.ExportToCSV(results);
    }
    
    static void RunSingleSMABacktest(string csvFilePath)
    {
        Console.WriteLine($"\n🔄 Loading: {csvFilePath}\n");
        
        var engine = DataLoader.LoadBitcoinKaggleCsv(csvFilePath);
        
        Console.WriteLine($"\n📊 Dataset Info:");
        Console.WriteLine($"  Total ticks: {engine.TickCount:N0}");
        
        var ticks = engine.GetTicks();
        var firstPrice = ticks[0].Price;
        var lastPrice = ticks[^1].Price;
        var priceChange = ((lastPrice - firstPrice) / firstPrice) * 100;
        
        Console.WriteLine($"  First price: ${firstPrice:N2}");
        Console.WriteLine($"  Last price: ${lastPrice:N2}");
        Console.WriteLine($"  Total change: {priceChange:+0.00;-0.00}%");
        
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("Configure Your Strategy");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine("\nSimple Moving Average (SMA):");
        Console.WriteLine("  Common periods: 20 (short), 50 (medium), 200 (long)\n");
        
        Console.Write("SMA period: ");
        var periodInput = Console.ReadLine();
        
        if (!int.TryParse(periodInput, out int period) || period < 2)
        {
            Console.WriteLine("⚠️  Invalid input, using 50");
            period = 50;
        }
        
        Console.WriteLine($"\nRunning SMA({period})...\n");
        
        var sw = Stopwatch.StartNew();
        var strategy = new SimpleMovingAverageStrategy(period);
        engine.Run(strategy);
        sw.Stop();
        
        Console.WriteLine(strategy.GetStats());
        Console.WriteLine($"\nExecution Time: {sw.ElapsedMilliseconds}ms");
        
        SaveSingleStrategyReport(csvFilePath, period, strategy, engine);
    }
    
    static void SaveSingleStrategyReport(string csvPath, int period, SimpleMovingAverageStrategy strategy, TickEngine engine)
    {
        try
        {
            Directory.CreateDirectory("results");
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var reportPath = $"results/backtest_sma{period}_{timestamp}.txt";
            
            var ticks = engine.GetTicks();
            var trades = strategy.GetTrades();
            
            var report = new StringBuilder();
            report.AppendLine("╔════════════════════════════════════════════════════════════╗");
            report.AppendLine("║           FlashBack Backtest Report                        ║");
            report.AppendLine("╚════════════════════════════════════════════════════════════╝");
            report.AppendLine();
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Data Source: {Path.GetFileName(csvPath)}");
            report.AppendLine($"Strategy: SMA({period})");
            report.AppendLine();
            report.AppendLine(strategy.GetStats());
            report.AppendLine();
            
            if (trades.Count > 0)
            {
                report.AppendLine("─────────────────────────────────────────────────────────────");
                report.AppendLine("Sample Trades (First 20):");
                report.AppendLine("─────────────────────────────────────────────────────────────");
                
                foreach (var t in trades.Take(20))
                {
                    report.AppendLine($"Trade:");
                    report.AppendLine($"  Entry: {t.EntryTime:yyyy-MM-dd HH:mm} @ ${t.EntryPrice:F2}");
                    report.AppendLine($"  Exit:  {t.ExitTime:yyyy-MM-dd HH:mm} @ ${t.ExitPrice:F2}");
                    report.AppendLine($"  P&L:   ${t.ProfitLoss:+0.00;-0.00}");
                    report.AppendLine();
                }
            }
            
            File.WriteAllText(reportPath, report.ToString());
            Console.WriteLine($"\n💾 Report saved: {reportPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n⚠️  Could not save report: {ex.Message}");
        }
    }
}