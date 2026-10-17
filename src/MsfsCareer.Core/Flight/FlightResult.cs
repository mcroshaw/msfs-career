namespace MsfsCareer.Core.Flight;

public sealed record GeoPoint(double LatitudeDeg, double LongitudeDeg);

public sealed record Landing(DateTimeOffset Time, GeoPoint Position, double RateFpm, double GForce, int Bounces);

/// <summary>Everything the career rules need to judge a completed flight.</summary>
public sealed record FlightResult
{
    public required string AircraftTitle { get; init; }
    public required GeoPoint Departure { get; init; }
    public required GeoPoint Arrival { get; init; }
    public required DateTimeOffset BlockOut { get; init; }
    public required DateTimeOffset Takeoff { get; init; }
    public required DateTimeOffset BlockIn { get; init; }

    /// <summary>The final landing of the flight (touch-and-goes before it are counted in <see cref="TouchAndGoes"/>).</summary>
    public required Landing Landing { get; init; }

    public int TouchAndGoes { get; init; }
    public double FuelUsedLbs { get; init; }
    public double DistanceFlownNm { get; init; }
    public double MaxGForce { get; init; }
    public double MinGForce { get; init; }
    public double MaxBankDeg { get; init; }
    public double MaxAltitudeFt { get; init; }
    public TimeSpan PausedTime { get; init; }
    public TimeSpan PausedAirborneTime { get; init; }

    /// <summary>Block time with time spent paused removed.</summary>
    public TimeSpan BlockTime => BlockIn - BlockOut - PausedTime;

    /// <summary>Takeoff to final touchdown, with time spent paused in the air removed.</summary>
    public TimeSpan AirTime => Landing.Time - Takeoff - PausedAirborneTime;
}
