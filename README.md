# CrashMonitor

CrashMonitor is a modern Windows/.NET replacement for the old AutoIt watcher used around Fallout 2 and sfall crash reports.

## Goals

- Detect `fallout2.exe` and monitor the process.
- Write minidumps through `MiniDumpWriteDump` from `dbghelp.dll`.
- Collect useful Fallout 2/sfall context: logs, INI files, mod hints, OS details and optional recent saves.
- Analyze an existing minidump and match the crash address against a local JSON knowledge base.
- Produce a readable Markdown report.

## Build

```powershell
dotnet build .\CrashMonitor.slnx
```

Run the small WPF interface:

```powershell
dotnet run --project .\CrashMonitor.Wpf
```

## Usage

Watch for Fallout 2 and write reports under `CrashReports`:

```powershell
dotnet run --project .\CrashMonitor.Cli -- watch --config .\crashmonitor.config.json --fallout-dir C:\Fallout2\F2_RPU_2.4.34_old
```

Analyze an existing dump:

```powershell
dotnet run --project .\CrashMonitor.Cli -- analyze C:\Fallout2\F2_RPU_2.4.34_old\crash_10122026_163227.dmp --known-errors .\known-errors.json --out .\CrashReports\report.md
```

Write a dump manually for a running process:

```powershell
dotnet run --project .\CrashMonitor.Cli -- dump --pid 1234 --output .\CrashReports
```

Generate fresh starter files:

```powershell
dotnet run --project .\CrashMonitor.Cli -- init-config .\crashmonitor.config.json
dotnet run --project .\CrashMonitor.Cli -- init-db .\known-errors.json
```

Import public CrashMonitor GitHub issue signatures into an untriaged database:

```powershell
dotnet run --project .\CrashMonitor.Cli -- import-github-issues --repo egornovivan/CrashMonitor --state all --max-pages 5 --out .\known-errors.github.json
```

By default, the importer ignores low/null instruction addresses below `0x00400000`. Use `--include-low-addresses` only when you want to keep those noisy cases. Use `--only-fallout-exe` to keep only addresses that look like Fallout 2 executable addresses.

Build a merged database from the local curated file plus imported GitHub signatures:

```powershell
dotnet run --project .\CrashMonitor.Cli -- import-github-issues --merge .\known-errors.json --out .\known-errors.enriched.json
```

Create a frequency-oriented triage report:

```powershell
dotnet run --project .\CrashMonitor.Cli -- triage .\known-errors.enriched.json --limit 30 --out .\artifacts\triage.md
```

Analyze a compiled Fallout script directly from its `.int` bytecode:

```powershell
dotnet run --project .\CrashMonitor.Cli -- analyze-script C:\Fallout2\F2_RPU_2.4.34_old\mods\PipboyExtended.dat\scripts\gl_PipboyEx.int --unsafe-address 0x004124E0 --out .\artifacts\script-gl-pipboyex-4124e0.md
```

The embedded script analyzer does not call an external `int2ssl.exe` and does not reference a separate library. It reads the INT procedure table, static strings and unsafe sfall opcodes directly inside CrashMonitor. It is meant for crash diagnosis, not full source reconstruction.

Capture the runtime state exposed by sfall's Debug Editor protocol:

```powershell
dotnet run --project .\CrashMonitor.Cli -- sfall-debug --arrays 0,1 --critter-locals 0 --out .\artifacts\sfall-debug.md
```

sfall opens this protocol only when its debug editor is launched from the game. For direct integration, publish CrashMonitor and place/copy the executable as `FalloutDebug.exe` in the Fallout 2 directory. When sfall starts it with `-debugedit`, CrashMonitor connects to `127.0.0.1:4245`, captures globals, map variables, sfall globals, arrays, critters and optional locals, writes a Markdown snapshot, sends the normal exit command, and lets the game resume.

Useful options:

- `--arrays 0,1`: include values for selected sfall arrays.
- `--locals 123`: include local variables for selected script IDs.
- `--critter-locals 0`: retrieve a critter, read its script ID and include that script's locals.
- `--max-rows 100`: limit rows per table.
- `--host` / `--port`: override the default `127.0.0.1:4245`.

## Known errors database

`known-errors.json` is intentionally simple and versionable. Entries can match:

- `address`: exact absolute address, with optional `tolerance`.
- `start` + `end`: inclusive address range.
- `module`: optional module name filter such as `fallout2.exe`.

The first seed entry maps the local `0x00427B25` access violation that was traced to edited `text\french\game` data. The `0x004F11DB` entry is a placeholder ready to document when a confirmed dump is available.

Imported GitHub entries are marked `status: "Untriaged"`. Treat them as sightings, not confirmed causes, until the linked issue, logs and dump attachments have been reviewed.

## V1 scope

The V1 stays out of process injection and deep symbolic debugging. It focuses on reliable collection, minidumps, context, and a useful first-pass report. WinDbg/cdb parsing can be added later as an optional analyzer stage.
