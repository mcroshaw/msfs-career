# MSFS Career

A free, open-source, offline career mode for Microsoft Flight Simulator 2024 (and 2020). It runs as a standalone Windows app alongside the sim, offers local jobs such as aircraft deliveries and sightseeing tours, tracks each flight through SimConnect, and builds your pilot career with licences, money and a fleet. Bush flying comes later.

Status: early development. See [docs/plan.md](docs/plan.md) for the plan and roadmap.

## What works so far: the flight tracker

A command-line tool that watches your flight and prints a summary when you park: block and air time, distance, fuel used, landing rate and g, bounces, touch-and-goes, max bank. Every sample is written to disk as you fly, so if the sim or the app crashes, restarting `record` picks the flight up where it left off.

### Try it without the sim

```
dotnet run --project src/MsfsCareer.Cli -- replay samples/local-circuit.jsonl
```

### Record a real flight (Windows)

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and the MSFS SDK (in MSFS: Options → General → Developers, then download the SDK installer from the Developer menu). The SDK installer sets the `MSFS2024_SDK` environment variable; for the 2020 SDK set `MSFS_SDK` instead.
2. Start the sim and load a flight.
3. Run:
   ```
   dotnet run --project src/MsfsCareer.Cli -- record
   ```
4. Fly. The tool prints each phase change, and the full summary once you stop with the engine off or the parking brake set.

Recordings go to `%LOCALAPPDATA%\MsfsCareer\recordings`. Replay any of them with `replay <file>`.

## Layout

| Project | What it does |
|---|---|
| `src/MsfsCareer.Core` | Flight tracker state machine and crash-safe recorder. No sim dependency. |
| `src/MsfsCareer.SimLink` | SimConnect connection: reads the aircraft once a second and reconnects if the sim goes away. Builds empty without the MSFS SDK. |
| `src/MsfsCareer.Cli` | `record` and `replay` commands. |
| `tests/MsfsCareer.Core.Tests` | Tracker and recorder tests (`dotnet test`). |

## Licence

MIT. See [LICENSE](LICENSE).
