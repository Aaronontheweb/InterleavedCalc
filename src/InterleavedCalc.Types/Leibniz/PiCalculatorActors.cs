using Akka.Event;

namespace InterleavedCalc.Types.Leibniz;

using Akka.Actor;

public record StartCalculation(long TargetIterations, long BatchSize);

public class ContinueCalculation
{
    public static readonly ContinueCalculation Instance = new();
    private ContinueCalculation(){}
}

public class SynchronousPiCalculatorActor : UntypedActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    protected override void OnReceive(object message)
    {
        if (message is StartCalculation start)
        {
            _log.Info("Starting calculation");
            var pi = PiCalculator.CalculatePi(start.TargetIterations);
            _log.Info("Calculated Pi: {0}", pi);
            Sender.Tell(pi);
        }
    }
}


public class InterleavedPiCalculatorActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    
    // State for our calculation
    private long _targetIterations;
    private long _currentIteration;
    private long _batchSize;
    private double _sum;
    private double _sign;

    private IActorRef? _requestor = null;

    public InterleavedPiCalculatorActor()
    {
        // Initialize the state
        _sum = 0.0;
        _sign = 1.0;
        _currentIteration = 0;

        WaitingForWork();
    }

    private void WaitingForWork()
    {
        Receive<StartCalculation>(msg =>
        {
            _targetIterations = msg.TargetIterations;
            _batchSize = msg.BatchSize;
            _requestor = Sender;
            Self.Tell(ContinueCalculation.Instance);
            Become(Working);
        });
    }

    private void Working()
    {
        Receive<ContinueCalculation>(_ => ProcessBatch());
    }

    private void ProcessBatch()
    {
        long iterationsProcessed = 0;
        while (_currentIteration < _targetIterations && iterationsProcessed < _batchSize)
        {
            _sum += _sign / (2 * _currentIteration + 1);
            _sign = -_sign; // alternate the sign for the series
            _currentIteration++;
            iterationsProcessed++;
        }

        if (_currentIteration < _targetIterations)
        {
            // Schedule the next batch; this yields control to process other messages.
            Self.Tell(ContinueCalculation.Instance);
        }
        else
        {
            // Calculation complete; compute π from the accumulated sum.
            double pi = 4 * _sum;
            _log.Info("Calculated Pi: {0}", pi);

            // Deliver results to original requestor
            _requestor.Tell(pi);
        }
    }
}
