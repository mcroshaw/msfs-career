namespace MsfsCareer.Core.Flight;

public enum FlightPhase
{
    /// <summary>Waiting at the gate or parking spot before the aircraft first moves.</summary>
    Preflight,
    /// <summary>Moving on the ground before takeoff.</summary>
    TaxiOut,
    Airborne,
    /// <summary>On the ground after a touchdown, still at speed.</summary>
    LandingRoll,
    /// <summary>Taxiing after landing.</summary>
    TaxiIn,
    /// <summary>Stopped after landing with the engine off or the parking brake set. The flight is complete.</summary>
    Parked,
}
