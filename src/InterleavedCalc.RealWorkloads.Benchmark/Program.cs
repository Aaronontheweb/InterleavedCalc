using System.Diagnostics;
using Akka.Actor;
using InterleavedCalc.Types.Leibniz;

namespace InterleavedCalc.RealWorkloads.Benchmark;

public class Program
{
    // Benchmark configuration parameters.
    const int ParallelCalculations = 1000;
    const long TargetIterations = 100_000_000L;
    const long BatchSize = 1_000L;
    const bool EnableHeartbeat = true; // Set to false to disable heartbeat checks.
    static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(5);

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting benchmark comparing Synchronous and Interleaved actors");
        Console.WriteLine($"Parallel calculations: {ParallelCalculations}");
        Console.WriteLine($"Target Iterations: {TargetIterations}");
        Console.WriteLine($"Batch Size: {BatchSize}");
        Console.WriteLine($"Heartbeat enabled: {EnableHeartbeat}");
        Console.WriteLine();

        // Run synchronous benchmark.
        BenchmarkResult syncResult = await RunBenchmark("sync");

        // Run interleaved benchmark.
        BenchmarkResult interleavedResult = await RunBenchmark("interleaved");

        // Final report.
        Console.WriteLine("Benchmark Results:");
        PrintResult(syncResult);
        Console.WriteLine();
        PrintResult(interleavedResult);
        Console.WriteLine();
        Console.WriteLine("Comparison Summary:");
        Console.WriteLine($"Synchronous Total Runtime:   {syncResult.TotalRuntimeMs:F2} ms");
        Console.WriteLine($"Interleaved Total Runtime:   {interleavedResult.TotalRuntimeMs:F2} ms");
    }

    private static async Task<BenchmarkResult> RunBenchmark(string calculationType)
    {
        Console.WriteLine($"Running {calculationType} benchmark...");
        string systemName = calculationType == "sync" ? "SyncSystem" : "InterleavedSystem";
        using var system = ActorSystem.Create(systemName);

        var calcLatencies = new List<double>(ParallelCalculations);
        var heartbeatLatencies = new List<double>();
        int heartbeatTimeoutCount = 0;
        var tasks = new List<Task>(ParallelCalculations);
        var calcStopwatches = new Stopwatch[ParallelCalculations];

        for (int i = 0; i < ParallelCalculations; i++)
        {
            IActorRef actor = calculationType == "sync"
                ? system.ActorOf(Props.Create(() => new SynchronousPiCalculatorActor()), $"sync-{i}")
                : system.ActorOf(Props.Create(() => new InterleavedPiCalculatorActor()), $"interleaved-{i}");

            // Launch heartbeat check if enabled.
            Task heartbeatTask = Task.CompletedTask;
            if (EnableHeartbeat)
            {
                async Task Cont()
                {
                    var hbStopwatch = Stopwatch.StartNew();
                    try
                    {
                        var hbAsk = await actor.Ask<ActorIdentity>(new Identify(null), HeartbeatTimeout);
                        hbStopwatch.Stop();
                        lock (heartbeatLatencies)
                        {
                            heartbeatLatencies.Add(hbStopwatch.Elapsed.TotalMilliseconds);
                        }
                    }
                    catch (Exception)
                    {
                        System.Threading.Interlocked.Increment(ref heartbeatTimeoutCount);
                    }
                }

                heartbeatTask = Cont();

            }

            // Start the calculation and measure its latency.
            var calcStopwatch = Stopwatch.StartNew();
            calcStopwatches[i] = calcStopwatch;
            var calcTask = actor
                .Ask<double>(new StartCalculation(TargetIterations, BatchSize), TimeSpan.FromSeconds(120))
                .ContinueWith(t =>
                {
                    calcStopwatch.Stop();
                    lock (calcLatencies)
                    {
                        if (t.IsCompletedSuccessfully)
                            calcLatencies.Add(calcStopwatch.Elapsed.TotalMilliseconds);
                        else
                            calcLatencies.Add(double.NaN);
                    }
                });

            tasks.Add(Task.WhenAll(heartbeatTask, calcTask));
        }

        var totalStopwatch = Stopwatch.StartNew();
        await Task.WhenAll(tasks);
        totalStopwatch.Stop();

        // Calculate statistics for calculation latencies.
        var validCalcLatencies = calcLatencies.Where(x => !double.IsNaN(x)).ToList();
        double calcMin = validCalcLatencies.Min();
        double calcMax = validCalcLatencies.Max();
        double calcMean = validCalcLatencies.Average();
        double calcMedian = CalculateMedian(validCalcLatencies);

        double? hbMin = null, hbMax = null, hbMean = null, hbMedian = null;
        if (EnableHeartbeat && heartbeatLatencies.Count > 0)
        {
            hbMin = heartbeatLatencies.Min();
            hbMax = heartbeatLatencies.Max();
            hbMean = heartbeatLatencies.Average();
            hbMedian = CalculateMedian(heartbeatLatencies);
        }

        await system.Terminate();

        return new BenchmarkResult
        {
            CalculationType = calculationType,
            TotalRuntimeMs = totalStopwatch.Elapsed.TotalMilliseconds,
            CalcMinMs = calcMin,
            CalcMaxMs = calcMax,
            CalcMeanMs = calcMean,
            CalcMedianMs = calcMedian,
            HeartbeatMinMs = hbMin,
            HeartbeatMaxMs = hbMax,
            HeartbeatMeanMs = hbMean,
            HeartbeatMedianMs = hbMedian,
            HeartbeatTimeoutCount = heartbeatTimeoutCount
        };
    }

    private static double CalculateMedian(List<double> numbers)
    {
        var sorted = numbers.OrderBy(x => x).ToList();
        int count = sorted.Count;
        if (count == 0)
            return 0;
        if (count % 2 == 0)
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        else
            return sorted[count / 2];
    }

    private static void PrintResult(BenchmarkResult result)
    {
        string label = result.CalculationType == "sync" ? "Synchronous" : "Interleaved";
        Console.WriteLine($"{label} Benchmark:");
        Console.WriteLine($"Total Runtime: {result.TotalRuntimeMs:F2} ms");
        Console.WriteLine("Calculation Latencies (ms):");
        Console.WriteLine($"  Min:    {result.CalcMinMs:F2}");
        Console.WriteLine($"  Mean:   {result.CalcMeanMs:F2}");
        Console.WriteLine($"  Median: {result.CalcMedianMs:F2}");
        Console.WriteLine($"  Max:    {result.CalcMaxMs:F2}");
        if (EnableHeartbeat)
        {
            Console.WriteLine("Heartbeat Latencies (ms):");
            Console.WriteLine($"  Min:    {result.HeartbeatMinMs:F2}");
            Console.WriteLine($"  Mean:   {result.HeartbeatMeanMs:F2}");
            Console.WriteLine($"  Median: {result.HeartbeatMedianMs:F2}");
            Console.WriteLine($"  Max:    {result.HeartbeatMaxMs:F2}");
            Console.WriteLine($"Heartbeat timeouts: {result.HeartbeatTimeoutCount}");
        }
    }
}