# Limes — AI Frontier CoE Assessor — Architecture & Plan

**Product name:** **Limes** (Latin: *the fortified frontier of Rome*) — the AI Frontier CoE Assessor for the Americas
**Owner:** AI CoE Frontier v-team, Microsoft
**Status:** Draft architecture / plan — no code yet (greenfield build)
**Author of draft:** Copilot, with Fabio Padua

---

## 0. Branding — *Limes*

**Limes** (pronounced *LEE-mess*) was the fortified frontier line of the Roman Empire — the edge of the known, defended world. It's the perfect metaphor for the **AI Frontier CoE**: Limes assesses where a partner stands today and guides them across the frontier to the bar of excellence.

- **Tagline candidates:** *"Cross the AI frontier."* / *"From here to the Frontier."*
- **Note on the name:** in English it reads like the fruit "limes" — lean into that visually if useful (a citrus-green accent), or always pair the wordmark with the *frontier-line* meaning so the Roman root stays front-and-center.
- **Agent pantheon:** each agent carries a Roman-deity codename whose domain mirrors its job (see §4) — a coherent, brandable family under the Limes banner.

---

## 1. Why this exists

The AI CoE Frontier v-team needs to help **every partner across the Americas** reach Microsoft's AI Frontier Center of Excellence bar. Today that means bespoke workshops, manual maturity scoring, and one-off PowerPoint decks per partner — inconsistent, slow, and impossible to scale across a whole region.

The opportunity is **not** "an AI that replaces the CoE architect." It's **an AI that gives the architect a strong, well-reasoned first draft** of a partner's AI CoE maturity assessment — so the human spends their time validating, advising, and selling the transformation, not parsing survey answers and building decks.

This is a **brand-new, greenfield** partner-reusable, multi-agent assessment system, built specifically for the AI CoE Frontier mission: turn a partner's self-assessment into a customer-ready draft deliverable in **hours instead of weeks**, scored against Microsoft's official AI Readiness pillars.

---

## 2. The assessment backbone — Microsoft's 7 AI Readiness pillars

The entire scoring model maps 1:1 to Microsoft's official **AI Readiness** framework so output is defensible and aligned to the AI CoE guidance and the Cloud Adoption Framework "Establish an AI Center of Excellence" guidance.

| # | Pillar | What we assess |
| --- | --- | --- |
| 1 | **Business Strategy** | AI initiatives tied to business value; prioritized use cases; executive sponsorship |
| 2 | **AI Strategy & Experience** | Maturity of the adoption roadmap; AI lifecycle governance; monitoring & continuous improvement |
| 3 | **Organization & Culture** | Org readiness, AI literacy, roles/responsibilities, change management, skilling plans |
| 4 | **Data Foundations** | Data governance, quality, integration, security/privacy/compliance |
| 5 | **Infrastructure for AI** | Scalable architecture (AI Foundry, Azure WAF), platform readiness |
| 6 | **Model Management** | Model lifecycle, deployment, monitoring, MLOps/LLMOps |
| 7 | **AI Governance & Security** | Responsible AI, accountability, transparency, risk management, controls |

Each pillar is scored on a **1–5 maturity scale** (Initial → Developing → Defined → Managed → Optimized), with weighting configurable per region/industry. Pillar scores roll up into an overall **CoE Readiness Index**.

---

## 3. What it does

**Input:** a partner's responses to a guided, adaptive questionnaire across the 7 pillars. Optional supporting artifacts: existing architecture docs, governance policies, current-state slides (`.docx`/`.pptx`/`.pdf`), or a pre-filled intake spreadsheet (`.xlsx`).

**Output:** a draft AI CoE assessment deliverable containing:

