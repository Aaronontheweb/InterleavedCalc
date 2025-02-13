namespace InterleavedCalc.Types.Leibniz;

public static class PiCalculator
{
    public static double CalculatePi(long iterations)
    {
        var sum = 0.0;
        var sign = 1.0;

        for (long i = 0; i < iterations; i++)
        {
            sum += sign / (2 * i + 1);
            sign = -sign; // alternate the sign for the series
        }

        return 4 * sum;
    }
}
