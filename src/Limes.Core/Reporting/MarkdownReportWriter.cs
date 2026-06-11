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
}
