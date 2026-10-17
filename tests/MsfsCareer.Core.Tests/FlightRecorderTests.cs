using MsfsCareer.Core.Flight;

namespace MsfsCareer.Core.Tests;

public class FlightRecorderTests
{
    [Fact]
    public void Recording_replays_to_the_same_result()
    {
        string path = Path.Combine(Path.GetTempPath(), $"msfs-career-{Guid.NewGuid():N}.jsonl");
        try
        {
            var script = FlightScript.Circuit(landingRateFpm: 210);
            using (var recorder = new FlightRecorder(path))
                foreach (var s in script.Samples)
                    recorder.Write(s);

            var replayed = new FlightTracker();
            foreach (var s in SnapshotLog.Read(path))
                replayed.Process(s);

            Assert.Equal(script.Run().Result, replayed.Result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Partly_written_last_line_is_skipped()
    {
        string path = Path.Combine(Path.GetTempPath(), $"msfs-career-{Guid.NewGuid():N}.jsonl");
        try
        {
            var script = FlightScript.Circuit();
            using (var recorder = new FlightRecorder(path))
                foreach (var s in script.Samples.Take(10))
                    recorder.Write(s);
            File.AppendAllText(path, "{\"time\":\"2026-10-17T09:");

            Assert.Equal(10, SnapshotLog.Read(path).Count());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
