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

## Known errors database

`known-errors.json` is intentionally simple and versionable. Entries can match:

- `address`: exact absolute address, with optional `tolerance`.
- `start` + `end`: inclusive address range.
- `module`: optional module name filter such as `fallout2.exe`.

The first seed entry maps the local `0x00427B25` access violation that was traced to edited `text\french\game` data. The `0x004F11DB` entry is a placeholder ready to document when a confirmed dump is available.

## V1 scope

The V1 stays out of process injection and deep symbolic debugging. It focuses on reliable collection, minidumps, context, and a useful first-pass report. WinDbg/cdb parsing can be added later as an optional analyzer stage.
