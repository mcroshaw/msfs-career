namespace MsfsCareer.Core.Flight;

public static class Geo
{
    private const double EarthRadiusNm = 3440.065;

    /// <summary>Great-circle distance in nautical miles (haversine).</summary>
    public static double DistanceNm(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusNm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static double DistanceNm(GeoPoint a, GeoPoint b) =>
        DistanceNm(a.LatitudeDeg, a.LongitudeDeg, b.LatitudeDeg, b.LongitudeDeg);

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
