using System.Text;
using Limes.Agents.Pipeline;
using Limes.Core.Domain;

namespace Limes.Agents.Maf;

/// <summary>
/// System instructions (the prompt-specialist persona for each Foundry agent) and per-stage
/// user-prompt builders. In agents mode the deterministic agent computes the authoritative
/// structured result and the MAF agent is asked to produce a grounded narrative on top of it.
/// </summary>
public static class AgentPrompts
{
    public const string Janus =
        "You are Janus, the intake assessor for the Limes AI CoE assessment. You normalize a " +
        "partner's self-assessment across Microsoft's 7 AI Readiness pillars and surface where " +
        "follow-up questions are needed. Be concise and neutral.";

    public const string Iustitia =
        "You are Iustitia, the scoring analyst for the Limes AI CoE assessment. The 1-5 maturity " +
        "scores are computed deterministically and are authoritative — never invent or change a " +
        "score. Explain the rationale behind the scores and the most material gaps, grounded in " +
        "the reference knowledge.";

    public const string Providentia =
        "You are Providentia, the roadmap planner for the Limes AI CoE assessment. Given the " +
        "deterministic remediation actions, explain the sequencing rationale (quick wins vs " +
        "strategic bets) and dependencies. Cite CAF/WAF guidance. Do not invent new actions.";

    public const string Egeria =
        "You are Egeria, the skilling counselor for the Limes AI CoE assessment. Explain how the " +
        "recommended Microsoft Learn paths close the organization's literacy and role gaps. Keep " +
        "it practical and role-based.";

    public const string Terminus =
        "You are Terminus, the risk and governance officer for the Limes AI CoE assessment. " +
        "Explain the registered risks and mitigations, anchored to the Responsible AI Standard " +
        "and WAF security/governance guidance. Do not overstate severity.";

    public const string Fama =
        "You are Fama, the report writer for the Limes AI CoE assessment. Produce a crisp, " +
        "executive-ready summary of the partner's readiness, top gaps, and recommended next " +
        "steps. Professional and partner-facing.";

    public static string ScoringPrompt(AssessmentContext ctx)
    {
        var s = ctx.RequireScoring();
        var sb = new StringBuilder();
        sb.AppendLine($"Partner: {s.Partner.Name}. Overall Readiness Index: {s.ReadinessIndex:0.00}/5.00 ({s.OverallLevel}).");
        sb.AppendLine("Pillar scores:");
        foreach (var p in s.PillarScores)
            sb.AppendLine($"- {p.Pillar.DisplayName()}: {p.Score:0.00} ({p.Level}), {p.Gaps.Count} gap(s).");
        sb.AppendLine();
        sb.AppendLine("Write a 3-5 sentence rationale for these scores and call out the most material gaps.");
        return sb.ToString();
    }

    public static string RoadmapPrompt(AssessmentContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Partner: {ctx.RequireScoring().Partner.Name}. Proposed remediation actions:");
        foreach (var a in ctx.Roadmap?.Actions ?? [])
            sb.AppendLine($"- [Wave {a.Wave}, {a.Priority}] {a.Title}");
        sb.AppendLine();
        sb.AppendLine("Explain the sequencing rationale and dependencies in 3-5 sentences.");
        return sb.ToString();
    }

    public static string SkillingPrompt(AssessmentContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Partner: {ctx.RequireScoring().Partner.Name}. Recommended Microsoft Learn paths:");
        foreach (var r in ctx.SkillingPlan?.Recommendations ?? [])
            sb.AppendLine($"- {r.Pillar.DisplayName()} ({r.Role}): {r.LearnPath}");
        sb.AppendLine();
        sb.AppendLine("Explain how these close the literacy/role gaps in 2-4 sentences.");
        return sb.ToString();
    }

    public static string RiskPrompt(AssessmentContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Partner: {ctx.RequireScoring().Partner.Name}. Registered risks:");
        foreach (var r in ctx.RiskRegister?.Risks ?? [])
            sb.AppendLine($"- [{r.Severity}] {r.Title} ({r.Pillar.DisplayName()})");
        sb.AppendLine();
        sb.AppendLine("Summarize the risk posture and priority mitigations in 2-4 sentences.");
        return sb.ToString();
    }

    public static string ReportPrompt(AssessmentContext ctx)
    {
        var s = ctx.RequireScoring();
        return $"Partner {s.Partner.Name} scored {s.ReadinessIndex:0.00}/5.00 ({s.OverallLevel}) overall, " +
               $"with {ctx.Roadmap?.Actions.Count ?? 0} remediation action(s) and " +
               $"{ctx.RiskRegister?.Risks.Count ?? 0} risk(s). Write a short executive summary.";
    }
}
