# MoltbookPilot 👽🟢  
Neon-themed **ASP.NET Core Razor Pages** “ops console” for running a **local LM Studio agent** that can register/verify on **Moltbook** and persist agent state in **SQL Server**.

> **Local-first:** This app talks to your locally-running LM Studio server (OpenAI-compatible `/v1` endpoints) and keeps secrets out of source control using **User Secrets**.

---

## What it does

- **LM Studio connectivity checks**
  - Ping `GET /v1/models` on your local LM Studio server
  - Send a prompt to your local model via `POST /v1/chat/completions`
- **Moltbook onboarding**
  - “Join Moltbook” flow that registers an agent and returns a **claim link**
  - Store agent state (**handle, claim URL, API key, heartbeat timestamps**) in SQL Server
- **Moltbook state dashboard**
  - View stored claim URL / masked API key
  - Refresh state from the backend

---

## Architecture (high level)

- **Razor Pages UI**: `Pages/Index.cshtml` (“Neon Alien Ops Console”)
- **LM Studio client**: `LmStudioClient` (OpenAI-compatible Chat Completions)
- **Tool runtime**: `AgentTools` + tool-call loop (HTTP GET / POST JSON)
- **Persistence**: `MoltbookAgentState` + `MoltbookDbContext` + `MoltbookStateStore`
- **Endpoints** (typical):
  - `GET /api/health`
  - `POST /api/agent/think`
  - `POST /api/moltbook/join`
  - `GET /api/moltbook/state`

---

## Prerequisites

- **.NET SDK** matching the project’s `TargetFramework` (e.g., `net9.0` or `net10.0`)
- **LM Studio** installed and running with:
  - A model downloaded and loaded
  - **Local Server** enabled (default: `http://localhost:1234/v1`)
- **SQL Server** (any of):
  - SQL Server Express (`.\SQLEXPRESS`)
  - LocalDB (`(localdb)\MSSQLLocalDB`)
  - Full SQL Server instance
- (Optional) **SSMS** for inspecting the DB

---

## Setup

### 1) Clone and restore

```bash
git clone <YOUR_REPO_URL>
cd MoltbookPilot
dotnet restore
```

### 2) Configure User Secrets (required)

This project uses **User Secrets** for local development so secrets do not land in `appsettings.json` or Git.

From the **project directory** (the folder containing the `.csproj`):

```bash
dotnet user-secrets init
```

Set your SQL connection string:

**SQL Express example**
```bash
dotnet user-secrets set "ConnectionStrings:MoltbookPilotDb" "Server=.\SQLEXPRESS;Database=MoltbookDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

**LocalDB example**
```bash
dotnet user-secrets set "ConnectionStrings:MoltbookPilotDb" "Server=(localdb)\MSSQLLocalDB;Database=MoltbookDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

> Tip (PowerShell): If quoting gets annoying, wrap the connection string in single quotes.

#### Optional secrets (recommended)

If your code supports it, store these too:

```bash
dotnet user-secrets set "LmStudio:BaseUrl" "http://localhost:1234/v1"
dotnet user-secrets set "Agent:Model" "your-local-model-id"
```

---

## Database setup

### Option A (recommended): EF Core migrations

If you have EF Core tooling installed and working:

```bash
dotnet ef migrations add InitMoltbookState -c MoltbookDbContext
dotnet ef database update -c MoltbookDbContext
```

### Option B: Create the table manually (SSMS)

If you prefer to create the table yourself:

```sql
CREATE TABLE dbo.MoltbookAgentState (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    AgentHandle     NVARCHAR(100) NULL,
    ClaimUrl        NVARCHAR(400) NULL,
    AgentApiKey     NVARCHAR(200) NULL,
    LastHeartbeatUtc DATETIME2(0) NULL,
    CreatedUtc      DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedUtc      DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);
```

---

## Run the app

```bash
dotnet run
```

Open the URL printed in the console (usually `https://localhost:xxxx`).

On the home page you can:

- **Ping LM Studio**: checks your local server
- **Ping API**: checks your backend
- **Test a Prompt**: sends a prompt to your local model
- **Join Moltbook**: runs registration + returns claim link
- **Refresh State**: reads state from SQL and displays it

---

## Moltbook onboarding flow

1. Click **Join Moltbook**
2. The app should return:
   - a **claim URL**
   - an **API key**
3. **Save** those values:
   - API key: store in SQL +/or user secrets (do not commit to Git)
4. Open the **claim URL** and complete verification (tweet/claim)
5. After verification, proceed to heartbeat + posting routines

---

## Security notes

- **Never commit**:
  - Moltbook API keys
  - Connection strings with credentials
- Prefer **User Secrets** locally, environment variables/secret store in production.

---

## Troubleshooting

### “Join Moltbook” button does nothing
- Open browser DevTools → **Console**
  - Fix any JavaScript syntax errors (a single error can prevent handlers from attaching)
- Check DevTools → **Network**
  - Confirm `POST /api/moltbook/join` is firing

### LM Studio ping fails from the browser
Some environments block cross-origin calls from the browser to `http://localhost:1234`.
If so, proxy the request through your ASP.NET backend (recommended) instead of calling LM Studio directly from the page.

### EF Core tooling errors
If `dotnet ef` errors out:
- Confirm EF Core packages are referenced in the `.csproj`
- Ensure the `dotnet-ef` tool version matches your EF Core package major version
- Use the **manual SQL option** above as a fallback

---

## Roadmap

- Heartbeat runner endpoint (`POST /api/moltbook/heartbeat`)
- Background service (runs heartbeat every ~4+ hours)
- Post/comment endpoints driven by heartbeat instructions
- Better redaction/masking in UI for sensitive values

---

## License

Licensed under the Apache License, Version 2.0. See the `LICENSE` file for details.

MoltbookPilot
Copyright (c) 2026 Gabriel Vogt

This product includes software developed by contributors.
Licensed under the Apache License, Version 2.0.
