namespace InterleavedCalc.RealWorkloads.Benchmark;

public class BenchmarkResult
{
    public string CalculationType { get; set; } = string.Empty;
    public double TotalRuntimeMs { get; set; }
    public double CalcMinMs { get; set; }
    public double CalcMeanMs { get; set; }
    public double CalcMedianMs { get; set; }
    public double CalcMaxMs { get; set; }
    public double? HeartbeatMinMs { get; set; }
    public double? HeartbeatMeanMs { get; set; }
    public double? HeartbeatMedianMs { get; set; }
    public double? HeartbeatMaxMs { get; set; }
    public int HeartbeatTimeoutCount { get; set; }
}