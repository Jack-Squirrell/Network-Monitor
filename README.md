# Network Monitor Service

A small .NET worker + dashboard that pings configured targets and shows status.

Usage
- Build and run locally:

```powershell
dotnet build
dotnet run
```

The web UI is available at `http://127.0.0.1:8080`.

Configuration
- Edit `appsettings.json` under `Monitoring` -> `Targets`.
- `WarningThreshold` and `CriticalThreshold` are configurable.

Contributing
- Open a PR or issue on the repository.
