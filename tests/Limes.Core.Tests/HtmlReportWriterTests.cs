using Limes.Core.Domain;
using Limes.Core.Reporting;

namespace Limes.Core.Tests;

/// <summary>
/// Covers the self-contained HTML report (Fama .html): that it carries the expected content and,
/// critically, that untrusted text is HTML-encoded and skilling links are restricted to safe
/// http/https URLs so the artifact can't carry script or dangerous URI schemes.
/// </summary>
public class HtmlReportWriterTests
{
    private static AssessmentDeliverable Deliverable(
        string partnerName = "Contoso Ltd",
        string? skillingUrl = "https://learn.microsoft.com/purview",
        string gap = "No unified data catalog") => new()
    {
        Partner = new PartnerProfile { Name = partnerName, Region = "EMEA", Industry = "Retail" },
        Mode = AssessmentMode.Agents,
        KnowledgeSource = "ai-coe-knowledge.md@abc123",
        PipelineTrace = ["Janus: normalized", "Fama: assembled"],
        Assessment = new AssessmentResult
        {
            Partner = new PartnerProfile { Name = partnerName, Region = "EMEA", Industry = "Retail" },
            ReadinessIndex = 2.64,
            OverallLevel = MaturityLevel.Defined,
            PillarScores =
            [
                new PillarScore
                {
                    Pillar = Pillar.DataFoundations, Score = 1.8, Level = MaturityLevel.Developing,
                    Gaps = [gap],
                },
                new PillarScore
                {
                    Pillar = Pillar.GovernanceAndSecurity, Score = 3.4, Level = MaturityLevel.Defined, Gaps = [],
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
                    LearnPath = "Govern data with Purview", Url = skillingUrl, Role = "Data Engineer",
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

    [Fact]
    public void Html_IsSelfContainedAndCarriesContent()
    {
        var html = HtmlReportWriter.Write(Deliverable());

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("Contoso Ltd", html);
        Assert.Contains("2.64", html);
        Assert.Contains("Defined", html);
        Assert.Contains("Stand up a data catalog", html);
        Assert.Contains("Ungoverned model use", html);
        Assert.Contains("Govern data with Purview", html);
        // Self-contained: no external stylesheets/scripts.
        Assert.DoesNotContain("<link", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public void Html_EscapesUntrustedText()
    {
        var html = HtmlReportWriter.Write(Deliverable(
            partnerName: "<script>alert(1)</script>",
            gap: "<img src=x onerror=alert(1)>"));

        // The raw markup must never reach the document verbatim.
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.DoesNotContain("<img src=x onerror=alert(1)>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
    }

    [Fact]
    public void Html_RendersSafeSkillingLinkButDropsDangerousScheme()
    {
        var safe = HtmlReportWriter.Write(Deliverable(skillingUrl: "https://learn.microsoft.com/purview"));
        Assert.Contains("<a href=\"https://learn.microsoft.com/purview\"", safe);
        Assert.Contains("rel=\"noopener noreferrer\"", safe);

        var dangerous = HtmlReportWriter.Write(Deliverable(skillingUrl: "javascript:alert(1)"));
        Assert.DoesNotContain("javascript:alert(1)", dangerous);
        // The path text is still present, just not as a link.
        Assert.Contains("Govern data with Purview", dangerous);
    }

    [Fact]
    public void Html_HandlesMinimalDeliverableWithoutThrowing()
    {
        var minimal = new AssessmentDeliverable
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

        var html = HtmlReportWriter.Write(minimal);
        Assert.Contains("Fabrikam", html);
        Assert.Contains("No gaps flagged", html);
        Assert.EndsWith("</html>\n", html);
    }
}
