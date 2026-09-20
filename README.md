# PubQuizMaster

A self-hosted web application for running pub quiz nights: manage teams, hand out scoring stations to helpers, collect results live, and push the standings into your presentation deck between rounds.

Built as a Blazor Server app on .NET 9 with PostgreSQL. It is designed for a single host who runs the night and an arbitrary number of scorers working on their own phones.

## Features

**Quiz nights and rounds.** Create a quiz night, add rounds, mark one of them as the final. Round configuration covers the answer type (points, boolean, or free answers) and how the round is scored.

**Live scoring stations.** Each scorer gets a tokenized URL, handed out as a QR code from the dashboard. Scorers work through their assigned teams on a phone without logging in, the host sees presence and workflow position of every station in real time.

**Team management.** Teams register per night, recurring teams are matched against the existing roster with fuzzy matching so that minor spelling differences do not create duplicates. Detected duplicates can be merged.

**Standings and matrix view.** A leaderboard with competition ranking (equal scores share a rank, the next rank skips accordingly) plus a matrix of all teams against all rounds for the host.

**Presentation export.** Upload your own PowerPoint template, the app fills the result slides for the current round and hands the file straight back to the browser. Missing slides or shapes are reported instead of failing the export, so an incomplete template still produces a usable deck.

**Historic import.** Aggregated results from previous seasons can be imported from Excel. The import is idempotent, so re-running it updates existing records rather than duplicating them.

## Tech stack

| Layer | Choice |
|---|---|
| Runtime | .NET 9, ASP.NET Core |
| UI | Blazor Server (interactive server rendering, SignalR) |
| Data | PostgreSQL via EF Core (Npgsql), `IDbContextFactory` per operation |
| Auth | Cookie authentication, single admin password, rate-limited login |
| Documents | DocumentFormat.OpenXml (PPTX), ClosedXML (XLSX), QRCoder |
| Deployment | Docker image, ARM64 and AMD64 |

## Project layout

```
PubQuizMaster.Core       Models, records, enums, ranking logic (no dependencies)
PubQuizMaster.Data       AppDbContext, EF Core migrations
PubQuizMaster.Services   Quiz, scorer, team, leaderboard, import and export services
PubQuizMaster.Web        Blazor Server UI, pages, components, security
```

## Running locally

Requirements: .NET 9 SDK, Docker for the database.

```bash
# 1. Start PostgreSQL
docker compose -f docker-compose.dev.yml up -d

# 2. Configure secrets (the app refuses to start without an admin password)
cd PubQuizMaster.Web
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5434;Database=pubquizmaster;Username=pubquizmaster;Password=dev"
dotnet user-secrets set "Auth:AdminPassword" "dev"

# 3. Apply migrations
dotnet ef database update -p ../PubQuizMaster.Data -s .

# 4. Run
dotnet run
```

## Configuration

All settings can be supplied as environment variables using the standard double-underscore notation (`Auth__AdminPassword`).

| Key | Required | Description |
|---|---|---|
| `ConnectionStrings:Default` | yes | PostgreSQL connection string |
| `Auth:AdminPassword` | yes | Password for the single admin account. The app throws on startup if unset. |
| `ReverseProxy:Enabled` | no | Enables forwarded header processing. Set this when running behind nginx, Traefik, or a similar proxy. |
| `ReverseProxy:KnownNetworks` | conditional | CIDR ranges of trusted proxies, e.g. the Docker network of your proxy. Required as soon as `ReverseProxy:Enabled` is true. |
| `ReverseProxy:KnownProxies` | conditional | Individual proxy IP addresses, alternative to `KnownNetworks` |
| `Export:MaxFileSizeMb` | no | Upload limit for presentation templates, defaults to 200 |

The reverse proxy settings fail closed on purpose: trusting every sender would let clients spoof `X-Forwarded-For` and bypass the login rate limiter.

## Deployment

The repository ships a multi-stage `Dockerfile` and a `docker-compose.yml` that runs the app next to a PostgreSQL container, with the database on an internal network and the web container attached to an external proxy network.

```bash
docker compose up -d
```

Prebuilt images are published to `ghcr.io/budul100/pubquizmaster`. A detailed walkthrough including reverse proxy configuration, WebSocket settings, and migration handling is in [DEPLOYMENT.md](DEPLOYMENT.md).

Behind a reverse proxy, make sure WebSocket upgrades are forwarded and read timeouts are generous. Blazor Server keeps a circuit open per browser tab, and an aggressive timeout drops scorers mid-round.

## Notes

The presentation template is expected to use German slide texts and marker shapes; see the export service for the marker conventions. The application UI itself is English.

## License

MIT