1. A **normalized response set** (deduped, gaps flagged) per pillar.
2. A **per-pillar maturity score (1–5)** with evidence-cited rationale and identified gaps.
3. An overall **CoE Readiness Index** + radar/heatmap visualization.
4. A **prioritized remediation roadmap** — wave-based, dependency-aware, each action grounded in CAF / WAF / Microsoft Learn.
5. A **skilling plan** (Microsoft Learn paths) mapped to the Organization & Culture gaps.
6. A **risk register** anchored to Responsible AI + WAF security/governance.

**Format:** structured **JSON** (downstream tooling), human-readable **Markdown** summary, plus polished \*\*Word (`.docx`)\*\* and **PowerPoint (`.pptx`)** exec deliverables — selectable via `--formats` (default: all).

**Two run modes:**

- **Deterministic mode** — pure rules engine, **$0 model cost**. Ideal for demos, CI, and dry runs.
- **Agents mode** — multi-agent LLM pipeline for adaptive interviewing and reasoned narrative.

---

## 4. The agents

Built with **Microsoft Agent Framework (MAF)** + **Azure AI Foundry**, in **C# / .NET**.

A pipeline of specialized Foundry agents, each owning one stage of the assessment.

| Agent (role) | Codename | Responsibility |
| --- | --- | --- |
| **Intake / Assessor** | **Janus** (doorways; present↔future) | Conducts the adaptive interview across the 7 pillars; asks targeted follow-ups to fill gaps; normalizes responses into a canonical `PillarResponse` schema |
| **Scoring & Gap** | **Iustitia** (the scales) | Assigns a 1–5 maturity score per pillar with cited rationale; identifies concrete gaps vs the target CoE bar |
| **Roadmap** | **Providentia** (foresight) | Produces a prioritized, dependency-aware remediation plan; sequences quick wins vs strategic bets; cites CAF/WAF |
| **Skilling** | **Egeria** (the counselor) | Maps Organization & Culture gaps to Microsoft Learn paths and role-based skilling tracks |
| **Risk & Governance** | **Terminus** (boundaries) | Builds a risk register anchored to Responsible AI + WAF security/governance pillars |
| **Report** | **Fama** (renown) | Assembles JSON + Markdown + `.docx` + `.pptx` exec deliverables |
| **Knowledge (RAG)** | **Minerva** (wisdom) | Grounds the other agents on the downloaded official AI CoE assets via citable retrieval |

### Grounding strategy

Start lean with **prompt-level grounding** — agents are prompt specialists; the AI CoE guidance reaches them as instruction text + a prompt-injected `REFERENCE KNOWLEDGE` block built from the downloaded official assets (content-hashed for drift detection). **Upgrade path:** wire a **Microsoft Learn MCP** + Foundry knowledge source for live, citable retrieval once the corpus stabilizes. This keeps v1 cheap and shippable while leaving a clean enterprise overlay.

> **Action item:** download the official AI CoE collection from partner.microsoft.com and the "Implementing an AI Center of Excellence" e-book to seed the reference-knowledge corpus. Fabio to attach.

---

## 5. Architecture

```javascript
Partner SE (laptop / portal)
        │  guided questionnaire + optional artifacts
        ▼
  Intake (blob/inbox container or web intake UI)
        │  event-driven trigger
        ▼
  Orchestrator (.NET 9, MAF)  ──►  Foundry agents (Intake→Scoring→Roadmap→Skilling→Risk→Report)
        │                                   ▲
        │                                   │ prompt-injected reference knowledge
        ▼                                   │ (downloaded AI CoE assets; MCP/RAG later)
  Deliverables: assessment-*.{json,md,docx,pptx}
        │  reports/<partner-slug>/<timestamp>/
        ▼
  Human reviewer (review gate) ──► Customer-ready CoE assessment
```

**What lives in Azure (lean tier, consumption-first):** Azure AI Foundry (account + project + model deployment), Container Apps Job (orchestrator, scales to zero), Storage (intake/reports/archive), Container Registry, Log Analytics + App Insights, Managed Identity. Provisioned with \*\*`azd up`\*\* from the partner's own subscription.

