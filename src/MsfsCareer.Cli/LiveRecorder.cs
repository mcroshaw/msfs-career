#if SIMCONNECT
using MsfsCareer.Core.Flight;
using MsfsCareer.SimLink;

namespace MsfsCareer.Cli;

/// <summary>Records flights from the sim until Ctrl+C, resuming an unfinished recording if one exists.</summary>
internal static class LiveRecorder
{
    public static int Run(string folder)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var (tracker, recorder) = ResumeOrStart(folder);
        var link = new SimConnectLink();
        link.Log += Console.WriteLine;
        link.ConnectionChanged += connected => Console.WriteLine(connected ? "Connected to the sim." : "Disconnected from the sim.");
        link.SnapshotReceived += s =>
        {
            recorder.Write(s);
            tracker.Process(s);
            if (tracker.Result is not null)
            {
                Console.WriteLine();
                Console.WriteLine(FlightSummary.Format(tracker.Result));
                Console.WriteLine($"Saved: {recorder.Path}");
                Console.WriteLine();
                recorder.Dispose();
                (tracker, recorder) = Start(folder);
            }
        };

        Console.WriteLine("Waiting for the sim. Press Ctrl+C to stop.");
        link.Run(cts.Token);
        recorder.Dispose();
        return 0;
    }

    private static (FlightTracker, FlightRecorder) ResumeOrStart(string folder)
    {
        var latest = Directory.Exists(folder)
            ? new DirectoryInfo(folder).GetFiles("*.jsonl").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()
            : null;

        if (latest is not null)
        {
            var tracker = NewTracker();
            foreach (var s in SnapshotLog.Read(latest.FullName))
                tracker.Process(s);

            if (tracker.Result is null && tracker.Phase != FlightPhase.Preflight)
            {
                Console.WriteLine($"Resuming unfinished flight ({tracker.Phase}) from {latest.Name}.");
                return (tracker, new FlightRecorder(latest.FullName));
            }
        }

        return Start(folder);
    }

    private static (FlightTracker, FlightRecorder) Start(string folder) =>
        (NewTracker(), new FlightRecorder(Path.Combine(folder, $"{DateTime.Now:yyyyMMdd-HHmmss}.jsonl")));

    private static FlightTracker NewTracker()
    {
        var tracker = new FlightTracker();
        tracker.PhaseChanged += Commands.PrintPhase;
        return tracker;
    }
}
#endif
