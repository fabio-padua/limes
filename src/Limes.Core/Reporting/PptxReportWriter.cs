using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Limes.Core.Domain;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace Limes.Core.Reporting;

/// <summary>
/// Renders a short executive <see cref="AssessmentDeliverable"/> summary as a Microsoft
/// PowerPoint (.pptx) deck using the Open XML SDK — no Office install required. The deck is
/// intentionally concise (title, pillar scores, top priorities, top risks) so it works as a
/// leave-behind. Returns the presentation as a byte array.
/// </summary>
public static class PptxReportWriter
{
    // 16:9 slide in EMUs (1 inch = 914400 EMU): 13.333in x 7.5in.
    private const long SlideCx = 12192000;
    private const long SlideCy = 6858000;
    private const long Margin = 685800; // 0.75in
    private const string Accent = "4F7A28";
    private const string Ink = "323232";

    /// <summary>How many roadmap actions / risks to surface on the summary slides.</summary>
    private const int TopN = 6;

    public static byte[] Write(AssessmentDeliverable deliverable)
    {
        ArgumentNullException.ThrowIfNull(deliverable);
        var c = CultureInfo.InvariantCulture;
        var assessment = deliverable.Assessment;

        using var stream = new MemoryStream();
        using (var doc = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            var presentationPart = doc.AddPresentationPart();

            // --- Master + layout + theme (minimal but valid) ---
            var masterPart = presentationPart.AddNewPart<SlideMasterPart>();
            var layoutPart = masterPart.AddNewPart<SlideLayoutPart>();
            var themePart = masterPart.AddNewPart<ThemePart>();
            WriteTheme(themePart);

            layoutPart.SlideLayout = new P.SlideLayout(
                new P.CommonSlideData(EmptyShapeTree()),
                new P.ColorMapOverride(new D.MasterColorMapping()))
            {
                Type = P.SlideLayoutValues.Blank,
                Preserve = true,
            };
            layoutPart.AddPart(masterPart); // layout -> master back-reference

            masterPart.SlideMaster = new P.SlideMaster(
                new P.CommonSlideData(EmptyShapeTree()),
                DefaultColorMap(),
                new P.SlideLayoutIdList(new P.SlideLayoutId
                {
                    Id = 2147483649U,
                    RelationshipId = masterPart.GetIdOfPart(layoutPart),
                }));

            // --- Slides ---
            var slideIds = new P.SlideIdList();
            uint slideId = 256;

            void AddSlide(P.Slide slide)
            {
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.Slide = slide;
                slidePart.AddPart(layoutPart);
                slideIds.AppendChild(new P.SlideId
                {
                    Id = slideId++,
                    RelationshipId = presentationPart.GetIdOfPart(slidePart),
                });
            }

            AddSlide(TitleSlide(deliverable, c));
            AddSlide(PillarSlide(assessment, c));
            AddSlide(PrioritiesSlide(deliverable));
            AddSlide(RisksSlide(deliverable));

            presentationPart.Presentation = new P.Presentation(
                new P.SlideMasterIdList(new P.SlideMasterId
                {
                    Id = 2147483648U,
                    RelationshipId = presentationPart.GetIdOfPart(masterPart),
                }),
                slideIds,
                new P.SlideSize { Cx = (Int32Value)SlideCx, Cy = (Int32Value)SlideCy },
                new P.NotesSize { Cx = 6858000, Cy = 9144000 });

            presentationPart.Presentation.Save();
        }

        return stream.ToArray();
    }

    // ---- Slide content ----

    private static P.Slide TitleSlide(AssessmentDeliverable d, CultureInfo c)
    {
        var a = d.Assessment;
        var subtitle = new List<Line>
        {
            new("AI CoE Readiness Assessment", 0, false, 24, Ink, true),
            new($"Readiness Index: {a.ReadinessIndex.ToString("0.00", c)} / 5.00 — {a.OverallLevel.DisplayName()}", 0, true, 28, Accent, true),
        };
        var meta = new StringBuilder(a.Partner.Name);
        if (!string.IsNullOrWhiteSpace(a.Partner.Industry)) meta.Append(" · ").Append(a.Partner.Industry);
        if (!string.IsNullOrWhiteSpace(a.Partner.Region)) meta.Append(" · ").Append(a.Partner.Region);
        subtitle.Add(new(meta.ToString(), 0, false, 16, Ink, true));
        subtitle.Add(new($"Generated {a.GeneratedAtUtc.ToString("u", c)} · {d.Mode} mode", 0, false, 14, "808080", true));

        return Slide(
            TextShape(2, "Title", Margin, 2057400, SlideCx - 2 * Margin, 1371600,
                [new(a.Partner.Name, 0, true, 44, Accent, true)]),
            TextShape(3, "Subtitle", Margin, 3520440, SlideCx - 2 * Margin, 2400300, subtitle));
    }

