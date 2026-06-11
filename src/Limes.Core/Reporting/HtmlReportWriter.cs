using System.Globalization;
using System.Text;
using Limes.Core.Domain;

namespace Limes.Core.Reporting;

/// <summary>
/// Renders an <see cref="AssessmentDeliverable"/> as a single, self-contained HTML document:
/// inline CSS, no external assets or network calls, safe to email or open from disk. All
/// partner-supplied and agent-produced text is HTML-encoded, and skilling links are restricted
/// to absolute http/https URLs so the artifact can't carry script or dangerous URI schemes.
/// </summary>
public static class HtmlReportWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Write(AssessmentDeliverable deliverable)
    {
        ArgumentNullException.ThrowIfNull(deliverable);

        var result = deliverable.Assessment;
        var sb = new StringBuilder();

        sb.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n");
        sb.Append("<meta charset=\"utf-8\" />\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />\n");
        sb.Append($"<title>Limes Assessment — {Enc(result.Partner.Name)}</title>\n");
        sb.Append("<style>\n").Append(Css).Append("\n</style>\n");
        sb.Append("</head>\n<body>\n");

        // Header
        sb.Append("<header>\n  <h1>Limes</h1>\n");
        sb.Append("  <span>AI Center of Excellence Readiness Assessment</span>\n</header>\n");
        sb.Append("<main>\n");

        WriteOverview(sb, deliverable);
        WritePillars(sb, result);
        WriteRoadmap(sb, deliverable.Roadmap);
        WriteSkilling(sb, deliverable.SkillingPlan);
        WriteRisks(sb, deliverable.RiskRegister);
        WriteFooter(sb, deliverable);

        sb.Append("</main>\n</body>\n</html>\n");
        return sb.ToString();
    }

    private static void WriteOverview(StringBuilder sb, AssessmentDeliverable deliverable)
    {
        var result = deliverable.Assessment;
        sb.Append("<section class=\"card\">\n");
        sb.Append("  <div class=\"index-wrap\">\n");
        sb.Append("    <div>\n");
        sb.Append($"      <div class=\"index-num\">{result.ReadinessIndex.ToString("0.00", Inv)}<small> / 5.00</small></div>\n");
        sb.Append("      <div class=\"muted\">Overall CoE Readiness Index</div>\n");
        sb.Append("    </div>\n");
        sb.Append($"    <span class=\"badge\">{Enc(result.OverallLevel.DisplayName())}</span>\n");
        sb.Append("  </div>\n");

        sb.Append($"  <h2 style=\"margin-top:18px\">{Enc(result.Partner.Name)}</h2>\n");
        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.Partner.Region)) meta.Add(Enc(result.Partner.Region));
        if (!string.IsNullOrWhiteSpace(result.Partner.Industry)) meta.Add(Enc(result.Partner.Industry));
        if (meta.Count > 0)
            sb.Append($"  <p class=\"partner-meta\">{string.Join(" · ", meta)}</p>\n");
        sb.Append("</section>\n");
    }

    private static void WritePillars(StringBuilder sb, AssessmentResult result)
    {
        sb.Append("<section class=\"card\">\n  <h2>Pillar scores</h2>\n");
        foreach (var p in result.PillarScores)
        {
            var pct = (Math.Clamp(p.Score, 1.0, 5.0) / 5.0 * 100).ToString("0.#", Inv);
            sb.Append("  <div class=\"bar-row\">\n");
            sb.Append($"    <span class=\"bar-label\">{Enc(p.Pillar.DisplayName())}</span>\n");
            sb.Append($"    <span class=\"bar-track\"><span class=\"bar-fill\" style=\"width:{pct}%\"></span></span>\n");
            sb.Append($"    <span class=\"bar-val\">{p.Score.ToString("0.00", Inv)} · {Enc(p.Level.DisplayName())}</span>\n");
            sb.Append("  </div>\n");
        }

        var withGaps = result.PillarScores.Where(p => p.Gaps.Count > 0).ToList();
        sb.Append("  <h3>Identified gaps</h3>\n");
        if (withGaps.Count == 0)
        {
            sb.Append("  <p class=\"muted\">No gaps flagged below the threshold.</p>\n");
        }
        else
        {
            foreach (var p in withGaps)
            {
                sb.Append($"  <div class=\"wave-title\">{Enc(p.Pillar.DisplayName())}</div>\n");
                sb.Append("  <ul class=\"gap-list\">\n");
                foreach (var gap in p.Gaps)
                    sb.Append($"    <li>{Enc(gap)}</li>\n");
                sb.Append("  </ul>\n");
            }
        }
        sb.Append("</section>\n");
    }

    private static void WriteRoadmap(StringBuilder sb, Roadmap? roadmap)
    {
        if (roadmap is not { Actions.Count: > 0 })
            return;

        sb.Append("<section class=\"card\">\n  <h2>Remediation roadmap</h2>\n");
        foreach (var wave in roadmap.Actions.GroupBy(a => a.Wave).OrderBy(g => g.Key))
        {
            sb.Append($"  <div class=\"wave-title\">Wave {wave.Key}</div>\n");
            foreach (var a in wave.OrderBy(a => a.Pillar))
            {
                sb.Append("  <div class=\"action\">\n");
                sb.Append($"    <div><strong>{Enc(a.Title)}</strong> <span class=\"prio\">· {Enc(a.Pillar.DisplayName())} · {Enc(a.Priority.ToString())}</span></div>\n");
                sb.Append($"    <div class=\"muted\">{Enc(a.Description)}</div>\n");
                if (a.DependsOn.Count > 0)
                    sb.Append($"    <div class=\"muted\">Depends on: {Enc(string.Join(", ", a.DependsOn))}</div>\n");
                if (a.Citations.Count > 0)
                    sb.Append($"    <div class=\"muted\">Grounding: {Enc(string.Join("; ", a.Citations))}</div>\n");
                sb.Append("  </div>\n");
            }
        }
        sb.Append("</section>\n");
    }

    private static void WriteSkilling(StringBuilder sb, SkillingPlan? skilling)
    {
        if (skilling is not { Recommendations.Count: > 0 })
            return;

        sb.Append("<section class=\"card\">\n  <h2>Skilling plan</h2>\n");
        sb.Append("  <table>\n    <thead><tr><th>Pillar</th><th>Gap</th><th>Microsoft Learn path</th><th>Role</th></tr></thead>\n    <tbody>\n");
        foreach (var r in skilling.Recommendations)
        {
            var safe = SafeUrl(r.Url);
            var pathCell = safe is null
                ? Cell(r.LearnPath)
                : $"<a href=\"{Enc(safe)}\" target=\"_blank\" rel=\"noopener noreferrer\">{Cell(r.LearnPath)}</a>";
            sb.Append("      <tr>");
            sb.Append($"<td>{Cell(r.Pillar.DisplayName())}</td>");
            sb.Append($"<td>{Cell(r.Gap)}</td>");
            sb.Append($"<td>{pathCell}</td>");
            sb.Append($"<td>{Cell(r.Role)}</td>");
            sb.Append("</tr>\n");
        }
        sb.Append("    </tbody>\n  </table>\n</section>\n");
    }

    private static void WriteRisks(StringBuilder sb, RiskRegister? risks)
    {
        if (risks is not { Risks.Count: > 0 })
            return;

        sb.Append("<section class=\"card\">\n  <h2>Risk register</h2>\n");
        sb.Append("  <table>\n    <thead><tr><th>Severity</th><th>Pillar</th><th>Risk</th><th>Mitigation</th></tr></thead>\n    <tbody>\n");
        foreach (var r in risks.Risks.OrderByDescending(r => r.Severity))
        {
            var sev = r.Severity.ToString();
            sb.Append("      <tr>");
            sb.Append($"<td><span class=\"pill sev-{Enc(sev)}\">{Enc(sev)}</span></td>");
            sb.Append($"<td>{Cell(r.Pillar.DisplayName())}</td>");
            sb.Append($"<td>{Cell(r.Title)}</td>");
            sb.Append($"<td>{Cell(r.Mitigation)}</td>");
            sb.Append("</tr>\n");
        }
        sb.Append("    </tbody>\n  </table>\n</section>\n");
    }

    private static void WriteFooter(StringBuilder sb, AssessmentDeliverable deliverable)
    {
        sb.Append("<section class=\"card footer\">\n");
        sb.Append($"  <p class=\"muted\">Mode: {Enc(deliverable.Mode.ToString())} · Generated: {Enc(deliverable.Assessment.GeneratedAtUtc.ToString("u", Inv))}</p>\n");
        if (!string.IsNullOrWhiteSpace(deliverable.KnowledgeSource))
            sb.Append($"  <p class=\"muted\">Grounding corpus: {Enc(deliverable.KnowledgeSource)}</p>\n");
        if (deliverable.PipelineTrace.Count > 0)
        {
            sb.Append("  <details>\n    <summary>Pipeline trace</summary>\n    <ol class=\"trace\">\n");
            foreach (var line in deliverable.PipelineTrace)
                sb.Append($"      <li>{Enc(line)}</li>\n");
            sb.Append("    </ol>\n  </details>\n");
        }
        sb.Append("</section>\n");
    }

    /// <summary>HTML-encodes a value, mapping it to an em dash when empty for table cells.</summary>
    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : Enc(value);

    /// <summary>Minimal HTML-entity encoding for safe inclusion in element content and attributes.</summary>
    private static string Enc(string? value) =>
        (value ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");

    /// <summary>
    /// Returns the URL only when it is an absolute http/https URI; otherwise <c>null</c>, so the
    /// caller emits plain text. Blocks <c>javascript:</c>, <c>data:</c>, and relative targets.
    /// </summary>
    private static string? SafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;
        return uri.AbsoluteUri;
    }

    private const string Css = """
        :root {
          --accent: #4F7A28; --accent-2: #7AB317; --accent-soft: #EDF3E3;
          --ink: #1f2937; --muted: #6b7280; --line: #e5e7eb; --bg: #f7f8f4; --card: #fff;
          --crit: #b91c1c; --high: #d97706; --med: #ca8a04; --low: #4b8b3b;
        }
        * { box-sizing: border-box; }
        body { margin: 0; font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
          color: var(--ink); background: var(--bg); line-height: 1.5; }
        header { background: var(--accent); color: #fff; padding: 18px 28px;
          display: flex; align-items: baseline; gap: 14px; }
        header h1 { font-size: 20px; margin: 0; font-weight: 700; letter-spacing: .3px; }
        header span { color: var(--accent-soft); font-size: 13px; }
        main { max-width: 1080px; margin: 0 auto; padding: 24px; }
        .card { background: var(--card); border: 1px solid var(--line); border-radius: 10px;
          padding: 20px; margin-bottom: 20px; box-shadow: 0 1px 2px rgba(0,0,0,.04); }
        h2 { font-size: 16px; margin: 0 0 14px; color: var(--accent); }
        h3 { font-size: 14px; margin: 18px 0 8px; }
        .muted { color: var(--muted); font-size: 12.5px; }
        .index-wrap { display: flex; align-items: center; gap: 24px; flex-wrap: wrap; }
        .index-num { font-size: 52px; font-weight: 800; color: var(--accent); line-height: 1; }
        .index-num small { font-size: 20px; color: var(--muted); font-weight: 600; }
        .badge { display: inline-block; background: var(--accent-soft); color: var(--accent);
          border-radius: 999px; padding: 5px 14px; font-weight: 700; font-size: 14px; }
        .partner-meta { font-size: 13px; color: var(--muted); margin: 4px 0 0; }
        .bar-row { display: grid; grid-template-columns: 220px 1fr 150px; gap: 12px;
          align-items: center; margin: 7px 0; }
        .bar-track { background: var(--accent-soft); border-radius: 6px; height: 18px; overflow: hidden; }
        .bar-fill { background: linear-gradient(90deg, var(--accent), var(--accent-2));
          height: 100%; border-radius: 6px; }
        .bar-label { font-size: 13px; }
        .bar-val { font-size: 12.5px; text-align: right; color: var(--muted); }
        .gap-list { margin: 4px 0 8px; padding-left: 18px; }
        .gap-list li { font-size: 12.5px; color: #7a3b12; }
        table { width: 100%; border-collapse: collapse; font-size: 12.5px; }
        th, td { text-align: left; padding: 8px 10px; border-bottom: 1px solid var(--line); vertical-align: top; }
        th { background: var(--accent-soft); color: var(--accent); font-weight: 700; }
        td a { color: var(--accent); }
        .wave-title { font-weight: 700; margin: 14px 0 4px; }
        .action { padding: 6px 0 8px; border-bottom: 1px solid var(--line); }
        .prio { color: var(--muted); font-size: 11.5px; }
        .pill { display: inline-block; padding: 2px 8px; border-radius: 999px;
          font-size: 11px; font-weight: 700; color: #fff; }
        .sev-Critical { background: var(--crit); } .sev-High { background: var(--high); }
        .sev-Medium { background: var(--med); } .sev-Low { background: var(--low); }
        details { margin-top: 8px; } summary { cursor: pointer; font-size: 13px; color: var(--accent); }
        .trace { font-size: 12px; color: var(--muted); margin: 8px 0 0; }
        .footer { background: transparent; border: 0; box-shadow: none; padding: 4px 0; }
        """;
}
