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
}
