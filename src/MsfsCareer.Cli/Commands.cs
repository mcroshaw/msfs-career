using MsfsCareer.Core.Flight;

namespace MsfsCareer.Cli;

internal static class Commands
{
    public static string RecordingsFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MsfsCareer", "recordings");

    public static int Usage()
    {
        Console.WriteLine("""
            MSFS Career flight tracker

              msfs-career record          Connect to the sim, record flights and print each result.
              msfs-career replay <file>   Run a recording through the tracker and print the result.

            Recordings are saved in:
            """ + "  " + RecordingsFolder);
        return 1;
    }

    public static int Replay(string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"No such file: {path}");
            return 1;
        }

        var tracker = new FlightTracker();
        tracker.PhaseChanged += PrintPhase;
        foreach (var s in SnapshotLog.Read(path))
            tracker.Process(s);

        if (tracker.Result is null)
        {
            Console.WriteLine($"Flight not finished (last phase: {tracker.Phase}).");
            return 2;
        }

        Console.WriteLine();
        Console.WriteLine(FlightSummary.Format(tracker.Result));
        return 0;
    }

    public static int Record()
    {
#if SIMCONNECT
        return LiveRecorder.Run(RecordingsFolder);
#else
        Console.Error.WriteLine("This build has no SimConnect support. Install the MSFS SDK, set MSFS2024_SDK, and rebuild on Windows.");
        return 1;
#endif
    }

    internal static void PrintPhase(FlightPhase phase, SimSnapshot s) =>
        Console.WriteLine($"{s.Time.ToLocalTime():HH:mm:ss}  {phase,-12} {s.LatitudeDeg,9:F4} {s.LongitudeDeg,10:F4}  {s.GroundSpeedKt,4:F0} kt  {s.AltitudeFt,6:F0} ft");
}