**Optional enterprise overlays (off by default):** Azure AI Search (RAG), Cosmos DB (response/benchmark persistence), API Management, Document Intelligence (PDF artifact ingestion), web intake front-end.

**Estimated cost:** standing infra \~**$5–15/month** (registry-dominated, scales to zero); per-assessment model spend well under **$2** on GPT-4.1, \~5× cheaper on `-mini`.

---

## 6. How this makes regional impact

1. **Standardize & scale** — one repeatable, Microsoft-aligned assessment replaces bespoke decks → cover *all* Americas partners with consistent quality.
2. **Speed to value** — hours not weeks; roadmap + skilling plan auto-generated and grounded in official assets.
3. **Benchmarking moat** — aggregate anonymized pillar scores → a regional AI-maturity dataset and cohort benchmarking no one else in the field has.
4. **Partner-reusable** — ship it so any partner can clone, plug in their own branding/weighting, and self-serve.
5. **Closed loop** — re-run periodically; dashboard tracks each partner's Readiness Index over time → proves the v-team's impact with hard KPIs.

---

## 7. Phased delivery plan

### Phase 0 — Foundation (Week 1–2)

- Confirm the 7-pillar question bank + maturity rubric (1–5 per pillar) with the v-team.
- Download & curate the official AI CoE corpus → `knowledge/ai-coe-knowledge.md`.
- Scaffold the greenfield repo (orchestrator, schema, infra `azd`, CI).
- Build a **golden dataset** of labelled sample assessments for the eval gate.

### Phase 1 — Deterministic MVP (Week 3–5)

- Canonical `PillarResponse` + scoring schema.
- Deterministic rules engine → scores + gaps + JSON/Markdown output. **$0 model cost**, fully testable in CI.
- Eval gate: scoring accuracy vs golden dataset on every PR.

### Phase 2 — Agents mode (Week 6–9)

- Stand up Foundry + the orchestrator and `azd` infra.
- Implement the agent pipeline (Intake → Scoring → Roadmap → Skilling → Risk → Report).
- `.docx` + `.pptx` exec deliverables.

### Phase 3 — Grounding & dashboard (Week 10–13)

- Prompt-injected reference knowledge → then Microsoft Learn MCP / Foundry RAG upgrade.
- Benchmarking dashboard (Readiness Index over time, cohort comparison).
- Responsible AI / groundedness scoring in the eval runner.

### Phase 4 — Partner packaging (Week 14+)

- Web intake UI (optional).
- Clone-and-rebrand docs, pricing/weighting config, partner quickstart.

---

## 8. Open decisions (for the v-team)

**Locked:** Stack = **C#/.NET + Microsoft Agent Framework (MAF)** on Azure AI Foundry.

1. **Intake UX:** **Locked → Both.** File-drop intake for v1 (upload a pre-filled spreadsheet/doc → event-driven run), with an interactive agent-driven web questionnaire as a fast follow.
2. **Persistence:** stateless per-run (v1) vs Cosmos DB for longitudinal benchmarking from day one.
3. **Question bank ownership:** who signs off the official rubric mapping to the 7 pillars.
4. **Repo location:** new repo under the v-team org/account; suggested name \*\*`limes`\*\* — to confirm.

---

## 9. Risks & mitigations

| Risk | Mitigation |
| --- | --- |
| Scoring perceived as "Microsoft grading partners" | Frame as self-assessment + advisory draft; human review gate before any partner sees output |
| Hallucinated recommendations | Start deterministic; prompt-injected official knowledge; planned groundedness eval |
| Corpus drift (guidance updates) | Content-hash the knowledge file; drift detection republishes agents |
| Data privacy of partner responses | Partner-owned subscription deploy; anonymize before any benchmarking aggregation |
| Scope creep across 7 pillars | Ship deterministic MVP first; agents/dashboard are additive phases |