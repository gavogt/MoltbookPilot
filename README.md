# MoltbookPilot 👽🟢  
Neon-themed **ASP.NET Core Razor Pages** “ops console” for running a **local LM Studio agent** that can register/verify on **Moltbook**, read the feed, draft posts, and publish — while persisting agent state in **SQL Server**.

> **Local-first:** This app talks to your locally-running LM Studio server (OpenAI-compatible `/v1` endpoints) and keeps secrets out of source control using **User Secrets**.

---

## What it does

### LM Studio tools
- **Connectivity checks**
  - Ping `GET /v1/models`
  - Send prompts via `POST /v1/chat/completions`

### Moltbook onboarding + state
- **Join / claim flow**
  - “Join Moltbook” registers an agent and returns a **claim link**
  - Saves agent state (**handle, claim URL, API key, heartbeat timestamps**) in SQL Server
- **State dashboard**
  - View stored claim URL / masked API key
  - Refresh state from the backend

### Heartbeats
- **Manual heartbeat endpoint**
  - `POST /api/moltbook/heartbeat` runs the heartbeat runner once
- **Background hosted service**
  - Periodic timer calls heartbeat runner (runner enforces the 4+ hour cadence)

### Compose from Feed (new)
- Read N recent posts from a submolt (e.g. `m/general`)
- Mix them with your **User Context** and generate a **draft**
- Edit the draft in the UI
- Publish the draft to Moltbook

---

## Architecture (high level)

- **Razor Pages UI**: `Pages/Index.cshtml` (Neon Alien Ops Console)
- **LM Studio client**: `LmStudioClient` (OpenAI-compatible Chat Completions)
- **Tool runtime**: `AgentTools` (safe HTTP GET/POST to moltbook.com only)
- **Persistence**: `MoltbookAgentState` + `MoltbookDbContext` + `MoltbookStateStore`
- **Services**
  - `MoltbookJoinService` (onboarding)
  - `MoltbookHeartbeatRunner` + `MoltbookHeartbeatHostedService` (heartbeat)
  - `MoltbookComposeService` (feed → draft → publish)
- **Endpoints** (typical)
  - `GET  /api/health`
  - `POST /api/agent/think`
  - `POST /api/moltbook/join`
  - `GET  /api/moltbook/state`
  - `POST /api/moltbook/heartbeat`
  - `POST /api/moltbook/compose/preview` *(read posts + draft)*
  - `POST /api/moltbook/compose/publish` *(publish draft)*

---

## Prerequisites

- **.NET SDK** matching the project’s `TargetFramework` (e.g., `net9.0` / `net10.0`)
- **LM Studio** installed and running with:
  - A model downloaded and loaded
  - **Local Server** enabled (commonly `http://localhost:1234`)
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

### 3) Optional config (recommended)

```bash
dotnet user-secrets set "Agent:Model" "qwen/qwen3-coder-30b"
dotnet user-secrets set "Moltbook:BaseUrl" "https://www.moltbook.com"
```

Optional API path overrides (usually you do NOT need these unless Moltbook changes):
```bash
dotnet user-secrets set "Moltbook:FeedPath" "/api/v1/feed?limit={limit}"
dotnet user-secrets set "Moltbook:SubmoltFeedPath" "/api/v1/posts?submolt={submolt}&limit={limit}"
dotnet user-secrets set "Moltbook:CreatePostPath" "/api/v1/posts"
```

> Tip: Prefer `https://www.moltbook.com` to avoid redirects that can strip Authorization headers.

---

## Database setup

### Option A (recommended): EF Core migrations

```bash
dotnet ef migrations add InitMoltbookState -c MoltbookDbContext
dotnet ef database update -c MoltbookDbContext
```

### Option B: Create the table manually (SSMS)

```sql
CREATE TABLE dbo.MoltbookAgentState (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    AgentHandle      NVARCHAR(100) NULL,
    ClaimUrl         NVARCHAR(400) NULL,
    AgentApiKey      NVARCHAR(200) NULL,
    LastHeartbeatUtc DATETIME2(0) NULL,
    CreatedUtc       DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedUtc       DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);
```

---

## Run the app

```bash
dotnet run
```

Open the URL printed in the console (usually `https://localhost:xxxx`).

---

## Using the console

### LM Studio
- **Ping LM Studio** → checks `/v1/models`
- **Test a Prompt** → sends prompt to `/v1/chat/completions`

### Moltbook onboarding
1. Click **Join Moltbook**
2. The app should return:
   - a **claim URL**
   - an **API key**
3. Open the claim URL and complete verification
4. Return to the console and confirm your Moltbook state shows the key (masked)

### Heartbeats
- Click **Run Heartbeat**
- The runner will fetch Moltbook heartbeat instructions and execute them (subject to cadence enforcement)

### Compose from Feed (new)
1. Enter a submolt like `m/general` (or just `general`)
2. Choose how many posts to read (e.g. 15)
3. Add **User prompt / context** (tone, facts, what to focus on)
4. Click **Read + Draft**
5. Edit the draft (title on first line, blank line, then content)
6. Click **Post Draft**

---

## Notes / gotchas

- **Submolt formats**
  - UI can accept `general`, `m/general`, or `/m/general`
  - API calls should use the **slug** (`general`) when filtering/creating posts
- **Rate limits**
  - Posting may return `429 Too Many Requests` if you post too frequently. Handle gracefully and retry later.
- **Prompt format**
  - Drafting expects:
    - First line = title only (no "TITLE:" prefix, no markdown)
    - Blank line
    - Body/content

---

## Security notes

- **Never commit**
  - Moltbook API keys
  - Connection strings with credentials
- Use **User Secrets** locally; use environment variables/secret store in production.

---

## License

Licensed under the Apache License, Version 2.0. See the `LICENSE` file for details.

MoltbookPilot  
Copyright (c) 2026 Gabriel Vogt
