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

Outputs `assessment-<partner>.json` and `assessment-<partner>.md` into the chosen output directory.

## Repository layout

```
Limes.sln
src/
  Limes.Core/          # Domain models, deterministic scoring, intake, reporting
  Limes.Agents/        # Agent pipeline (MAF): Janus → … → Fama + Minerva grounding
  Limes.Orchestrator/  # CLI entrypoint (--mode deterministic|agents)
tests/
  Limes.Core.Tests/    # xUnit tests for the scoring engine
  Limes.Agents.Tests/  # xUnit tests for the deterministic pipeline
samples/               # Example intake JSON
knowledge/             # Reference-knowledge corpus that grounds the agents (Minerva)
docs/                  # Architecture & plan
```

## Roadmap

- **Phase 1 — Deterministic MVP** ✅ scoring engine, JSON/Markdown reports, CI eval gate
- **Phase 2 — Agents mode** 🚧 MAF + Foundry pipeline scaffolded (deterministic fallback);
  next: `azd` + Foundry infra, `.docx`/`.pptx` deliverables
- **Phase 3 — Grounding & dashboard** — Microsoft Learn MCP / RAG, benchmarking
- **Phase 4 — Partner packaging** — web intake UI, clone-and-rebrand

See [`docs/architecture-and-plan.md`](docs/architecture-and-plan.md) for the full design.

## License

MIT
