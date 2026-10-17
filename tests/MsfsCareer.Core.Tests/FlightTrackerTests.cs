using MsfsCareer.Core.Flight;

namespace MsfsCareer.Core.Tests;

public class FlightTrackerTests
{
    [Fact]
    public void Circuit_goes_through_every_phase_and_completes()
    {
        var phases = new List<FlightPhase>();
        var tracker = new FlightTracker();
        tracker.PhaseChanged += (phase, _) => phases.Add(phase);

        foreach (var s in FlightScript.Circuit().Samples)
            tracker.Process(s);

        Assert.Equal(
            new[] { FlightPhase.TaxiOut, FlightPhase.Airborne, FlightPhase.LandingRoll, FlightPhase.TaxiIn, FlightPhase.Parked },
            phases);
        Assert.NotNull(tracker.Result);
    }

    [Fact]
    public void Result_records_landing_rate_times_and_fuel()
    {
        var script = FlightScript.Circuit(landingRateFpm: 180);
        var result = script.Run().Result!;

        Assert.Equal(180, result.Landing.RateFpm);
        Assert.Equal(0, result.Landing.Bounces);
        Assert.Equal("Cessna 172 Skyhawk", result.AircraftTitle);
        Assert.True(result.BlockOut < result.Takeoff && result.Takeoff < result.Landing.Time && result.Landing.Time < result.BlockIn);
        Assert.Equal(script.Samples[^1].Time, result.BlockIn);
        Assert.InRange(result.FuelUsedLbs, 6, 7); // 0.01 lb/s for ~670 s
        Assert.True(result.DistanceFlownNm > 10);
        Assert.True(result.MaxAltitudeFt > 1000);
    }

    [Fact]
    public void Landing_rate_falls_back_to_sampled_vertical_speed()
    {
        var script = new FlightScript().Taxi(10).Climb(30).Descend(30, vsFpm: -320)
            .Hold(1, s => s with { OnGround = true, GroundSpeedKt = 50, VerticalSpeedFpm = 0, TouchdownRateFpm = null })
            .Rollout(5).Stop();

        Assert.Equal(320, script.Run().Result!.Landing.RateFpm);
    }

    [Fact]
    public void Bounce_keeps_first_touchdown_rate_and_counts_bounce()
    {
        var script = new FlightScript().Taxi(10).Climb(60).Descend(60)
            .Touchdown(420, g: 1.8)
            .Hold(2, s => s with { OnGround = false, GroundSpeedKt = 55, TouchdownRateFpm = null })
            .Touchdown(150, g: 2.1)
            .Rollout(5).Taxi(10).Stop();

        var landing = script.Run().Result!.Landing;
        Assert.Equal(420, landing.RateFpm);
        Assert.Equal(1, landing.Bounces);
        Assert.Equal(2.1, landing.GForce);
    }

    [Fact]
    public void Touch_and_go_is_counted_and_final_landing_is_used()
    {
        var script = new FlightScript().Taxi(10).Climb(60).Descend(60)
            .Touchdown(300)
            .Hold(8, s => s with { OnGround = true, GroundSpeedKt = 55 })
            .Climb(60).Cruise(60).Descend(60)
            .Touchdown(120)
            .Rollout(5).Taxi(10).Stop();

        var result = script.Run().Result!;
        Assert.Equal(1, result.TouchAndGoes);
        Assert.Equal(120, result.Landing.RateFpm);
    }

    [Fact]
    public void Single_airborne_sample_on_takeoff_roll_is_not_a_takeoff()
    {
        var script = new FlightScript().Taxi(10)
            .Hold(1, s => s with { OnGround = false, GroundSpeedKt = 40 })
            .Taxi(5);

        Assert.Equal(FlightPhase.TaxiOut, script.Run().Phase);
    }

    [Fact]
    public void Paused_time_is_excluded_from_block_time()
    {
        var withPause = new FlightScript().Taxi(10).Climb(60).Pause(600).Unpause().Cruise(60).Descend(60)
            .Touchdown(150).Rollout(5).Taxi(10).Stop().Run().Result!;
        var withoutPause = new FlightScript().Taxi(10).Climb(60).Unpause().Cruise(60).Descend(60)
            .Touchdown(150).Rollout(5).Taxi(10).Stop().Run().Result!;

        Assert.Equal(TimeSpan.FromSeconds(600), withPause.PausedTime);
        Assert.Equal(withoutPause.BlockTime, withPause.BlockTime);
        Assert.Equal(withoutPause.AirTime, withPause.AirTime);
        Assert.Equal(withoutPause.DistanceFlownNm, withPause.DistanceFlownNm, precision: 6);
    }

    [Fact]
    public void Stopping_with_engine_running_and_no_parking_brake_does_not_complete()
    {
        var tracker = new FlightScript().Taxi(10).Climb(60).Descend(60).Touchdown(150).Rollout(5)
            .Stop(engineOff: false, parkingBrake: false).Run();

        Assert.Equal(FlightPhase.TaxiIn, tracker.Phase);
        Assert.Null(tracker.Result);
    }

    [Fact]
    public void Parking_brake_with_engine_running_completes()
    {
        var tracker = new FlightScript().Taxi(10).Climb(60).Descend(60).Touchdown(150).Rollout(5)
            .Stop(engineOff: false, parkingBrake: true).Run();

        Assert.Equal(FlightPhase.Parked, tracker.Phase);
    }

    [Fact]
    public void Max_bank_and_g_are_tracked()
    {
        var result = new FlightScript().Taxi(10).Climb(60).Cruise(20, bankDeg: -48, g: 1.9).Descend(60)
            .Touchdown(150).Rollout(5).Taxi(10).Stop().Run().Result!;

        Assert.Equal(48, result.MaxBankDeg);
        Assert.Equal(1.9, result.MaxGForce);
    }
}
