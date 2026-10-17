# MSFS 2024 Career Mode Companion App: Plan

Status: proposal, no code yet. Written 17 Oct 2026. Decisions so far: free, offline single-player, local career flying with licences first, then bush flying (see sections 5 and 7).

## 1. What we're building

A standalone Windows app that runs alongside Microsoft Flight Simulator 2024 (and ideally 2020). It offers jobs, watches the flight through SimConnect, judges how it went, and pays out. Over time the player earns money, ratings and aircraft. Nothing is installed in the sim's Community folder for the MVP.

PC only: like every app in this genre, it can't run on Xbox or PlayStation because they have no SimConnect access for external apps.

## 2. How a standalone app talks to MSFS 2024

| Option | What it is | Use it for |
|---|---|---|
| **SimConnect (managed .NET)** | Microsoft's official API, shipped in the MSFS 2024 SDK. Out-of-process clients can be written in C, C++ or any .NET language. | **Primary choice.** Reading position, fuel, weight, gear, on-ground, vertical speed, engine state, aircraft title, and so on. |
| SimConnect (native C/C++) | The same API without the .NET wrapper. | Only if we pick Rust or C++. |
| **SimConnect.NET** (community, MIT) | Async/await wrapper over SimConnect for .NET 8/9, still in beta. | Worth trying to avoid the window-message pump boilerplate. Keep it behind our own interface in case it stalls. |
| FSUIPC7 | Long-running third-party bridge with its own offset API. The unregistered version is free and lets external apps connect; registering (paid) adds the user's own button and axis features. | Not needed. SimConnect covers what we read, and it would mean users installing a second program. |
| MobiFlight WASM module | Free in-sim module that exposes L-vars and H-events to external apps. | Later, only if we need aircraft-specific L-vars (e.g. a study-level airliner's custom state). |
| Python-SimConnect | Python wrapper. | Prototyping only. Packaging and UI story are weak. |

Facts that shape the design:

- **Model:** a client defines a data struct (`AddToDataDefinition` + `RegisterDataDefineStruct<T>`), requests it on an interval (e.g. per second), and receives callbacks. Events (pause, crash, sim start/stop, aircraft loaded) are subscribed the same way.
- **The managed client needs a window handle and a message pump**, and **SimConnect clients aren't thread-safe**. So one dedicated "sim link" component owns the connection and publishes snapshots to the rest of the app.
- **2020 vs 2024:** modules built with the 2020 SDK still work in 2024 but don't get new features. Building against the 2024 SDK is the way to go; a few structs changed (for example, helipad idents are now 8 characters).
- **Facility data:** SimConnect can return airport, runway and parking data from the sim itself, which helps keep jobs consistent with the user's scenery.
- **Disconnects happen** (sim crashes, menu reloads). The app must survive them, keep the flight log, and reconnect automatically.

## 3. What existing career apps do

| App | Model | Strengths people mention | Complaints people mention |
|---|---|---|---|
| **OnAir** | Subscription (about $3.50/month on annual billing), online shared world | Best UI of the bunch, huge depth: owned fleet, employees, AI pilots, full airline management. Aircraft stay where you leave them. | Subscription cost; online-only. |
| **NeoFly** | Free core, some paid extras; offline PC app | Smooth progression from small GA to turboprops; good for GA and helicopters. | Clunky UI, crashes that lose progress, filters apply only after relocating; weak for airliners. |
| **FSEconomy** | Free, web-based, shared economy | Long-running community, rentals, ownership, groups. | Dated web UI; aircraft availability depends on other players. |
| **Air Hauler 2** | Paid | Bases, cargo and passengers, fleet finances, AI pilots. | 2020 purchase doesn't automatically cover 2024. |
| **A Pilot's Life: Chapter 2** | About $35, one-off | Start as junior first officer, apply to airlines, ratings rise with performance; strong community events. | Airline-employee angle only. |
| **Fly the Line: Short Haul** | About $35, one-off | Fly any plane for any airline on a generated schedule. | Very light; repetitive; no management. |
| **AirOpsFS** | Free with optional premium | Multi-leg flights, AI pilots, codeshares and alliances, generated crew voices. | Newer, smaller user base. |
| **Clear2Land** | Free, early access | Single-player kept free. | Early access. |
| **Built-in MSFS 2024 Career** | Included | Good mission variety for GA, helicopters, SAR. | Can't fly any aircraft you like, airliners locked behind a ~40M price with no loans, bugs. |

Gaps we can aim at:

1. **A one-off or free offline career with a polished UI.** The best UI (OnAir) is subscription and online; the free offline option (NeoFly) has the weakest UI and stability.
2. **Never lose progress.** Write the flight log to disk continuously so a crash or disconnect doesn't wipe a flight.
3. **Fly anything you own.** The built-in career's biggest complaint. Match the user's installed aircraft to a career "type" by category and weight rather than a fixed whitelist.
4. **A path to airliners that doesn't take months.** Loans, leasing, or an "employee pilot" track (APL2-style) while saving up.

## 4. Recommended tech stack

| Layer | Choice | Why |
|---|---|---|
| Language/runtime | **C# on .NET 8 (LTS)** | Official managed SimConnect API; one language for sim link, game logic and UI. |
| Sim link | Microsoft.FlightSimulator.SimConnect (from the MSFS 2024 SDK), optionally via SimConnect.NET | Official, documented; wrapper reduces pump boilerplate. |
| UI | **WPF shell hosting Blazor Hybrid (WebView2)** | The WPF window provides the message pump SimConnect needs; Blazor gives a modern web UI and lets us drop in a Leaflet map for jobs and routes. |
| Storage | **SQLite via EF Core** | Single local file, easy backup, no server. |
| World data | **OurAirports** dataset (public domain) for airports and runways, plus SimConnect facility queries to confirm against the user's sim | Free, complete enough for job generation. |
| Packaging | MSIX or a plain installer (e.g. Velopack for auto-updates) | Simple distribution and updates. |
| Tests | xUnit, plus a **recorded-flight replayer** that feeds saved SimConnect snapshots into the flight tracker | Lets us test scoring without running the sim. |

Alternative I considered and don't recommend for the MVP: Tauri or Electron with a Rust/Node SimConnect binding. It's nicer for web developers, but adds a second language and a less mature SimConnect layer.

### Architecture sketch

```
MSFS 2024 ──SimConnect──▶ SimLink (one thread, owns connection)
                              │ snapshots every 1s + events
                              ▼
                        FlightTracker (state machine:
                        Preflight → Taxi → Takeoff → Cruise → Landing → Parked)
                              │ flight result (route, fuel, landing rate, time, damage)
                              ▼
       JobEngine ◀──▶ Career rules (pay, costs, ratings, fleet) ──▶ SQLite
                              │
                              ▼
                      Blazor UI (jobs board, map, logbook, finances, hangar)
```

## 5. MVP feature set and order

Order agreed with Mike on 17 Oct 2026: local career flying with a licence ladder first, bush flying after. Each milestone is playable on its own.

### M0: Sim link and flight tracker (foundation)
- Connect, auto-reconnect, show live status and aircraft.
- Detect flight phases; log departure, arrival, block time, fuel used, landing rate, max bank and g-load.
- Persist the log every few seconds.
- *Why first:* every other feature consumes a "flight result". Without it nothing can be scored.

### M1: Local jobs (the core loop, similar to the built-in career)
Short flights around a home region, each with its own success criteria:
- **Aircraft delivery / ferry:** move an aircraft between airports (for a dealer, owner or maintenance shop). Scored on no damage, no overstress, arriving within the window.
- **Sightseeing tours:** passengers want to see a landmark or coastline. The route passes over points of interest at a set height; scored on passenger comfort (gentle bank and pitch, smooth g, no hard landing).
- **Passenger hops and air taxi:** A to B with a deadline and a weight limit.
- **Small cargo and parcel runs:** mail, spare parts, urgent medical supplies.
- **Later in M1, as licences unlock:** skydiver drops (climb to altitude, drop over a zone, descend), banner towing, aerial photography passes, night flights.
- **Routes:** the app plans each job's route (departure, waypoints or tour points, destination), saves it as a .PLN, and sends it to the sim with SimConnect's flight plan load call. It can optionally load a saved flight so you start at the right airport in the right aircraft. Fallback: open the .PLN from the world map. *Test early:* how reliably MSFS 2024 and third-party avionics pick up a plan loaded this way.
- *Why second:* this is the moment the app feels like a career.

### M2: Licences and progression (built with M1)
Licences decide which jobs, aircraft and conditions you're allowed. You earn each one by logging the required hours and passing a **checkride**: a scripted flight the tracker scores (e.g. hold altitude ±100 ft, three landings in the touchdown zone, a diversion).

| Licence / endorsement | Unlocks |
|---|---|
| Student pilot | Training flights only, local area, day VFR, light singles. |
| Private pilot (PPL) | Unpaid or low-paid passenger flights, ferry flights of light singles. |
| Night rating | Night jobs. |
| Commercial pilot (CPL) | Paid passenger and tour work, skydiver drops, banner towing. |
| Instrument rating (IR) | Jobs in IMC and longer cross-countries. |
| Endorsements: complex, high-performance, tailwheel, seaplane | The matching aircraft types. |
| Bush endorsements: short field, unpaved, mountain (with M5) | Bush strips and jobs. |

- XP and reputation per job type on top of licences, so good tour pilots get better tour offers.
- Logbook with hours by class, day/night and conditions.
- *Why alongside M1:* licences gate which jobs appear, so the job board needs them from the start.

### M3: Money
- Pay per job, scaled by distance, payload, job type and on-time arrival.
- Costs: fuel, landing fees, rental per hour, training and checkride fees.
- Bonuses and penalties for landing quality, passenger comfort, overspeed or overstress.
- *Why third:* gives jobs meaning and creates the reason to buy aircraft.

### M4: Fleet
- Rent first (pay per hour), then buy or lease; aircraft stay where you parked them.
- Maintenance from hours and hard landings; aircraft unavailable until repaired.
- Map installed sim aircraft to owned career aircraft by title and category.
- *Why fourth:* it's the largest feature and depends on money and licences being balanced.

### M5: Bush flying expansion
The bush features listed under section 7 (strip tagging, resupply and medevac jobs, short-field scoring, bush endorsements, STOL modifications, one starting bush region).

### After MVP (candidates)
- A small charter company with hired AI pilots and bases.
- Helicopters and floatplane-only jobs.
- SimBrief import for flight plans; Navigraph charts link.
- MobiFlight WASM for aircraft-specific state.

## 6. Risks and open questions

- **Aircraft identification:** MSFS 2024 modular aircraft and liveries make matching by title messy. Plan a user-editable mapping table.
- **Cheating vs fun:** slew, pause and sim-rate acceleration need a policy (detect and flag, rather than block).
- **SimConnect.NET is beta:** wrap it behind our own interface so we can fall back to the raw managed API.
- **Licensing:** check the MSFS SDK EULA terms for redistributing the SimConnect DLLs with a free app (other free tools such as NeoFly already do this).

## 7. Decisions

- **17 Oct 2026: the app will be free.** No payments, licence keys or paid dependencies. Users shouldn't need to install anything besides the app, so FSUIPC isn't required. No servers to pay for, which also favours staying offline. Distribute through GitHub Releases with Velopack auto-updates, plus listings on flightsim.to and the forums. Positioning: "polished, free, offline", competing with NeoFly rather than OnAir.
- **17 Oct 2026: focus on GA and bush flying.** Airliners are out of scope, so gap 4 in section 3 is dropped. Bush flying is milestone M5; these points apply there:
  - **Aircraft classes:** light single (C152/C172), bush taildragger (Cub, Super Cub, Savage), utility (Kodiak, Caravan), with floats/amphibians as a later class. Helicopters are a post-MVP candidate.
  - **Airports:** include grass, gravel, dirt and water strips from OurAirports, plus short and one-way strips; tag runway surface and length so jobs can require "short field" or "unpaved" ratings.
  - **Job types:** remote resupply, passenger hops, fuel and food runs to camps, medevac, survey and sightseeing loops, and multi-leg bush routes. Pay more for remote, short or unpaved destinations.
  - **Scoring:** touchdown point vs runway threshold on short strips, landing rate and side-load on taildraggers, payload vs density altitude at takeoff, and penalties for prop strikes or gear damage.
  - **Ratings:** endorsements instead of type ratings: tailwheel, short field, unpaved, mountain, floats, night/IFR later.
  - **Fleet:** cheap starter aircraft (no loans needed), modifications like tundra tyres or STOL kits that unlock harder strips, and maintenance from rough-field wear.
  - **Regions:** start with one well-loved bush region (Alaska, Idaho backcountry or Papua New Guinea) so jobs feel hand-placed, then expand.
- **17 Oct 2026: offline, single-player only.** No accounts, servers or shared world, and "optional cloud sync or a shared world" is removed from the post-MVP list. The SQLite save file is the whole game state, so the app should offer manual backup/restore and keep automatic rolling backups.

- **17 Oct 2026: local career flying and licences come before bush flying.** Jobs like aircraft deliveries and tourist flights, similar to the built-in career, plus a licence ladder with checkrides (section 5, M1 and M2).
- **17 Oct 2026: open source.** MIT licence by default (simple, permissive, matches SimConnect.NET). Public GitHub repo with issues for bug reports. The SimConnect DLL redistribution terms in the MSFS SDK EULA still need checking before the first release.

## Sources

- [MSFS 2024 SimConnect SDK docs](https://docs.flightsimulator.com/msfs2024/html/6_Programming_APIs/SimConnect/SimConnect_SDK.htm)
- [Programming SimConnect clients using managed code](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/programming-simconnect-clients-using-managed-code/index.md)
- [SimConnect.NET on NuGet](https://packages.nuget.org/packages/SimConnect.NET/0.2.2)
- [C# adapter for SimConnect 20/24 (dev support forum)](https://devsupport.flightsimulator.com/t/c-adapter-for-simconnect-20-24-proposal-and-proof-of-concept/12583)
- [FSUIPC](https://www.fsuipc.com)
- [MobiFlight WASM Module](https://github.com/MobiFlight/MobiFlight-WASM-Module)
- [FlyAway Simulation: best career and economy mods](https://flyawaysimulation.com/ask/answers/best-career-economy-mods-microsoft-flight-simulator/)
- [AVSIM: exploring the various career add-ons](https://www.avsim.com/forums/topic/693171-exploring-the-various-career-add-ons-available/)
- [MSFS forums: OnAir vs FSEconomy vs Air Hauler 2 vs NeoFly vs The Skypark](https://forums.flightsimulator.com/t/career-mode-addons-onair-vs-fseconomy-vs-air-hauler-2-vs-neofly-vs-the-skypark/328066?page=2)
