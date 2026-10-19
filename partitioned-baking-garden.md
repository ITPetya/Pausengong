# Pausengong — WinUI3/MQTT Rewrite

## Context

the maintainer runs a break-bell ("Pausengong") system across networked office/school PCs. The current implementation is a single Python script (not included in this repo) that coordinates via a JSON file on a network share — the first entry becomes "server", elections re-check every 60s via TCP+MQTT ping, and the server randomly MQTT-publishes a `play_gong` command to one connected client. It has no room grouping and no safe test mode.

the maintainer wants a full rewrite as a native Windows desktop app (C#/WinUI 3) that removes the network-share dependency entirely, adds room-aware scheduling (auto ultrasonic-fingerprint or manual drag&drop), a safe globally-synced test mode (toast notifications instead of real audio), and ships as a self-contained `.exe` (no installer, no separate .NET runtime) with MSIX packaging as a secondary Store-distribution path.

The original spec left one thing unspecified: how a PC discovers the current "Chef" (leader) over the network *before* any MQTT connection exists (first launch, or after a stale local cache). Resolved with the maintainer: **UDP broadcast discovery** on the LAN subnet, cache-assisted (try last-known Chef by unicast first, fall back to broadcast). MQTTnet version was also open (spec said "4"); resolved with the maintainer: use **MQTTnet 5.x** (actively maintained, same relevant API surface as 4.x).

**Hard safety constraint** (explicit instruction from the maintainer mid-task): automated/agent-driven verification must never trigger a real gong sound. All automated testing goes through the test-mode/toast path or mocked audio interfaces; real audio playback (including the manual Test-Gong button) is human-only, never invoked by an agent as a side effect of development or testing.

**Environment status**: this machine has no .NET SDK (only old shared runtimes 3.1/6/8) and no usable Visual Studio (the `Program Files\Microsoft Visual Studio\18` folder is an empty/orphaned stub). Everything will be built and published via `dotnet` CLI + winget-installed tooling — no VS required at any point, confirmed feasible for both the unpackaged self-contained exe and the MSIX path.

## Approach

Build a two-project solution: a UI-agnostic `Pausengong.Core` class library (fully unit-testable — MQTT, election, config sync, scheduling, audio abstractions) and a `Pausengong.App` WinUI 3 executable that hosts it via `Microsoft.Extensions.Hosting`/DI. Deliver in ordered phases, each independently verifiable, with the riskiest/least-agent-verifiable piece (ultrasonic room fingerprinting, which needs real multi-PC speaker+mic hardware) pushed to its own late phase rather than blocking the MVP.

### Solution layout

```
Pausengong.sln
src/
  Pausengong.Core/                     # net8.0 class library, no WinUI/UI refs
    Mqtt/EmbeddedBrokerService.cs      # MQTTnet 5.x embedded broker (MqttFactory/MqttServerOptionsBuilder), no ASP.NET Core dep
    Mqtt/MqttNodeClient.cs             # IManagedMqttClient wrapper: LWT, retained pub/sub, auto-reconnect
    Mqtt/TopicSchema.cs                # typed constants for the fixed topic tree (spec'd, unchanged)
    Election/SuccessionEntry.cs, SuccessionListStore.cs   # local JSON cache of priority list
    Election/ChefDiscoveryService.cs   # UDP broadcast/unicast discovery protocol (new design, see below)
    Election/ChefElectionService.cs    # discovery -> connect-as-client OR self-promote; duplicate-chief reconciliation
    Config/ScheduleConfig.cs, RoomConfig.cs, RoomModeConfig.cs, TestModeState.cs, ConfigStore.cs
    Scheduling/BreakScheduler.cs, DrawService.cs, AvailabilityPoller.cs, RingConfirmationTracker.cs
    Audio/IGongPlayer.cs, GongPlayer.cs          # NAudio WasapiOut real playback
    Audio/RoomFingerprintService.cs, RoomGroupingService.cs   # tone emit + FFT capture (Phase 6 only)
    Models/NodeInfo.cs, RoomGroup.cs, BreakSchedule.cs, DrawResult.cs
  Pausengong.App/                      # net8.0-windows10.0.19041.0, WinUI3 exe
    App.xaml/.cs                       # DI wiring
    Views/ (MainWindow, RoomAssignmentPage, ScheduleConfigPage, AutomaticRoomDetectionPage, SettingsPage)
    ViewModels/                        # CommunityToolkit.Mvvm
    Notifications/ToastService.cs      # AppNotificationManager wrapper
tests/
  Pausengong.Core.Tests/               # xUnit — election, scheduling, config; no real audio/network
packaging/
  AppxManifest.xml, build-msix.ps1     # Phase 7 only
```

Local storage: `%LocalAppData%\Pausengong\` — `config.json`, `succession.json`, `node-id.json` (persisted GUID so MQTT `{pc-id}` survives renames), `logs\`.

### Build/publish settings (validated, no VS required)

`Pausengong.App.csproj` core properties:
```xml
<TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
<TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
<UseWinUI>true</UseWinUI>
<EnableMsixTooling>true</EnableMsixTooling>
<WindowsPackageType>None</WindowsPackageType>
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
<SelfContained>true</SelfContained>
```
NuGet: `Microsoft.WindowsAppSDK` 1.8.x (stable; 2.0/.NET 10 line is still preview — avoid). Tooling: `winget install --id Microsoft.DotNet.SDK.8`, plus the standalone Windows 10/11 SDK for `makeappx`/`signtool` (verify exact winget package id with `winget search "Windows SDK"` at execution time).

Publish: `dotnet publish -r win-x64 -c Release -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=None --self-contained true`. Primary deliverable is a self-contained multi-file publish folder (exe + DLLs) zipped for distribution — "no installer, no separate runtime" is satisfied even if not literally one file. `PublishSingleFile=true` is attempted as a stretch in Phase 0 only (known WinUI3 crash-on-launch issues exist for single-file in some configs); fall back to the multi-file folder without touching anything downstream if it doesn't work cleanly here.

### UDP Chef-discovery protocol (new design)

- Discovery port: UDP 49531 (dynamic/private range). Broker port: 18830 (non-standard, avoids colliding with any other MQTT infra e.g. Home Assistant on the same LAN) rather than the default 1883.
- JSON datagrams: `{"type":"chef_discover","fromId":...,"protocolVersion":1}` and `{"type":"chef_announce","chefId":...,"address":...,"mqttPort":18830,"priorityTimestamp":...,"protocolVersion":1}`.
- Startup sequence per PC: (1) if a cached last-known Chef address exists, unicast `chef_discover` to it, 500ms timeout, 2 retries; (2) on timeout or no cache, broadcast to the subnet-directed broadcast address, 3 attempts ~800ms apart (~2.5s total); (3) on any `chef_announce` reply, cache it and connect as MQTT client to `address:mqttPort`; (4) on total timeout, self-promote — add self to succession list, start embedded broker, publish retained `election/chief`, start answering future discovery requests.
- Split-brain handling: every node (including a self-promoted Chef) keeps listening on the discovery port. A `chef_announce` from a different `chefId` than expected is a conflict signal; both sides compare `priorityTimestamp` (same rule as normal succession) — the loser tears down its broker and reconnects as a client to the winner. This is the single highest-value piece to unit test early since it trades a synchronous election lock for speed.

### Safety-critical gate

`Config/TestModeState.cs` is the single chokepoint deciding toast-vs-real-audio when a ring command executes. **Default state on any fresh config is test mode ON.** The manual Test-Gong button is wired only to a UI click handler — never reachable from startup, config sync, scheduling, or any test code path.

## Phased delivery (each phase independently buildable/verifiable)

1. **Environment spike** — throwaway hello-world WinUI3 csproj validating the build/publish settings above on this machine (currently no SDK/VS installed). Verify: unpackaged self-contained publish launches a window.
2. **Solution/project skeleton** — real `Pausengong.sln`, both csproj's, `Models/*`, empty `MainWindow.xaml`. Verify: `dotnet build` succeeds, app launches to empty shell.
3. **Local config persistence + node identity** — `ConfigStore`, `ScheduleConfig`/`RoomConfig`/`RoomModeConfig`, `NodeIdentity`. Verify: xUnit round-trip tests, no network/audio.
4. **MQTT + election core** — embedded broker, managed client, topic schema, succession store, UDP discovery, election service (incl. split-brain reconciliation). Verify: unit tests for succession ordering/tie-break/reconciliation against faked transports; manual two-instance loopback integration test.
5. **MVP: manual room mode + config sync + basic gong + test mode** — `IGongPlayer`/`GongPlayer`, `TestModeState` (MQTT-synced, retained, default-on), `ToastService`, drag-drop room assignment UI, visually loud test-mode banner. Verify: automated tests assert which path *would* fire via mocked `IGongPlayer`/`IToastService` only — never real NAudio playback in agent/CI runs. Human separately toggles test mode off and clicks Test-Gong once to confirm real audio, explicitly not automated.
6. **Scheduling draw/fallback logic** — `BreakScheduler`, `DrawService`, `AvailabilityPoller`, `RingConfirmationTracker`. Verify: fully unit-testable with injected `TimeProvider` + faked MQTT sink simulating T-60s/T-10s/T-0 ticks and fallback, no real timers/network/audio.
7. **Automatic room detection** — **superseded 2026-08-28**, then **revived as a second parallel track 2026-10-19**: the ultrasonic approach below was dropped 2026-08-28 (too many technical/environmental problems), replaced by BLE-RSSI-based neighborhood clustering. On 2026-10-19 the maintainer asked for ultrasonic to be built *alongside* BLE after all (found `ultramodem.ronen.in`, an unrelated ultrasonic data-transfer project, and wanted both tracks tested empirically rather than decided on paper) — full design for both tracks in `HANDOFF.md` (same repo, top level) under "Phase 7 replacement: BLE neighborhood clustering" (Track A) and "Track B: ultrasonic presence sensing" (Track B, not a port of ultramodem's protocol, see that section for why). Original ultrasonic idea, kept here for history only: (flagged separately riskier) — `RoomFingerprintService`, `RoomGroupingService`, detection UI. Verify: FFT-magnitude/threshold-grouping math unit-tested against synthetic sample arrays; the real tone-emit/capture round trip needs 2+ physical machines with real speakers/mic and is explicitly human-run hardware testing, not agent-verifiable, and not automated even then.
8. **MSIX packaging** (Store path, last) — `AppxManifest.xml`, `build-msix.ps1` (makeappx+signtool, no VS). Verify: local `Add-AppxPackage` install with a self-signed dev cert; actual Store submission is an out-of-repo human step.

## Verification summary

- Unit tests (xUnit, `Pausengong.Core.Tests`) cover election/succession logic, config persistence, scheduling/draw/fallback timing, and FFT threshold math — all deterministic, no real network/audio/timers.
- Manual multi-process loopback runs verify MQTT election/discovery end-to-end on one machine.
- Real audio playback and real ultrasonic hardware tests are explicitly human-only actions, called out per-phase, never invoked by an agent.
- Each phase's `dotnet build`/`dotnet test` must pass before moving to the next.