    private static P.Slide PillarSlide(AssessmentResult a, CultureInfo c)
    {
        var lines = a.PillarScores
            .Select(p => new Line(
                $"{p.Pillar.DisplayName()} — {p.Score.ToString("0.00", c)} ({p.Level.DisplayName()})",
                0, false, 18, Ink, false))
            .ToList();
        if (lines.Count == 0)
            lines.Add(new("No pillar scores were generated.", 0, false, 18, Ink, false));
        return ContentSlide("Pillar scores", lines);
    }

    private static P.Slide PrioritiesSlide(AssessmentDeliverable d)
    {
        var actions = d.Roadmap?.Actions ?? [];
        List<Line> lines;
        if (actions.Count == 0)
        {
            lines = [new("No roadmap actions were generated.", 0, false, 18, Ink, false)];
        }
        else
        {
            lines = actions
                .OrderBy(x => x.Wave).ThenBy(x => x.Pillar)
                .Take(TopN)
                .Select(x => new Line(
                    $"Wave {x.Wave.ToString(CultureInfo.InvariantCulture)} · {x.Title} ({x.Pillar.DisplayName()})",
                    0, false, 18, Ink, false))
                .ToList();
        }
        return ContentSlide("Top priorities", lines);
    }

    private static P.Slide RisksSlide(AssessmentDeliverable d)
    {
        var risks = d.RiskRegister?.Risks ?? [];
        List<Line> lines;
        if (risks.Count == 0)
        {
            lines = [new("No risks were identified.", 0, false, 18, Ink, false)];
        }
        else
        {
            lines = risks
                .OrderByDescending(r => r.Severity)
                .Take(TopN)
                .Select(r => new Line(
                    $"[{r.Severity}] {r.Title} ({r.Pillar.DisplayName()})",
                    0, false, 18, Ink, false))
                .ToList();
        }
        return ContentSlide("Top risks", lines);
    }

    private static P.Slide ContentSlide(string title, IReadOnlyList<Line> bodyLines) =>
        Slide(
            TextShape(2, "Title", Margin, 381000, SlideCx - 2 * Margin, 1143000,
                [new(title, 0, true, 32, Accent, false)]),
            TextShape(3, "Body", Margin, 1600200, SlideCx - 2 * Margin, 4800600,
                bodyLines.Select(l => l with { Text = "•  " + l.Text }).ToList()));

    // ---- OpenXML scaffolding ----

    private static P.Slide Slide(params P.Shape[] shapes)
    {
        var tree = EmptyShapeTree();
        foreach (var shape in shapes)
            tree.AppendChild(shape);
        return new P.Slide(
            new P.CommonSlideData(tree),
            new P.ColorMapOverride(new D.MasterColorMapping()));
    }

