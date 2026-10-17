using MsfsCareer.Core.Flight;

namespace MsfsCareer.Core.Tests;

/// <summary>Builds a one-sample-per-second flight, the same rate the sim link requests.</summary>
internal sealed class FlightScript
{
    private static readonly DateTimeOffset Start = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly List<SimSnapshot> _samples = new();
    private SimSnapshot _state = new()
    {
        Time = Start,
        LatitudeDeg = 51.0,
        LongitudeDeg = -1.0,
        GroundSpeedKt = 0,
        OnGround = true,
        EngineRunning = true,
        FuelTotalLbs = 200,
        AircraftTitle = "Cessna 172 Skyhawk",
    };

    public IReadOnlyList<SimSnapshot> Samples => _samples;

    /// <summary>Adds <paramref name="seconds"/> samples, each changed by <paramref name="change"/>, moving north at the current ground speed.</summary>
    public FlightScript Hold(int seconds, Func<SimSnapshot, SimSnapshot>? change = null)
    {
        for (int i = 0; i < seconds; i++)
        {
            var next = change?.Invoke(_state) ?? _state;
            double movedDeg = next.Paused ? 0 : next.GroundSpeedKt / 3600.0 / 60.0;
            _state = next with
            {
                Time = _state.Time.AddSeconds(1),
                LatitudeDeg = next.LatitudeDeg + movedDeg,
                FuelTotalLbs = next.Paused || !next.EngineRunning ? next.FuelTotalLbs : next.FuelTotalLbs - 0.01,
            };
            _samples.Add(_state);
        }
        return this;
    }

    public FlightScript Taxi(int seconds) => Hold(seconds, s => s with { GroundSpeedKt = 12, OnGround = true });

    public FlightScript Climb(int seconds) =>
        Hold(seconds, s => s with { GroundSpeedKt = 90, OnGround = false, VerticalSpeedFpm = 700, AltitudeFt = s.AltitudeFt + 12, TouchdownRateFpm = null });

    public FlightScript Cruise(int seconds, double bankDeg = 0, double g = 1.0) =>
        Hold(seconds, s => s with { GroundSpeedKt = 110, OnGround = false, VerticalSpeedFpm = 0, BankDeg = bankDeg, GForce = g });

    public FlightScript Descend(int seconds, double vsFpm = -500) =>
        Hold(seconds, s => s with { GroundSpeedKt = 70, OnGround = false, VerticalSpeedFpm = vsFpm, AltitudeFt = Math.Max(0, s.AltitudeFt + vsFpm / 60), BankDeg = 0, GForce = 1.0 });

    public FlightScript Touchdown(double rateFpm, double g = 1.3) =>
        Hold(1, s => s with { GroundSpeedKt = 60, OnGround = true, VerticalSpeedFpm = 0, TouchdownRateFpm = rateFpm, GForce = g });

    public FlightScript Rollout(int seconds) =>
        Hold(seconds, s => s with { GroundSpeedKt = Math.Max(10, s.GroundSpeedKt - 10), OnGround = true, GForce = 1.0 });

    public FlightScript Stop(bool engineOff = true, bool parkingBrake = false) =>
        Hold(1, s => s with { GroundSpeedKt = 0, OnGround = true, EngineRunning = !engineOff, ParkingBrakeSet = parkingBrake });

    public FlightScript Pause(int seconds) => Hold(seconds, s => s with { Paused = true });

    public FlightScript Unpause() => Hold(1, s => s with { Paused = false });

    /// <summary>A complete local circuit: taxi, take off, fly, land, taxi in, shut down.</summary>
    public static FlightScript Circuit(double landingRateFpm = 150) =>
        new FlightScript()
            .Hold(5)
            .Taxi(60)
            .Climb(120)
            .Cruise(300)
            .Descend(120)
            .Touchdown(landingRateFpm)
            .Rollout(5)
            .Taxi(60)
            .Stop();

    public FlightTracker Run()
    {
        var tracker = new FlightTracker();
        foreach (var s in _samples)
            tracker.Process(s);
        return tracker;
    }
}
