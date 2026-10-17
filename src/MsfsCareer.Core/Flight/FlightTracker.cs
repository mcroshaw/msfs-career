namespace MsfsCareer.Core.Flight;

/// <summary>
/// Turns a stream of <see cref="SimSnapshot"/>s into flight phases and, once the aircraft
/// is parked after landing, a <see cref="FlightResult"/>.
/// The tracker is pure logic: it never talks to the sim, so recorded flights can be replayed through it.
/// </summary>
public sealed class FlightTracker
{
    /// <summary>Ground speed above which the aircraft counts as moving.</summary>
    public const double MovingSpeedKt = 3;
    /// <summary>Ground speed below which a landing roll becomes taxi.</summary>
    public const double TaxiSpeedKt = 30;
    /// <summary>Ground speed below which the aircraft counts as stopped.</summary>
    public const double StoppedSpeedKt = 1;
    /// <summary>Consecutive airborne samples needed before a takeoff counts, so a bump on a rough strip isn't a takeoff.</summary>
    public const int TakeoffConfirmSamples = 2;
    /// <summary>Leaving the ground again within this time of a touchdown is a bounce, not a touch-and-go.</summary>
    public static readonly TimeSpan BounceWindow = TimeSpan.FromSeconds(5);

    private SimSnapshot? _last;
    private SimSnapshot? _lastUnpaused;
    private SimSnapshot? _lastAirborne;
    private int _airborneSamples;
    private DateTimeOffset? _liftoffTime;

    private DateTimeOffset? _blockOut;
    private DateTimeOffset? _takeoff;
    private GeoPoint? _departure;
    private string _aircraftTitle = "";
    private double _startFuelLbs;
    private double _minFuelLbs = double.MaxValue;

    private Landing? _landing;
    private bool _inBounce;
    private int _touchAndGoes;

    private double _distanceNm;
    private double _maxG = 1;
    private double _minG = 1;
    private double _maxBank;
    private double _maxAltitude;
    private TimeSpan _paused;
    private TimeSpan _pausedAirborne;

    public FlightPhase Phase { get; private set; } = FlightPhase.Preflight;

    /// <summary>The completed flight, once <see cref="Phase"/> is <see cref="FlightPhase.Parked"/>.</summary>
    public FlightResult? Result { get; private set; }

    public event Action<FlightPhase, SimSnapshot>? PhaseChanged;
    public event Action<FlightResult>? FlightCompleted;

    public void Process(SimSnapshot s)
    {
        if (Phase == FlightPhase.Parked)
            return;

        if (s.Paused)
        {
            if (_last is not null)
            {
                _paused += s.Time - _last.Time;
                if (Phase == FlightPhase.Airborne)
                    _pausedAirborne += s.Time - _last.Time;
            }
            _last = s;
            return;
        }

        if (Phase != FlightPhase.Preflight)
            UpdateStats(s);

        switch (Phase)
        {
            case FlightPhase.Preflight:
                if (!s.OnGround)
                {
                    // Flight started in the air (e.g. loaded mid-air). Treat the start point as departure.
                    StartBlock(s);
                    _takeoff = s.Time;
                    SetPhase(FlightPhase.Airborne, s);
                }
                else if (s.GroundSpeedKt > MovingSpeedKt)
                {
                    StartBlock(s);
                    SetPhase(FlightPhase.TaxiOut, s);
                }
                break;

            case FlightPhase.TaxiOut:
                _airborneSamples = s.OnGround ? 0 : _airborneSamples + 1;
                if (_airborneSamples == 1)
                    _liftoffTime = s.Time;
                if (_airborneSamples >= TakeoffConfirmSamples)
                {
                    _takeoff = _liftoffTime;
                    SetPhase(FlightPhase.Airborne, s);
                }
                break;

            case FlightPhase.Airborne:
                if (s.OnGround)
                {
                    Touchdown(s);
                    SetPhase(FlightPhase.LandingRoll, s);
                }
                break;

            case FlightPhase.LandingRoll:
                if (!s.OnGround)
                {
                    if (_landing is not null && s.Time - _landing.Time <= BounceWindow)
                    {
                        _landing = _landing with { Bounces = _landing.Bounces + 1 };
                        _inBounce = true;
                    }
                    else
                    {
                        _touchAndGoes++;
                        _landing = null;
                    }
                    SetPhase(FlightPhase.Airborne, s);
                }
                else if (s.GroundSpeedKt < TaxiSpeedKt)
                {
                    SetPhase(FlightPhase.TaxiIn, s);
                }
                break;

            case FlightPhase.TaxiIn:
                if (!s.OnGround)
                {
                    // Took off again after slowing down: a stop-and-go. The earlier landing doesn't end the flight.
                    _touchAndGoes++;
                    _landing = null;
                    SetPhase(FlightPhase.Airborne, s);
                }
                else if (s.GroundSpeedKt < StoppedSpeedKt && (!s.EngineRunning || s.ParkingBrakeSet))
                {
                    Complete(s);
                }
                break;
        }

        if (!s.OnGround)
            _lastAirborne = s;
        _last = s;
    }