    private static P.ShapeTree EmptyShapeTree() =>
        new(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new D.TransformGroup()));

    private static P.Shape TextShape(uint id, string name, long x, long y, long cx, long cy, IReadOnlyList<Line> lines)
    {
        var textBody = new P.TextBody(new D.BodyProperties(), new D.ListStyle());
        foreach (var line in lines)
        {
            var runProps = new D.RunProperties
            {
                Language = "en-US",
                FontSize = line.SizePt * 100,
                Bold = line.Bold,
                Dirty = false,
            };
            if (line.ColorHex is not null)
                runProps.AppendChild(new D.SolidFill(new D.RgbColorModelHex { Val = line.ColorHex }));

            var paraProps = new D.ParagraphProperties(new D.NoBullet()) { Level = line.Level };
            if (line.Center)
                paraProps.Alignment = D.TextAlignmentTypeValues.Center;

            textBody.AppendChild(new D.Paragraph(
                paraProps,
                new D.Run(runProps, new D.Text(line.Text))));
        }

        // A txBody must contain at least one paragraph even when there are no lines.
        if (lines.Count == 0)
            textBody.AppendChild(new D.Paragraph(new D.ParagraphProperties(new D.NoBullet())));

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(new D.ShapeLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new D.Transform2D(
                    new D.Offset { X = x, Y = y },
                    new D.Extents { Cx = cx, Cy = cy }),
                new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle }),
            textBody);
    }

    private static P.ColorMap DefaultColorMap() => new()
    {
        Background1 = D.ColorSchemeIndexValues.Light1,
        Text1 = D.ColorSchemeIndexValues.Dark1,
        Background2 = D.ColorSchemeIndexValues.Light2,
        Text2 = D.ColorSchemeIndexValues.Dark2,
        Accent1 = D.ColorSchemeIndexValues.Accent1,
        Accent2 = D.ColorSchemeIndexValues.Accent2,
        Accent3 = D.ColorSchemeIndexValues.Accent3,
        Accent4 = D.ColorSchemeIndexValues.Accent4,
        Accent5 = D.ColorSchemeIndexValues.Accent5,
        Accent6 = D.ColorSchemeIndexValues.Accent6,
        Hyperlink = D.ColorSchemeIndexValues.Hyperlink,
        FollowedHyperlink = D.ColorSchemeIndexValues.FollowedHyperlink,
    };

    private static void WriteTheme(ThemePart themePart)
    {
        const string themeXml =
            """
            <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Limes">
              <a:themeElements>
                <a:clrScheme name="Limes">
                  <a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1>
                  <a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1>
                  <a:dk2><a:srgbClr val="44546A"/></a:dk2>
                  <a:lt2><a:srgbClr val="E7E6E6"/></a:lt2>
                  <a:accent1><a:srgbClr val="4F7A28"/></a:accent1>
                  <a:accent2><a:srgbClr val="7AB317"/></a:accent2>
                  <a:accent3><a:srgbClr val="A5A5A5"/></a:accent3>
                  <a:accent4><a:srgbClr val="FFC000"/></a:accent4>
                  <a:accent5><a:srgbClr val="5B9BD5"/></a:accent5>
                  <a:accent6><a:srgbClr val="70AD47"/></a:accent6>
                  <a:hlink><a:srgbClr val="0563C1"/></a:hlink>
                  <a:folHlink><a:srgbClr val="954F72"/></a:folHlink>
                </a:clrScheme>
                <a:fontScheme name="Office">
                  <a:majorFont><a:latin typeface="Calibri Light"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont>
                  <a:minorFont><a:latin typeface="Calibri"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont>
                </a:fontScheme>
                <a:fmtScheme name="Office">
                  <a:fillStyleLst>
                    <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                    <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                    <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                  </a:fillStyleLst>
                  <a:lnStyleLst>
                    <a:ln w="6350" cap="flat" cmpd="sng" algn="ctr"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:prstDash val="solid"/></a:ln>
                    <a:ln w="12700" cap="flat" cmpd="sng" algn="ctr"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:prstDash val="solid"/></a:ln>
                    <a:ln w="19050" cap="flat" cmpd="sng" algn="ctr"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:prstDash val="solid"/></a:ln>
                  </a:lnStyleLst>
                  <a:effectStyleLst>
                    <a:effectStyle><a:effectLst/></a:effectStyle>
                    <a:effectStyle><a:effectLst/></a:effectStyle>
                    <a:effectStyle><a:effectLst/></a:effectStyle>
                  </a:effectStyleLst>
                  <a:bgFillStyleLst>
                    <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                    <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                    <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                  </a:bgFillStyleLst>
                </a:fmtScheme>
              </a:themeElements>
            </a:theme>
            """;
        using var s = themePart.GetStream(FileMode.Create, FileAccess.Write);
        var bytes = Encoding.UTF8.GetBytes(themeXml);
        s.Write(bytes, 0, bytes.Length);
    }

    /// <summary>A single line of slide text: content, indent level, weight, point size, colour, alignment.</summary>
    private readonly record struct Line(string Text, int Level, bool Bold, int SizePt, string? ColorHex, bool Center);
}
