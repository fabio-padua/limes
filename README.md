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

- **Deterministic mode** *(shipped — Phase 1)* — a pure C# rules engine. **$0 model cost.** Ideal for demos, CI, and dry runs.
- **Agents mode** *(Phase 2)* — a pipeline of Azure AI Foundry agents for adaptive interviewing and reasoned narrative.

## The agent pantheon *(Phase 2)*

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

# Run a deterministic assessment on the sample intake
dotnet run --project src/Limes.Orchestrator -- samples/sample-intake.json out
```

Outputs `assessment-<partner>.json` and `assessment-<partner>.md` into the chosen output directory.

## Repository layout

```
Limes.sln
src/
  Limes.Core/          # Domain models, deterministic scoring, intake, reporting
  Limes.Orchestrator/  # CLI entrypoint (deterministic mode; agents mode in Phase 2)
tests/
  Limes.Core.Tests/    # xUnit tests for the scoring engine
samples/               # Example intake JSON
knowledge/             # Reference-knowledge corpus that grounds the agents (Minerva)
docs/                  # Architecture & plan
```

## Roadmap

- **Phase 1 — Deterministic MVP** ✅ scoring engine, JSON/Markdown reports, CI eval gate
- **Phase 2 — Agents mode** — MAF + Foundry pipeline, `.docx`/`.pptx` deliverables
- **Phase 3 — Grounding & dashboard** — Microsoft Learn MCP / RAG, benchmarking
- **Phase 4 — Partner packaging** — web intake UI, clone-and-rebrand

See [`docs/architecture-and-plan.md`](docs/architecture-and-plan.md) for the full design.

## License

MIT
