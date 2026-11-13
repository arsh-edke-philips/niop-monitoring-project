# NIOP Monitoring & Operations Assistant

A monitoring dashboard and conversational assistant for NIOP inventory and order-processing operations. The project combines the existing ASP.NET Core dashboard with a Python-based AI agent so operations staff can inspect metrics, trace records, and ask questions about the same underlying SQL Server data.

## What is included

| Component | Purpose |
| --- | --- |
| **Operations dashboard** | Order volumes, pending/delayed orders, fulfillment and shipping metrics, distribution-center device counts, and device-flow durations. Interactive drilldowns show device, order, enrollment, report, prebuilt-kit, and transfer lineage. |
| **Operations assistant** | Natural-language record lookups, identifier discovery, related-record queries, totals, and grouped counts. Available through the dashboard chat panel or a terminal. |
| **Agent data API** | A catalog-driven .NET API exposing approved entities and fields from SQL Server, rather than allowing the model to generate arbitrary SQL. |

## How it works

```text
Dashboard browser --> ASP.NET Core MVC --> SQL Server
       |
       +--> .NET chat proxy --> Python FastAPI / Copilot SDK
                                      |
                                      +--> .NET agent data API --> SQL Server
```

The dashboard uses C# services and Dapper queries to build Razor view models. The agent integration adds `Agent\catalogue.json` and SQL query files defining available entities, columns, relationships, filters, and masking rules.

At startup, .NET validates the catalog against the database. Python then loads its public metadata to build the assistant's instructions and three tools: `query_entity`, `find_by_identifier`, and `count_entity`. The model selects tools; .NET validates their inputs and executes parameterized queries. Record lookups require filters and return at most 25 rows; larger results are flagged as truncated. Configured sensitive fields are masked in agent responses.

The dashboard proxies chat to Python and relays streamed text and result tables using Server-Sent Events (SSE). Conversation IDs are kept in the dashboard's server-side session.

**Technologies:** .NET 10 / ASP.NET Core MVC, Razor, Dapper, Microsoft.Data.SqlClient, SQL Server, Bootstrap, jQuery, and JavaScript; Python, GitHub Copilot SDK, FastAPI, Uvicorn, HTTPX, Pydantic Settings, and truststore.

## Repository layout

```text
NIOP_Monitoring\
  Controllers\, Services\, Models\   Dashboard logic and database queries
  Views\, wwwroot\                  Dashboard and chat interface
  Agent\                            Data API, catalog, and SQL definitions
Agent-Chatbot\
  app\                              Copilot tools, chat service, and FastAPI API
  cli.py                            Standalone terminal assistant
  chat_client.py                    Terminal client for the running chat API
  list_models.py                    List available Copilot models
  eval.py, evals\cases.json          Live-agent evaluation runner and cases
```

## Setup and run locally

The commands below use **PowerShell**, starting at the repository root.

### 1. Prerequisites

- .NET 10 SDK and Python 3.11+ (recommended).
- GitHub Copilot access and a compatible Copilot CLI available on `PATH`, authenticated with `copilot login`. The agent also accepts an optional `GITHUB_TOKEN` for SDK authentication.
- Network access and read permissions to an **existing NIOP/Novus SQL Server database**. This repository does not include database creation scripts or sample data. Some dashboard queries explicitly reference `[Novus].[dbo]`, so an empty database or a differently named copy is not sufficient without adapting those queries.

### 2. Configure and start the dashboard

Store local .NET settings in user secrets, replacing all placeholders. Use two different, strong random API keys.

```powershell
dotnet dev-certs https --trust
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<sql-server>;Database=Novus;Integrated Security=True;Encrypt=True" --project .\NIOP_Monitoring
dotnet user-secrets set "Agent:ApiKey" "<agent-data-key>" --project .\NIOP_Monitoring
dotnet user-secrets set "ChatAgent:BaseUrl" "http://127.0.0.1:8001" --project .\NIOP_Monitoring
dotnet user-secrets set "ChatAgent:ApiKey" "<chat-service-key>" --project .\NIOP_Monitoring
dotnet run --project .\NIOP_Monitoring --launch-profile https
```

Adjust the connection string for your organization's authentication and certificate requirements. Open **https://localhost:7150**. Use HTTPS: the dashboard's chat session cookie is configured as secure. Keep this terminal running.

The dashboard can be used without starting Python; its chat feature requires the next step. Database access is required even at startup because the agent catalog is validated then.

### 3. Configure and start the assistant

In a second terminal at the repository root:

```powershell
Set-Location .\Agent-Chatbot
py -3 -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe list_models.py
```

Create `Agent-Chatbot\.env` with the following content, choosing a model ID from the listing:

```dotenv
DOTNET_BASE_URL=https://localhost:7150
AGENT_API_KEY=<agent-data-key>
CHAT_API_KEY=<chat-service-key>
COPILOT_MODEL=<available-model-id>
```

`AGENT_API_KEY` must match .NET's `Agent:ApiKey`; `CHAT_API_KEY` must match `ChatAgent:ApiKey`. Set `DOTNET_BASE_URL` explicitly: the Python default uses HTTPS on port 5111, but the checked-in HTTPS launch profile uses **7150**. Python reads `.env` relative to its working directory, so run its commands from `Agent-Chatbot`.

With the dashboard already running, start the chat API:

```powershell
.\.venv\Scripts\python.exe -m uvicorn app.api:app --host 127.0.0.1 --port 8001
```

Check readiness from another terminal with `Invoke-RestMethod http://127.0.0.1:8001/health`; expect `status: ok`. Then open the dashboard's chat button. Use **New** to start a fresh conversation.

## Using the assistant

Ask questions such as "How many orders are there by status?", "Where is device `<serial-number>`?", or "Show reports for enrollment `<number>`", substituting real identifiers. Follow-up questions can use the current conversation's context. Ambiguous identifiers may trigger clarification; narrow the question when results are truncated.

For terminal use, run either command from `Agent-Chatbot`:

```powershell
.\.venv\Scripts\python.exe cli.py          # Direct agent; requires .NET, not FastAPI
.\.venv\Scripts\python.exe chat_client.py  # Client for the running FastAPI service
```

Type `exit` to leave. Optional: `.\.venv\Scripts\python.exe eval.py` runs the cases in `evals\cases.json` and writes results under `evals\results`. These are live evaluations requiring database/Copilot access, not offline unit tests; outcomes depend on available data and model behavior.

## Configuration and troubleshooting

- **Startup/database errors:** check the connection string, network/VPN access, database permissions, and catalog SQL compatibility with your schema.
- **Certificate errors:** trust the .NET development certificate and use `https://localhost:7150`. Python uses the OS certificate store through truststore; do not disable certificate verification.
- **401/503 errors:** verify both key pairs match. The data API expects `X-Agent-Key`; the Python chat API expects `X-Chat-Key`.
- **Chat unavailable:** ensure both processes are running and `ChatAgent:BaseUrl` points to Python. Agent diagnostics are written to `Agent-Chatbot\agent.log` by default.

Keep `.env`, tokens, and connection strings out of Git; `.env` is already ignored. Agent logs, evaluation output, and Copilot session storage may contain operational data: handle them according to your organization's data policies, and confirm that the selected model/service is approved for that data. API keys protect service-to-service calls; they are not a complete end-user authentication or production deployment solution.