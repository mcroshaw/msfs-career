using MsfsCareer.Core.Flight;

namespace MsfsCareer.Cli;

internal static class FlightSummary
{
    public static string Format(FlightResult r) => $"""
        Flight complete: {r.AircraftTitle}
          From        {r.Departure.LatitudeDeg:F4}, {r.Departure.LongitudeDeg:F4}
          To          {r.Arrival.LatitudeDeg:F4}, {r.Arrival.LongitudeDeg:F4}
          Block time  {Hms(r.BlockTime)}{(r.PausedTime > TimeSpan.Zero ? $" (excluding {Hms(r.PausedTime)} paused)" : "")}
          Air time    {Hms(r.AirTime)}
          Distance    {r.DistanceFlownNm:F1} nm
          Fuel used   {r.FuelUsedLbs:F1} lb
          Landing     {r.Landing.RateFpm:F0} fpm, {r.Landing.GForce:F2} g{(r.Landing.Bounces > 0 ? $", {r.Landing.Bounces} bounce(s)" : "")}
          Touch & go  {r.TouchAndGoes}
          Max bank    {r.MaxBankDeg:F0}°
          G range     {r.MinGForce:F2} to {r.MaxGForce:F2}
          Max alt     {r.MaxAltitudeFt:F0} ft
        """;

    private static string Hms(TimeSpan t) => t.ToString(@"hh\:mm\:ss");
}
