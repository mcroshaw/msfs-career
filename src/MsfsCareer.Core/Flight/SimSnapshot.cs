namespace MsfsCareer.Core.Flight;

/// <summary>
/// One sample of the user's aircraft state, as read from the sim.
/// Units follow the SimConnect variables they come from.
/// </summary>
public sealed record SimSnapshot
{
    public required DateTimeOffset Time { get; init; }
    public required double LatitudeDeg { get; init; }
    public required double LongitudeDeg { get; init; }
    public double AltitudeFt { get; init; }
    public double AltitudeAglFt { get; init; }
    public required double GroundSpeedKt { get; init; }
    public double IndicatedAirspeedKt { get; init; }
    public double VerticalSpeedFpm { get; init; }
    public double HeadingTrueDeg { get; init; }
    public double BankDeg { get; init; }
    public double PitchDeg { get; init; }
    public double GForce { get; init; } = 1.0;
    public required bool OnGround { get; init; }
    public bool EngineRunning { get; init; }
    public bool ParkingBrakeSet { get; init; }
    public double FuelTotalLbs { get; init; }

    /// <summary>
    /// Vertical speed at the last touchdown, in feet per minute (positive = descending).
    /// The sim latches this at the moment of contact, so it is more accurate than sampled vertical speed.
    /// </summary>
    public double? TouchdownRateFpm { get; init; }

    public bool Paused { get; init; }
    public string AircraftTitle { get; init; } = "";
}
