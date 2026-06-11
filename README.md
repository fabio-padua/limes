# Limes — AI Frontier CoE Assessor

**Limes** (Latin: *the fortified frontier of Rome*) is a partner-reusable, multi-agent assessment platform that scores a partner's **AI Center of Excellence readiness** across Microsoft's **seven AI Readiness pillars** — turning a self-assessment into a customer-ready draft deliverable in **hours instead of weeks**.

Built for the **AI CoE Frontier v-team** to help partners across the Americas reach Microsoft's AI Frontier CoE bar.

> *Cross the AI frontier.*

Built with **C# / .NET 9**, **Microsoft Agent Framework (MAF)**, and **Azure AI Foundry**.

---

## The seven pillars

Limes scores maturity (1–5) against Microsoft's AI Readiness model:

1. **Business Strategy**
2. **AI Strategy & Experience**
3. **Organization & Culture**
4. **Data Foundations**
5. **Infrastructure for AI**
6. **Model Management**
7. **AI Governance & Security**

Each pillar is scored `Initial → Developing → Defined → Managed → Optimized`, weighted (configurable per region/industry), and rolled up into an overall **CoE Readiness Index**.

## Run modes

- **Deterministic mode** *(shipped)* — a pure C# rules engine. **$0 model cost.** Ideal for demos, CI, and dry runs. Produces the full deliverable (scores + roadmap + skilling + risk register).
- **Agents mode** *(Phase 2 — scaffolded)* — the same six-stage pipeline backed by Azure AI Foundry agents (Microsoft Agent Framework). Each stage runs its deterministic counterpart for authoritative, reproducible structured output, then layers grounded narrative on top — so scores are never hallucinated. Falls back cleanly to deterministic results if the model call fails.

## The agent pantheon

The pipeline runs `Janus → Iustitia → Providentia → Egeria → Terminus → Fama`, grounded by Minerva:

| Stage | Codename | Domain |
| --- | --- | --- |
| Intake / Assessor | **Janus** | doorways; present ↔ future |
| Scoring & Gap | **Iustitia** | the scales of maturity |
| Roadmap | **Providentia** | foresight |
| Skilling | **Egeria** | the counselor |
| Risk & Governance | **Terminus** | boundaries |
| Report | **Fama** | renown |
| Knowledge (RAG) | **Minerva** | wisdom |

## Quickstart

```bash
# Build
dotnet build Limes.sln

# Run tests
dotnet test Limes.sln

# Run a deterministic assessment on the sample intake ($0 model cost)
dotnet run --project src/Limes.Orchestrator -- samples/sample-intake.json out

# Run the agents-mode pipeline (requires Azure AI Foundry)
export LIMES_FOUNDRY_ENDPOINT="https://<resource>.openai.azure.com/"
export LIMES_FOUNDRY_DEPLOYMENT="gpt-4.1-mini"
dotnet run --project src/Limes.Orchestrator -- samples/sample-intake.json out \
  --mode agents --knowledge knowledge/ai-coe-knowledge.md
```

Agents mode authenticates with `DefaultAzureCredential` (managed identity / `az login`) — no keys in source. The `--knowledge` corpus is prompt-injected (and content-hashed) to ground the agents.

Outputs four artifacts per run — `assessment-<partner>.json`, `.md`, a branded Word report (`.docx`), and a PowerPoint executive summary (`.pptx`). The `.docx`/`.pptx` are produced with the Open XML SDK, so no Office install is required (CI- and cloud-job-friendly). The **intake** argument accepts a local file path or a blob URL pointing at a single blob (`https://<account>.blob.core.windows.net/<container>/<blob>`). The **output** argument accepts a local directory or a container URL, optionally with a prefix (`https://<account>.blob.core.windows.net/<container>[/<prefix>]`), under which the report files are written. The same binary therefore runs locally or as a cloud job.

### Web UI