    private void StartBlock(SimSnapshot s)
    {
        _blockOut = s.Time;
        _departure = new GeoPoint(s.LatitudeDeg, s.LongitudeDeg);
        _aircraftTitle = s.AircraftTitle;
        _startFuelLbs = s.FuelTotalLbs;
        _minFuelLbs = s.FuelTotalLbs;
        _maxAltitude = s.AltitudeFt;
        _lastUnpaused = s;
    }

    private void UpdateStats(SimSnapshot s)
    {
        if (_lastUnpaused is not null)
            _distanceNm += Geo.DistanceNm(_lastUnpaused.LatitudeDeg, _lastUnpaused.LongitudeDeg, s.LatitudeDeg, s.LongitudeDeg);

        _lastUnpaused = s;
        _maxG = Math.Max(_maxG, s.GForce);
        _minG = Math.Min(_minG, s.GForce);
        _maxBank = Math.Max(_maxBank, Math.Abs(s.BankDeg));
        _maxAltitude = Math.Max(_maxAltitude, s.AltitudeFt);
        _minFuelLbs = Math.Min(_minFuelLbs, s.FuelTotalLbs);
    }

    private void Touchdown(SimSnapshot s)
    {
        // Prefer the sim's latched touchdown rate; fall back to the last sampled descent rate.
        double rate = s.TouchdownRateFpm ?? Math.Max(0, -(_lastAirborne?.VerticalSpeedFpm ?? 0));

        if (_inBounce && _landing is not null)
        {
            // The first touchdown of a bounced landing sets the landing rate; later contacts can only make the g worse.
            _landing = _landing with { GForce = Math.Max(_landing.GForce, s.GForce) };
            _inBounce = false;
            return;
        }

        _landing = new Landing(s.Time, new GeoPoint(s.LatitudeDeg, s.LongitudeDeg), rate, s.GForce, Bounces: 0);
    }

    private void Complete(SimSnapshot s)
    {
        Result = new FlightResult
        {
            AircraftTitle = _aircraftTitle,
            Departure = _departure!,
            Arrival = new GeoPoint(s.LatitudeDeg, s.LongitudeDeg),
            BlockOut = _blockOut!.Value,
            Takeoff = _takeoff!.Value,
            BlockIn = s.Time,
            Landing = _landing!,
            TouchAndGoes = _touchAndGoes,
            FuelUsedLbs = Math.Max(0, _startFuelLbs - _minFuelLbs),
            DistanceFlownNm = _distanceNm,
            MaxGForce = _maxG,
            MinGForce = _minG,
            MaxBankDeg = _maxBank,
            MaxAltitudeFt = _maxAltitude,
            PausedTime = _paused,
            PausedAirborneTime = _pausedAirborne,
        };
        SetPhase(FlightPhase.Parked, s);
        FlightCompleted?.Invoke(Result);
    }

    private void SetPhase(FlightPhase phase, SimSnapshot s)
    {
        Phase = phase;
        PhaseChanged?.Invoke(phase, s);
    }
}
