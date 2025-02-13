using System.Diagnostics;
using Akka.Actor;
using InterleavedCalc.Types.Leibniz;

namespace InterleavedCalc.RealWorkloads.Benchmark;

public class Program
{
    // Configuration parameters.
    const int ParallelCalculations = 1000;
    const long TargetIterations = 1_000_000L;
    const long BatchSize = 10_000L;
        
    // Choose the type of calculation: "sync" or "interleaved"
    static string CalculationType = "interleaved"; // change to "sync" for synchronous actor

    // Heartbeat configuration.
    public static readonly bool EnableHeartbeat = true;
    public static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(5);

    public static async Task Main(string[] args)
    {
        Console.WriteLine($"Starting benchmark: {CalculationType} calculations");
        Console.WriteLine($"Parallel jobs: {ParallelCalculations}, TargetIterations: {TargetIterations}, BatchSize: {BatchSize}");
        Console.WriteLine($"Heartbeat enabled: {EnableHeartbeat}");
        Console.WriteLine();

        // Create an ActorSystem. Using different system names for clarity.
        var systemName = CalculationType == "sync" ? "SyncSystem" : "InterleavedSystem";
        using var system = ActorSystem.Create(systemName);

        // Lists to collect latency data.
        var calcLatenciesMs = new List<double>(ParallelCalculations);
        var heartbeatLatenciesMs = new List<double>();
        int heartbeatTimeoutCount = 0;

        // Create tasks for each calculation.
        var tasks = new List<Task>(ParallelCalculations);
        var calcStopwatches = new Stopwatch[ParallelCalculations];

        // Kick off all operations immediately.
        for (int i = 0; i < ParallelCalculations; i++)
        {
            // Create an actor based on the selected calculation type.
            IActorRef actor;
            if (CalculationType == "sync")
            {
                actor = system.ActorOf(Props.Create(() => new SynchronousPiCalculatorActor()), $"sync-{i}");
            }
            else // interleaved
            {
                actor = system.ActorOf(Props.Create(() => new InterleavedPiCalculatorActor()), $"interleaved-{i}");
            }

            // If heartbeat is enabled, fire off the Identify message and measure its latency.
            Task heartbeatTask = Task.CompletedTask;
            if (EnableHeartbeat)
            {
                
                var heartbeatAsk = actor.Ask<ActorIdentity>(new Identify(null), HeartbeatTimeout);
                async Task Cont()
                {
                    try
                    {
                        var heartbeatStopwatch = Stopwatch.StartNew();
                        await heartbeatAsk;
                        heartbeatStopwatch.Stop();
                        lock (heartbeatLatenciesMs)
                        {
                            heartbeatLatenciesMs.Add(heartbeatStopwatch.Elapsed.TotalMilliseconds);
                        }
                    }
                    catch (Exception)
                    {
                        // Heartbeat failed.
                        System.Threading.Interlocked.Increment(ref heartbeatTimeoutCount);
                    }
                }

                heartbeatTask = Cont();
            }

            // Start the calculation and measure its latency.
            var calcStopwatch = Stopwatch.StartNew();
            calcStopwatches[i] = calcStopwatch;
            var calcTask = actor.Ask<double>(new StartCalculation(TargetIterations, BatchSize), TimeSpan.FromSeconds(120))
                .ContinueWith(t =>
                {
                    calcStopwatch.Stop();
                    // Record only if the calculation completed successfully.
                    if (t.IsCompletedSuccessfully)
                    {
                        lock (calcLatenciesMs)
                        {
                            calcLatenciesMs.Add(calcStopwatch.Elapsed.TotalMilliseconds);
                        }
                    }
                    else
                    {
                        // Mark as NaN in case of failure.
                        lock (calcLatenciesMs)
                        {
                            calcLatenciesMs.Add(double.NaN);
                        }
                    }
                });

            // Combine both heartbeat and calculation tasks.
            tasks.Add(Task.WhenAll(heartbeatTask, calcTask));
        }

        // Start total job runtime stopwatch.
        var totalStopwatch = Stopwatch.StartNew();
        await Task.WhenAll(tasks);
        totalStopwatch.Stop();

        // Compute statistics for calculations.
        var validCalcLatencies = calcLatenciesMs.Where(x => !double.IsNaN(x)).ToList();
        double calcMin = validCalcLatencies.Min();
        double calcMax = validCalcLatencies.Max();
        double calcMean = validCalcLatencies.Average();
        double calcMedian = CalculateMedian(validCalcLatencies);

        Console.WriteLine("Calculation Latencies (ms):");
        Console.WriteLine($"Min: {calcMin:F2}");
        Console.WriteLine($"Mean: {calcMean:F2}");
        Console.WriteLine($"Median: {calcMedian:F2}");
        Console.WriteLine($"Max: {calcMax:F2}");
        Console.WriteLine();
        Console.WriteLine($"Total job runtime: {totalStopwatch.Elapsed.TotalMilliseconds:F2} ms");

        // If heartbeat is enabled, compute heartbeat statistics.
        if (EnableHeartbeat)
        {
            var validHeartbeatLatencies = heartbeatLatenciesMs.Where(x => !double.IsNaN(x)).ToList();
            double hbMean = validHeartbeatLatencies.Count > 0 ? validHeartbeatLatencies.Average() : 0;
            double hbMin = validHeartbeatLatencies.Count > 0 ? validHeartbeatLatencies.Min() : 0;
            double hbMax = validHeartbeatLatencies.Count > 0 ? validHeartbeatLatencies.Max() : 0;
            double hbMedian = validHeartbeatLatencies.Count > 0 ? CalculateMedian(validHeartbeatLatencies) : 0;

            Console.WriteLine();
            Console.WriteLine("Heartbeat Latencies (ms):");
            Console.WriteLine($"Min: {hbMin:F2}");
            Console.WriteLine($"Mean: {hbMean:F2}");
            Console.WriteLine($"Median: {hbMedian:F2}");
            Console.WriteLine($"Max: {hbMax:F2}");
            Console.WriteLine($"Heartbeat timeouts: {heartbeatTimeoutCount}");
        }
    }

    // Helper method to compute the median.
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
}