using System.Diagnostics;
using Akka.Actor;
using BenchmarkDotNet.Attributes;
using InterleavedCalc.Types.Leibniz;

namespace InterleavedCalc.Benchmarks;

[MemoryDiagnoser]
public class PiCalculationBenchmark
{
    // Number of iterations each calculation performs.
    [Params(10_000_000L)] public long TargetIterations { get; set; }

    // For interleaved calculations.
    [Params(10_000L)] public long BatchSize { get; set; }
    
    [Params(1_000L)] public int Parallelism { get; set; }

    // When true, each actor will perform a heartbeat check before starting.
    // [Params(false, true)] public bool WithHeartbeat { get; set; }

    [Benchmark(Baseline = true, Description = "Synchronous Calculation")]
    public async Task SynchronousCalculationBenchmark()
    {
        using var system = ActorSystem.Create("SyncSystem");
        var tasks = new List<Task<double>>(Parallelism);

        for (int i = 0; i < Parallelism; i++)
        {
            var actor = system.ActorOf(
                Props.Create(() => new SynchronousPiCalculatorActor()),
                $"sync-{i}");

            // if (WithHeartbeat)
            // {
            //     var identity = await actor.Ask<ActorIdentity>(
            //         new Identify(null),
            //         TimeSpan.FromSeconds(5));
            //     if (identity.ActorRef == null)
            //         throw new Exception("Actor did not respond to heartbeat");
            // }

            tasks.Add(actor.Ask<double>(
                new StartCalculation(TargetIterations, BatchSize),
                TimeSpan.FromSeconds(120)));
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark(Description = "Interleaved Calculation")]
    public async Task InterleavedCalculationBenchmark()
    {
        using var system = ActorSystem.Create("InterleavedSystem");
        var tasks = new List<Task<double>>(Parallelism);

        for (int i = 0; i < Parallelism; i++)
        {
            var actor = system.ActorOf(
                Props.Create(() => new InterleavedPiCalculatorActor()),
                $"interleaved-{i}");

            // if (WithHeartbeat)
            // {
            //     var identity = await actor.Ask<ActorIdentity>(
            //         new Identify(null),
            //         TimeSpan.FromSeconds(5));
            //     if (identity.ActorRef == null)
            //         throw new Exception("Actor did not respond to heartbeat");
            // }

            tasks.Add(actor.Ask<double>(
                new StartCalculation(TargetIterations, BatchSize),
                TimeSpan.FromSeconds(120)));
        }

        await Task.WhenAll(tasks);
    }
}