For a point-and-click demo, `Limes.Web` is a small ASP.NET Core app that runs the deterministic pipeline behind a browser UI — paste, upload, or load the bundled sample intake, see the Readiness Index, pillar scores, gaps, roadmap, skilling plan, and risk register, then download any of the four artifacts.

```bash
dotnet run --project src/Limes.Web
# then open the printed URL (e.g. http://localhost:5xxx)
```

It calls the same `Limes.Core`/`Limes.Agents` engine, so it stays $0 model cost with no Azure required. Key endpoints: `POST /api/assess` (intake JSON → scored result) and `GET /api/assessments/{id}/download/{json|md|docx|pptx}`.

## Deploy to Azure (`azd`)

Agents mode runs as a **Container Apps Job** (batch, scales to zero) against an Azure AI Foundry model deployment, reading intake from Blob Storage and writing reports back to Blob. One command provisions everything and deploys the orchestrator image:

```bash
azd auth login
azd up   # prompts for an environment name, region, and subscription
```

`azd up` provisions (see [`infra/`](infra/)): Azure AI Foundry (account + **`gpt-5.2`** deployment), a Container Apps environment + Job, Container Registry, Storage (with `intake`/`reports` containers), Log Analytics + Application Insights, and a user-assigned managed identity wired with least-privilege RBAC (`Cognitive Services OpenAI User`, `Storage Blob Data Contributor`, `AcrPull`). It also grants **your** principal data-plane access so you can run locally against the same Foundry endpoint. All auth is **Entra ID only** — local keys are disabled on both Foundry and Storage.

Override the model without editing Bicep:

```bash
azd env set LIMES_CHAT_MODEL gpt-4o-mini          # if your subscription lacks gpt-5.2 quota
azd env set LIMES_CHAT_MODEL_VERSION 2024-07-18
azd env set LIMES_AI_LOCATION eastus2             # pin the model region
```

Run an assessment by starting the job (it reads `samples`-style intake from the `intake` container):

```bash
# Upload an intake, then start the job
az storage blob upload --account-name <st-account> --auth-mode login \
  -c intake -n sample-intake.json -f samples/sample-intake.json
az containerapp job start -g <rg> -n <job-name>
```

The job name and storage account are emitted as `azd` outputs (`LIMES_JOB_NAME`, `AZURE_STORAGE_ACCOUNT`) and saved to `.azure/<env>/.env`.

## Repository layout

```
Limes.sln
src/
  Limes.Core/          # Domain models, deterministic scoring, intake, reporting
  Limes.Agents/        # Agent pipeline (MAF): Janus → … → Fama + Minerva grounding
  Limes.Orchestrator/  # CLI entrypoint (--mode deterministic|agents) + Blob I/O
  Limes.Web/           # ASP.NET Core web UI + API (browser demo, deterministic mode)
tests/
  Limes.Core.Tests/    # xUnit tests for the scoring engine
  Limes.Agents.Tests/  # xUnit tests for the deterministic pipeline
  Limes.Web.Tests/     # xUnit integration tests for the web API endpoints
samples/               # Example intake JSON
knowledge/             # Reference-knowledge corpus that grounds the agents (Minerva)
infra/                 # azd Bicep: Foundry, Container Apps Job, Storage, ACR, RBAC
docs/                  # Architecture & plan
```

## Roadmap

- **Phase 1 — Deterministic MVP** ✅ scoring engine, JSON/Markdown reports, CI eval gate
- **Phase 2 — Agents mode** 🚧 MAF + Foundry pipeline scaffolded (deterministic fallback);
  `azd` + Foundry infra ✅ (Container Apps Job, Blob intake/reports); branded `.docx`/`.pptx` exec deliverables ✅
- **Phase 3 — Grounding & dashboard** — Microsoft Learn MCP / RAG, benchmarking
- **Phase 4 — Partner packaging** — web intake UI ✅ (`Limes.Web`), clone-and-rebrand

See [`docs/architecture-and-plan.md`](docs/architecture-and-plan.md) for the full design.

## License

MIT
