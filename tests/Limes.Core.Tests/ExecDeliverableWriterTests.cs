using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Limes.Core.Domain;
using Limes.Core.Reporting;

namespace Limes.Core.Tests;

/// <summary>
/// Validates the Open XML executive deliverables (Fama .docx / .pptx). Each test asserts the
/// generated package is schema-valid (via <see cref="OpenXmlValidator"/>) and carries the
/// expected content, so a regression in the verbose Open XML scaffolding fails fast in CI.
/// </summary>
public class ExecDeliverableWriterTests
{
    private static AssessmentDeliverable RichDeliverable() => new()
    {
        Partner = new PartnerProfile { Name = "Contoso Ltd", Region = "EMEA", Industry = "Retail" },
        Mode = AssessmentMode.Agents,
        KnowledgeSource = "ai-coe-knowledge.md@abc123",
        PipelineTrace = ["Janus: normalized", "Fama: assembled"],
        Assessment = new AssessmentResult
        {
            Partner = new PartnerProfile { Name = "Contoso Ltd", Region = "EMEA", Industry = "Retail" },
            ReadinessIndex = 2.64,
            OverallLevel = MaturityLevel.Defined,
            PillarScores =
            [
                new PillarScore
                {
                    Pillar = Pillar.DataFoundations,
                    Score = 1.8,
                    Level = MaturityLevel.Developing,
                    Gaps = ["No unified data catalog", "Limited lineage tracking"],
                },
                new PillarScore
                {
                    Pillar = Pillar.GovernanceAndSecurity,
                    Score = 3.4,
                    Level = MaturityLevel.Defined,
                    Gaps = [],
                },
            ],
        },
        Roadmap = new Roadmap
        {
            Actions =
            [
                new RemediationAction
                {
                    Id = "A1", Pillar = Pillar.DataFoundations, Title = "Stand up a data catalog",
                    Description = "Deploy Microsoft Purview.", Wave = 1, Priority = ActionPriority.QuickWin,
                    Citations = ["CAF: Govern"],
                },
                new RemediationAction
                {
                    Id = "A2", Pillar = Pillar.GovernanceAndSecurity, Title = "Adopt RAI standard",
                    Description = "Operationalize the Responsible AI Standard.", Wave = 2,
                    Priority = ActionPriority.Strategic, DependsOn = ["A1"],
                },
            ],
        },
        SkillingPlan = new SkillingPlan
        {
            Recommendations =
            [
                new SkillingRecommendation
                {
                    Pillar = Pillar.DataFoundations, Gap = "No catalog",
                    LearnPath = "Govern data with Purview", Url = "https://learn.microsoft.com/purview", Role = "Data Engineer",
                },
            ],
        },
        RiskRegister = new RiskRegister
        {
            Risks =
            [
                new Risk
                {
                    Id = "R1", Pillar = Pillar.GovernanceAndSecurity, Title = "Ungoverned model use",
                    Description = "Shadow AI.", Severity = RiskSeverity.High, Mitigation = "Establish an AI governance board.",
                },
            ],
        },
    };

    private static AssessmentDeliverable MinimalDeliverable() => new()
    {
        Partner = new PartnerProfile { Name = "Fabrikam" },
        Mode = AssessmentMode.Deterministic,
        Assessment = new AssessmentResult
        {
            Partner = new PartnerProfile { Name = "Fabrikam" },
            ReadinessIndex = 1.0,
            OverallLevel = MaturityLevel.Initial,
            PillarScores = [],
        },
    };

    [Fact]
    public void Docx_IsSchemaValidAndContainsPartner()
    {
        var bytes = DocxReportWriter.Write(RichDeliverable());

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        AssertValid(doc);

        var text = doc.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains("Contoso Ltd", text);
        Assert.Contains("Stand up a data catalog", text);
        Assert.Contains("Ungoverned model use", text);
    }

    [Fact]
    public void Pptx_IsSchemaValidAndContainsPartner()
    {
        var bytes = PptxReportWriter.Write(RichDeliverable());

        using var ms = new MemoryStream(bytes);
        using var doc = PresentationDocument.Open(ms, false);
        AssertValid(doc);

        var presentationPart = doc.PresentationPart!;
        Assert.Equal(4, presentationPart.SlideParts.Count());
        var allText = string.Concat(presentationPart.SlideParts.Select(s => s.Slide.InnerText));
        Assert.Contains("Contoso Ltd", allText);
        Assert.Contains("Pillar scores", allText);
    }

    [Fact]
    public void Writers_HandleMinimalDeliverableWithoutThrowing()
    {
        var docx = DocxReportWriter.Write(MinimalDeliverable());
        var pptx = PptxReportWriter.Write(MinimalDeliverable());

        using var docxMs = new MemoryStream(docx);
        using var word = WordprocessingDocument.Open(docxMs, false);
        AssertValid(word);
        // Even with no pillar scores, the Word report must still carry real content.
        var docxText = word.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains("Fabrikam", docxText);
        Assert.Contains("No pillar scores were available", docxText);

        using var pptxMs = new MemoryStream(pptx);
        using var pres = PresentationDocument.Open(pptxMs, false);
        AssertValid(pres);
        // Empty roadmap/risk register still produce their placeholder slides.
        var allText = string.Concat(pres.PresentationPart!.SlideParts.Select(s => s.Slide.InnerText));
        Assert.Contains("No roadmap actions", allText);
        Assert.Contains("No risks were identified", allText);
        Assert.Contains("No pillar scores were generated", allText);
    }

    private static void AssertValid(OpenXmlPackage package)
    {
        var errors = new OpenXmlValidator(FileFormatVersions.Office2019).Validate(package).ToList();
        Assert.True(errors.Count == 0, string.Join("\n",
            errors.Select(e => $"{e.Id}: {e.Description} @ {e.Path?.XPath}")));
    }
}
