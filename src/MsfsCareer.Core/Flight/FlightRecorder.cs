using System.Text.Json;

namespace MsfsCareer.Core.Flight;

/// <summary>
/// Appends every snapshot to a JSON Lines file and flushes each line, so a sim crash or app crash
/// loses at most the last sample. Replaying the file through a new <see cref="FlightTracker"/>
/// restores the flight in progress.
/// </summary>
public sealed class FlightRecorder : IDisposable
{
    private readonly StreamWriter _writer;

    public FlightRecorder(string path)
    {
        Path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read));
    }

    public string Path { get; }

    public void Write(SimSnapshot snapshot)
    {
        _writer.WriteLine(JsonSerializer.Serialize(snapshot, SnapshotLog.JsonOptions));
        _writer.Flush();
    }

    public void Dispose() => _writer.Dispose();
}

public static class SnapshotLog
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Reads a recording written by <see cref="FlightRecorder"/>. A partly written last line is skipped.</summary>
    public static IEnumerable<SimSnapshot> Read(string path)
    {
        using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            SimSnapshot? snapshot;
            try
            {
                snapshot = JsonSerializer.Deserialize<SimSnapshot>(line, JsonOptions);
            }
            catch (JsonException) when (reader.EndOfStream)
            {
                yield break;
            }
            if (snapshot is not null)
                yield return snapshot;
        }
    }
}
