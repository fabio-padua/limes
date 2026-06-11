using Limes.Core.Domain;
using Limes.Core.Reporting;

namespace Limes.Core.Tests;

public class MarkdownReportWriterTests
{
    private static AssessmentResult MinimalResult() => new()
    {
        Partner = new PartnerProfile { Name = "Contoso" },
        PillarScores = [],
        ReadinessIndex = 2.0,
        OverallLevel = MaturityLevel.Defined,
    };

    [Fact]
    public void Write_EscapesPipesAndNewlinesInTableCells()
    {
        var deliverable = new AssessmentDeliverable
        {
            Partner = new PartnerProfile { Name = "Contoso" },
            Assessment = MinimalResult(),
            Mode = AssessmentMode.Deterministic,
            RiskRegister = new RiskRegister
            {
                Risks =
                [
                    new Risk
                    {
                        Id = "R1",
                        Pillar = Pillar.DataFoundations,
                        Title = "Risk | with pipe",
                        Description = "d",
                        Severity = RiskSeverity.High,
                        Mitigation = "line1\nline2",
                    },
                ],
            },
        };

        var md = MarkdownReportWriter.Write(deliverable);

        // The risk row must remain a single, well-formed table row: pipe escaped, newline flattened.
        var row = md.Split('\n').Single(l => l.Contains("Risk \\| with pipe"));
        Assert.Contains("line1 line2", row);
        Assert.DoesNotContain("line1\nline2", md);
    }

    [Fact]
    public void Write_HtmlEncodesPipelineTrace()
    {
        var deliverable = new AssessmentDeliverable
        {
            Partner = new PartnerProfile { Name = "Contoso" },
            Assessment = MinimalResult(),
            Mode = AssessmentMode.Agents,
            PipelineTrace = ["Fama: </details><script>alert('x')</script>"],
        };

        var md = MarkdownReportWriter.Write(deliverable);

        Assert.Contains("&lt;/details&gt;&lt;script&gt;", md);
        Assert.DoesNotContain("</details><script>", md);
    }

    [Fact]
    public void Write_HtmlEncodesKnowledgeSourceFooter()
    {
        var deliverable = new AssessmentDeliverable
        {
            Partner = new PartnerProfile { Name = "Contoso" },
            Assessment = MinimalResult(),
            Mode = AssessmentMode.Agents,
            KnowledgeSource = "</details><script>alert(1)</script>@abc123",
        };

        var md = MarkdownReportWriter.Write(deliverable);

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", md);
        Assert.DoesNotContain("<script>alert(1)</script>", md);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("not-a-url")]
    public void Write_OmitsLinkForUnsafeOrInvalidUrl(string url)
    {
        var md = MarkdownReportWriter.Write(SkillingDeliverable(url));

        // The Learn-path cell is rendered as plain text (not a link) and the dangerous scheme
        // never reaches a link target.
        Assert.Contains("Some learn path", md);
        Assert.DoesNotContain("[Some learn path](", md);
        Assert.DoesNotContain("javascript:", md);
        Assert.DoesNotContain("data:text/html", md);
    }

    [Fact]
    public void Write_EmitsLinkForHttpsUrl()
    {
        var md = MarkdownReportWriter.Write(SkillingDeliverable("https://learn.microsoft.com/path/"));

        Assert.Contains("](https://learn.microsoft.com/path/)", md);
    }

    private static AssessmentDeliverable SkillingDeliverable(string url) => new()
    {
        Partner = new PartnerProfile { Name = "Contoso" },
        Assessment = MinimalResult(),
        Mode = AssessmentMode.Deterministic,
        SkillingPlan = new SkillingPlan
        {
            Recommendations =
            [
                new SkillingRecommendation
                {
                    Pillar = Pillar.DataFoundations,
                    Gap = "gap",
                    LearnPath = "Some learn path",
                    Url = url,
                    Role = "Data engineer",
                },
            ],
        },
    };
}
