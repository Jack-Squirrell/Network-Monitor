# Network Monitor Service

A lightweight .NET background service with a single-file dashboard that
pings configured hosts (IP or hostname) and exposes a small web UI and JSON
APIs for status, logs and target management.

**Features**
- Monitor multiple targets (name + address) defined in `appsettings.json` or
  added at runtime via the dashboard.
- Failure counting and severity levels (configurable thresholds).
- Runtime add/remove of targets with atomic persistence into
  `appsettings.json`.
- Minimal API + single-file UI served from `wwwroot`.
- Optional API protection via an API key (`X-Api-Key`).

**Prerequisites**
- .NET 8 SDK (or compatible runtime)
- Write permission to the directory when using runtime persistence

**Build & Run (development)**
```powershell
dotnet build
dotnet run
```
The dashboard will be available at `http://127.0.0.1:8080` by default.

**Publish (single-file / Release)**
```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishTrimmed=true /p:PublishSingleFile=true
```
Note: trimmed or single-file builds require the included source-generated JSON
context (`JsonContext`) to ensure System.Text.Json metadata is available.

**Configuration**
Edit `appsettings.json` to configure monitoring, interval and thresholds. Example:

```json
{
  "Monitoring": {
    "IntervalSeconds": 10,
    "WarningThreshold": 2,
    "CriticalThreshold": 5,
    "Targets": [
      { "Name": "Router", "Address": "192.168.1.1" },
      { "Name": "db", "Address": "10.0.0.5" }
    ]
  }
}
```

**API Endpoints**
- `GET /api/status` — returns JSON array of host statuses.
- `GET /api/logs` — returns recent log lines.
- `GET /api/targets` — returns configured targets.
- `POST /api/targets` — add a target (JSON: `{ "name":"x","address":"a.b.c.d" }`).
- `DELETE /api/targets/{address}` — remove a target by address.

When an API key is configured (see Security) all `/api/*` endpoints require an
`X-Api-Key` header.

**Security**
- API Key: set environment variable `ApiKey` to enable the server-side check:

```powershell
$env:ApiKey = 'your-secret-key'
dotnet run --configuration Release
```

- The dashboard includes a short input to paste the API key. The value is
  stored only in the browser `sessionStorage` and is not persisted by the
  service. For production, prefer a secrets manager or platform-specific
  secret injection (systemd, Windows service secrets store, or container
  secrets).

**Troubleshooting**
- If `appsettings.json` cannot be written at runtime the service will be able
  to monitor targets in-memory but additions/removals won't persist — ensure
  the process has write permission to the working directory.
- If publishing as a trimmed single-file exe and you see
  `JsonTypeInfo metadata for '...' was not provided`, verify `JsonContext.cs`
  exists and the `TypeInfoResolver` is configured in `Program.cs`.

**Contributing**
- Feel free to open issues or PRs. Suggested next improvements:
  - Move persistence out of `appsettings.json` to a dedicated small DB/file.
  - Add rate-limiting and stricter input validation on APIs.
  - Replace API key with OAuth2/OpenID Connect for multi-user deployments.

**License**
- MIT (or choose your preferred license)
