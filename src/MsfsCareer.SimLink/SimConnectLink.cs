using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using MsfsCareer.Core.Flight;

namespace MsfsCareer.SimLink;

/// <summary>
/// Owns the SimConnect connection on one thread (SimConnect clients aren't thread-safe),
/// requests the user aircraft's state once a second, and reconnects if the sim goes away.
/// </summary>
public sealed class SimConnectLink
{
    private enum Definition { Snapshot }
    private enum Request { Snapshot }
    private enum SystemEvent { Pause }

    // Field order must match the AddToDataDefinition calls in Define().
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct SnapshotData
    {
        public double Latitude;
        public double Longitude;
        public double Altitude;
        public double AltitudeAgl;
        public double GroundSpeed;
        public double IndicatedAirspeed;
        public double VerticalSpeed;
        public double HeadingTrue;
        public double Bank;
        public double Pitch;
        public double GForce;
        public double OnGround;
        public double EngineRunning;
        public double ParkingBrake;
        public double FuelTotalWeight;
        public double TouchdownNormalVelocity;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Title;
    }

    private static readonly (string Name, string Unit)[] Variables =
    {
        ("PLANE LATITUDE", "degrees"),
        ("PLANE LONGITUDE", "degrees"),
        ("PLANE ALTITUDE", "feet"),
        ("PLANE ALT ABOVE GROUND", "feet"),
        ("GROUND VELOCITY", "knots"),
        ("AIRSPEED INDICATED", "knots"),
        ("VERTICAL SPEED", "feet per minute"),
        ("PLANE HEADING DEGREES TRUE", "degrees"),
        ("PLANE BANK DEGREES", "degrees"),
        ("PLANE PITCH DEGREES", "degrees"),
        ("G FORCE", "gforce"),
        ("SIM ON GROUND", "bool"),
        ("GENERAL ENG COMBUSTION:1", "bool"),
        ("BRAKE PARKING POSITION", "bool"),
        ("FUEL TOTAL QUANTITY WEIGHT", "pounds"),
        ("PLANE TOUCHDOWN NORMAL VELOCITY", "feet per minute"),
    };

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private bool _paused;
    private bool _quit;

    /// <summary>Raised on the link's thread for every sample.</summary>
    public event Action<SimSnapshot>? SnapshotReceived;

    /// <summary>Raised when the sim connects (true) or disconnects (false).</summary>
    public event Action<bool>? ConnectionChanged;

    public event Action<string>? Log;

    /// <summary>Connects, pumps messages and reconnects until <paramref name="ct"/> is cancelled. Blocks the calling thread.</summary>
    public void Run(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                RunConnection(ct);
            }
            catch (COMException ex)
            {
                // Thrown by the SimConnect constructor when the sim isn't running.
                Log?.Invoke($"Sim not reachable ({ex.Message.Trim()}). Retrying in {RetryDelay.TotalSeconds:0}s.");
            }

            ct.WaitHandle.WaitOne(RetryDelay);
        }
    }

    private void RunConnection(CancellationToken ct)
    {
        using var messageReady = new AutoResetEvent(false);
        using var sim = new SimConnect("MSFS Career", IntPtr.Zero, 0, messageReady, 0);

        _quit = false;
        _paused = false;

        sim.OnRecvOpen += (_, _) => ConnectionChanged?.Invoke(true);
        sim.OnRecvQuit += (_, _) => _quit = true;
        sim.OnRecvException += (_, e) => Log?.Invoke($"SimConnect exception {(SIMCONNECT_EXCEPTION)e.dwException}");
        sim.OnRecvEvent += OnEvent;
        sim.OnRecvSimobjectData += OnData;

        Define(sim);
        sim.SubscribeToSystemEvent(SystemEvent.Pause, "Pause_EX1");
        sim.RequestDataOnSimObject(Request.Snapshot, Definition.Snapshot, SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);

        try
        {
            while (!ct.IsCancellationRequested && !_quit)
            {
                if (messageReady.WaitOne(500))
                    sim.ReceiveMessage();
            }
        }
        catch (COMException ex)
        {
            Log?.Invoke($"Lost connection to the sim ({ex.Message.Trim()}).");
        }
        finally
        {
            ConnectionChanged?.Invoke(false);
        }
    }

    private static void Define(SimConnect sim)
    {
        foreach (var (name, unit) in Variables)
            sim.AddToDataDefinition(Definition.Snapshot, name, unit, SIMCONNECT_DATATYPE.FLOAT64, 0, SimConnect.SIMCONNECT_UNUSED);
        sim.AddToDataDefinition(Definition.Snapshot, "TITLE", null, SIMCONNECT_DATATYPE.STRING256, 0, SimConnect.SIMCONNECT_UNUSED);
        sim.RegisterDataDefineStruct<SnapshotData>(Definition.Snapshot);
    }

    private void OnEvent(SimConnect sender, SIMCONNECT_RECV_EVENT e)
    {
        // Pause_EX1 sends a set of pause flags; any flag means the aircraft isn't flying.
        if ((SystemEvent)e.uEventID == SystemEvent.Pause)
            _paused = e.dwData != 0;
    }

    private void OnData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA e)
    {
        if ((Request)e.dwRequestID != Request.Snapshot)
            return;

        var d = (SnapshotData)e.dwData[0];
        bool onGround = d.OnGround != 0;
        SnapshotReceived?.Invoke(new SimSnapshot
        {
            Time = DateTimeOffset.UtcNow,
            LatitudeDeg = d.Latitude,
            LongitudeDeg = d.Longitude,
            AltitudeFt = d.Altitude,
            AltitudeAglFt = d.AltitudeAgl,
            GroundSpeedKt = d.GroundSpeed,
            IndicatedAirspeedKt = d.IndicatedAirspeed,
            VerticalSpeedFpm = d.VerticalSpeed,
            HeadingTrueDeg = d.HeadingTrue,
            // SimConnect reports bank and pitch with the opposite sign to the usual convention.
            BankDeg = -d.Bank,
            PitchDeg = -d.Pitch,
            GForce = d.GForce,
            OnGround = onGround,
            EngineRunning = d.EngineRunning != 0,
            ParkingBrakeSet = d.ParkingBrake != 0,
            FuelTotalLbs = d.FuelTotalWeight,
            // The sim keeps the last touchdown's value, so it only means something once on the ground.
            TouchdownRateFpm = onGround && d.TouchdownNormalVelocity != 0 ? Math.Abs(d.TouchdownNormalVelocity) : null,
            Paused = _paused,
            AircraftTitle = d.Title ?? "",
        });
    }
}
