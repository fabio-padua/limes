using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Limes.Core.Domain;

namespace Limes.Core.Reporting;

/// <summary>
/// Renders the full Phase 2 <see cref="AssessmentDeliverable"/> as a branded Microsoft Word
/// (.docx) document using the Open XML SDK — no Office install required, so it runs the same in
/// CI and in the Container Apps Job. Returns the document as a byte array ready to write to disk
/// or upload to Blob Storage.
/// </summary>
public static class DocxReportWriter
{
    // Limes brand accent (a lime green) used for headings, as a hex RGB string.
    private const string Accent = "4F7A28";
    private const string HeaderFill = "EDF3E3";

    public static byte[] Write(AssessmentDeliverable deliverable)
    {
        ArgumentNullException.ThrowIfNull(deliverable);
        var c = CultureInfo.InvariantCulture;
        var assessment = deliverable.Assessment;

        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            var body = new Body();

            body.AppendChild(Title("Limes — AI CoE Readiness Assessment"));

            body.AppendChild(Meta("Partner", assessment.Partner.Name));
            if (!string.IsNullOrWhiteSpace(assessment.Partner.Region))
                body.AppendChild(Meta("Region", assessment.Partner.Region!));
            if (!string.IsNullOrWhiteSpace(assessment.Partner.Industry))
                body.AppendChild(Meta("Industry", assessment.Partner.Industry!));
            body.AppendChild(Meta("Generated", assessment.GeneratedAtUtc.ToString("u", c)));
            body.AppendChild(Meta("Mode", deliverable.Mode.ToString()));

            body.AppendChild(Heading(
                $"Overall CoE Readiness Index: {assessment.ReadinessIndex.ToString("0.00", c)} / 5.00 — {assessment.OverallLevel.DisplayName()}",
                level: 1));

            // --- Pillar scores ---
            body.AppendChild(Heading("Pillar scores", level: 2));
            var scoreRows = new List<string[]> { new[] { "Pillar", "Score", "Maturity" } };
            scoreRows.AddRange(assessment.PillarScores.Select(p => new[]
            {
                p.Pillar.DisplayName(),
                p.Score.ToString("0.00", c),
                p.Level.DisplayName(),
            }));
            body.AppendChild(Table(scoreRows));

            // --- Gaps ---
            body.AppendChild(Heading("Identified gaps", level: 2));
            var pillarsWithGaps = assessment.PillarScores.Where(p => p.Gaps.Count > 0).ToList();
            if (pillarsWithGaps.Count == 0)
            {
                body.AppendChild(Body_("No gaps flagged below the threshold.", italic: true));
            }
            else
            {
                foreach (var p in pillarsWithGaps)
                {
                    body.AppendChild(Heading(p.Pillar.DisplayName(), level: 3));
                    foreach (var gap in p.Gaps)
                        body.AppendChild(Bullet(gap));
                }
            }

            // --- Roadmap ---
            if (deliverable.Roadmap is { Actions.Count: > 0 } roadmap)
            {
                body.AppendChild(Heading("Remediation roadmap (Providentia)", level: 2));
                foreach (var wave in roadmap.Actions.GroupBy(a => a.Wave).OrderBy(g => g.Key))
                {
                    body.AppendChild(Heading($"Wave {wave.Key.ToString(c)}", level: 3));
                    foreach (var a in wave.OrderBy(a => a.Pillar))
                    {
                        body.AppendChild(Bullet($"{a.Title} ({a.Pillar.DisplayName()}, {a.Priority})", bold: true));
                        body.AppendChild(Body_(a.Description, indent: true));
                        if (a.DependsOn.Count > 0)
                            body.AppendChild(Body_($"Depends on: {string.Join(", ", a.DependsOn)}", indent: true, italic: true));
                        if (a.Citations.Count > 0)
                            body.AppendChild(Body_($"Grounding: {string.Join("; ", a.Citations)}", indent: true, italic: true));
                    }
                }
            }

            // --- Skilling ---
            if (deliverable.SkillingPlan is { Recommendations.Count: > 0 } skilling)
            {
                body.AppendChild(Heading("Skilling plan (Egeria)", level: 2));
                var rows = new List<string[]> { new[] { "Pillar", "Gap", "Microsoft Learn path", "Role" } };
                rows.AddRange(skilling.Recommendations.Select(r => new[]
                {
                    r.Pillar.DisplayName(),
                    r.Gap,
                    string.IsNullOrWhiteSpace(r.Url) ? r.LearnPath : $"{r.LearnPath} ({r.Url})",
                    r.Role ?? "—",
                }));
                body.AppendChild(Table(rows));
            }

            // --- Risks ---
            if (deliverable.RiskRegister is { Risks.Count: > 0 } risks)
            {
                body.AppendChild(Heading("Risk register (Terminus)", level: 2));
                var rows = new List<string[]> { new[] { "Severity", "Pillar", "Risk", "Mitigation" } };
                rows.AddRange(risks.Risks.OrderByDescending(r => r.Severity).Select(r => new[]
                {
                    r.Severity.ToString(),
                    r.Pillar.DisplayName(),
                    r.Title,
                    r.Mitigation,
                }));
                body.AppendChild(Table(rows));
            }

            // --- Footer ---
            var footer = $"Mode: {deliverable.Mode} · Generated: {assessment.GeneratedAtUtc.ToString("u", c)}";
            if (!string.IsNullOrWhiteSpace(deliverable.KnowledgeSource))
                footer += $" · Grounding corpus: {deliverable.KnowledgeSource}";
            body.AppendChild(Body_(footer, italic: true));

            main.Document = new Document(body);
            main.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph Title(string text) =>
        Para(text, sizeHalfPts: 40, bold: true, color: Accent, spaceAfter: 240);

    private static Paragraph Heading(string text, int level)
    {
        var size = level switch { 1 => 32, 2 => 28, _ => 24 };
        return Para(text, sizeHalfPts: size, bold: true, color: Accent, spaceBefore: 240, spaceAfter: 80);
    }

    private static Paragraph Meta(string label, string value)
    {
        var para = new Paragraph();
        para.AppendChild(Run_(label + ": ", bold: true));
        para.AppendChild(Run_(value));
        return para;
    }

    private static Paragraph Body_(string text, bool indent = false, bool italic = false)
    {
        var para = Para(text, sizeHalfPts: 22, italic: italic);
        if (indent)
            para.ParagraphProperties = new ParagraphProperties(new Indentation { Left = "480" });
        return para;
    }

    private static Paragraph Bullet(string text, bool bold = false)
    {
        var para = new Paragraph(
            new ParagraphProperties(new Indentation { Left = "360", Hanging = "180" }));
        para.AppendChild(Run_("• ", bold: bold));
        para.AppendChild(Run_(text, bold: bold));
        return para;
    }

    private static Paragraph Para(
        string text, int sizeHalfPts, bool bold = false, bool italic = false,
        string? color = null, int spaceBefore = 0, int spaceAfter = 60)
    {
        var props = new ParagraphProperties(
            new SpacingBetweenLines
            {
                Before = spaceBefore.ToString(CultureInfo.InvariantCulture),
                After = spaceAfter.ToString(CultureInfo.InvariantCulture),
            });
        var para = new Paragraph(props);
        para.AppendChild(Run_(text, bold, italic, sizeHalfPts, color));
        return para;
    }

    private static Run Run_(string text, bool bold = false, bool italic = false, int? sizeHalfPts = null, string? color = null)
    {
        var props = new RunProperties();
        if (bold) props.AppendChild(new Bold());
        if (italic) props.AppendChild(new Italic());
        // RunProperties children are an ordered sequence: color must precede sz (font size).
        if (color is not null) props.AppendChild(new Color { Val = color });
        if (sizeHalfPts is { } sz) props.AppendChild(new FontSize { Val = sz.ToString(CultureInfo.InvariantCulture) });

        var run = new Run();
        if (props.HasChildren) run.AppendChild(props);
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    /// <summary>Builds a bordered table; the first row is rendered as a shaded header.</summary>
    private static Table Table(IReadOnlyList<string[]> rows)
    {
        var table = new Table();
        const string borderColor = "C8D6B0";
        const uint borderSize = 4;
        table.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = borderSize, Color = borderColor },
                new LeftBorder { Val = BorderValues.Single, Size = borderSize, Color = borderColor },
                new BottomBorder { Val = BorderValues.Single, Size = borderSize, Color = borderColor },
                new RightBorder { Val = BorderValues.Single, Size = borderSize, Color = borderColor },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = borderSize, Color = borderColor },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = borderSize, Color = borderColor })));

        // A table must declare its column grid before any rows.
        var columnCount = rows.Count > 0 ? rows[0].Length : 0;
        var grid = new TableGrid();
        for (var col = 0; col < columnCount; col++)
            grid.AppendChild(new GridColumn());
        table.AppendChild(grid);

        for (var i = 0; i < rows.Count; i++)
        {
            var isHeader = i == 0;
            var row = new TableRow();
            foreach (var cellText in rows[i])
            {
                var cellProps = new TableCellProperties();
                // CT_TcPr is an ordered sequence: shading (shd) must precede cell margins (tcMar).
                if (isHeader)
                    cellProps.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = HeaderFill });
                cellProps.AppendChild(new TableCellMargin(
                    new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa }));

                var para = new Paragraph(Run_(cellText ?? "—", bold: isHeader, sizeHalfPts: 20));
                row.AppendChild(new TableCell(cellProps, para));
            }
            table.AppendChild(row);
        }

        return table;
    }
}
