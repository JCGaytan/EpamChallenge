# TextProcessor

A .NET 9 + React application that demonstrates long-running text processing with real-time SignalR updates, EPAM-inspired styling, and connection-aware cancellation.

## Highlights

- SignalR streams character-by-character progress plus completion, failure, and cancellation events.
- Secure cancellation: the SPA forwards the hub connection ID through the `X-SignalR-ConnectionId` header so only the originating client can abort its job.
- Production-style logging (Serilog) and health checks, with a Docker Compose stack for parity with local development.
- Comprehensive tests, including integration coverage for successful and rejected cancellation flows.

## Repository Layout

```text
├── src/
│   ├── TextProcessor.Core/          # Domain services, job manager, processing pipeline
│   └── TextProcessor.Api/           # ASP.NET Core API, SignalR hub, background worker
├── client/
│   └── text-processor-ui/           # React 18 + TypeScript SPA
├── tests/
│   ├── TextProcessor.Core.Tests/    # Core unit tests
│   └── TextProcessor.Api.Tests/     # API integration tests (self-hosted SignalR)
└── docker/                          # Compose files, Dockerfiles, nginx config
```


## How It Works

1. The React SPA connects to the SignalR hub, caches the connection ID, and posts text to `/api/textprocessing/process` with that ID in `X-SignalR-ConnectionId`.
2. The API creates a job for the owning connection, queues it with the background service, and streams state updates via SignalR.
3. `TextProcessingService` produces the deterministic payload (`"<char><count>.../<base64>"`) while pushing `CharacterProcessed` events to the client.
4. The hook-driven UI listens for progress, completion, cancellation, and failure events to keep the form state, EPAM-themed progress bar, and toast feedback synchronized.

## Run It Locally

### Prerequisites

- .NET 9 SDK
- Node.js 18+
- (Optional) Docker Desktop for the containerized workflow

### Option 1: Developer CLI workflow

```powershell
cd src/TextProcessor.Api
dotnet run
```

API listens on `http://localhost:5133` (watch the console for Serilog output and health-check chatter).

Start the React SPA:

```powershell
cd client/text-processor-ui
npm install
npm start
```

The UI is available at `http://localhost:3000` and proxies API + SignalR calls to `http://localhost:5133` based on `.env.development`.

### Option 2: Docker Compose

```powershell
cd docker
docker compose up --build -d
```

Browse to `http://localhost:8080` (served by nginx). Tear the stack down with `docker compose down` when finished.

The compose stack launches the API, UI, and an nginx reverse proxy. nginx terminates basic auth, forwards `/api/*` and `/hubs/*` to the API with WebSocket upgrades for SignalR, and serves the built SPA for everything else using the config in `docker/nginx-proxy.conf`.

The React build baked into the image uses `.env.production` so browser calls stay on the proxy origin (`/api` and `/hubs/processing`). Local development keeps targeting the Kestrel port via `.env.development`.

## Run the Tests

- Full .NET suite (includes integration coverage for the cancellation workflow):

  ```powershell
  dotnet test
  ```

- Frontend hook + UI tests:

  ```powershell
  cd client/text-processor-ui
  $env:CI='true'; npm test -- --watchAll=false; Remove-Item Env:CI
  ```

## Cancellation Workflow

1. The UI blocks job creation until SignalR has produced a connection ID.
2. `TextProcessingApi.processText` sends the `X-SignalR-ConnectionId` header so the backend records job ownership.
3. When **Cancel** is clicked, the SPA invokes the hub; the API checks ownership before cancelling the background worker and broadcasting `JobCancelled`.
4. Integration tests `CancelJob_RunningJob_ReturnsOkAndMarksJobCancelled` and `CancelJob_CompletedJob_ReturnsBadRequest` guard both the happy path and the immutable-job scenario.

## UI Notes

- The form, EPAM-themed progress bar, and streamed output are the focal points—no global chrome to distract from the workflow.
- The processed text area is read-only and streams characters as the hub events arrive.
- Connection status pills reflect the SignalR state, and cancellation errors surface inline when the backend rejects an unauthorized attempt.
