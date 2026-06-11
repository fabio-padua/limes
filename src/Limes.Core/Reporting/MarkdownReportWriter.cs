using System.Globalization;
using System.Text;
using Limes.Core.Domain;

namespace Limes.Core.Reporting;

/// <summary>Renders an <see cref="AssessmentResult"/> as a human-readable Markdown summary.</summary>
public static class MarkdownReportWriter
{
    public static string Write(AssessmentResult result)
    {
        var sb = new StringBuilder();
        var c = CultureInfo.InvariantCulture;

        sb.AppendLine($"# Limes — AI CoE Readiness Assessment");
        sb.AppendLine();
        sb.AppendLine($"**Partner:** {result.Partner.Name}");
        if (!string.IsNullOrWhiteSpace(result.Partner.Region))
            sb.AppendLine($"**Region:** {result.Partner.Region}");
        if (!string.IsNullOrWhiteSpace(result.Partner.Industry))
            sb.AppendLine($"**Industry:** {result.Partner.Industry}");
        sb.AppendLine($"**Generated:** {result.GeneratedAtUtc.ToString("u", c)}");
        sb.AppendLine();
        sb.AppendLine($"## Overall CoE Readiness Index: {result.ReadinessIndex.ToString("0.00", c)} / 5.00 — {result.OverallLevel.DisplayName()}");
        sb.AppendLine();

        sb.AppendLine("## Pillar scores");
        sb.AppendLine();
        sb.AppendLine("| Pillar | Score | Maturity |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var p in result.PillarScores)
        {
            sb.AppendLine($"| {p.Pillar.DisplayName()} | {p.Score.ToString("0.00", c)} | {p.Level.DisplayName()} |");
        }
        sb.AppendLine();

        var pillarsWithGaps = result.PillarScores.Where(p => p.Gaps.Count > 0).ToList();
        sb.AppendLine("## Identified gaps");
        sb.AppendLine();
        if (pillarsWithGaps.Count == 0)
        {
            sb.AppendLine("_No gaps flagged below the threshold._");
        }
        else
        {
            foreach (var p in pillarsWithGaps)
            {
                sb.AppendLine($"### {p.Pillar.DisplayName()}");
                foreach (var gap in p.Gaps)
                {
                    sb.AppendLine($"- {gap}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>Renders the full Phase 2 deliverable: scores, roadmap, skilling, risk, and pipeline trace.</summary>
    public static string Write(AssessmentDeliverable deliverable)
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.Append(Write(deliverable.Assessment));

        if (deliverable.Roadmap is { Actions.Count: > 0 } roadmap)
        {
            sb.AppendLine("## Remediation roadmap (Providentia)");
            sb.AppendLine();
            foreach (var wave in roadmap.Actions.GroupBy(a => a.Wave).OrderBy(g => g.Key))
            {
                sb.AppendLine($"### Wave {wave.Key}");
                sb.AppendLine();
                foreach (var a in wave.OrderBy(a => a.Pillar))
                {
                    sb.AppendLine($"- **{a.Title}** ({a.Pillar.DisplayName()}, {a.Priority})");
                    sb.AppendLine($"  - {a.Description}");
                    if (a.DependsOn.Count > 0)
                        sb.AppendLine($"  - Depends on: {string.Join(", ", a.DependsOn)}");
                    if (a.Citations.Count > 0)
                        sb.AppendLine($"  - Grounding: {string.Join("; ", a.Citations)}");
                }
                sb.AppendLine();
            }
        }

        if (deliverable.SkillingPlan is { Recommendations.Count: > 0 } skilling)
        {
            sb.AppendLine("## Skilling plan (Egeria)");
            sb.AppendLine();
            sb.AppendLine("| Pillar | Gap | Microsoft Learn path | Role |");
            sb.AppendLine("| --- | --- | --- | --- |");
            foreach (var r in skilling.Recommendations)
            {
                var path = string.IsNullOrWhiteSpace(r.Url) ? r.LearnPath : $"[{r.LearnPath}]({r.Url})";
                sb.AppendLine($"| {r.Pillar.DisplayName()} | {r.Gap} | {path} | {r.Role ?? "—"} |");
            }
            sb.AppendLine();
        }

        if (deliverable.RiskRegister is { Risks.Count: > 0 } risks)
        {
            sb.AppendLine("## Risk register (Terminus)");
            sb.AppendLine();
            sb.AppendLine("| Severity | Pillar | Risk | Mitigation |");
            sb.AppendLine("| --- | --- | --- | --- |");
            foreach (var r in risks.Risks.OrderByDescending(r => r.Severity))
            {
                sb.AppendLine($"| {r.Severity} | {r.Pillar.DisplayName()} | {r.Title} | {r.Mitigation} |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"_Mode: {deliverable.Mode} · Generated: {deliverable.GeneratedAtUtc.ToString("u", c)}_");
        if (!string.IsNullOrWhiteSpace(deliverable.KnowledgeSource))
            sb.AppendLine($"_Grounding corpus: {deliverable.KnowledgeSource}_");
        if (deliverable.PipelineTrace.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<details><summary>Pipeline trace</summary>");
            sb.AppendLine();
            foreach (var line in deliverable.PipelineTrace)
                sb.AppendLine($"- {line}");
            sb.AppendLine();
            sb.AppendLine("</details>");
        }

        return sb.ToString();
    }
}
