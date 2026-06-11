using Limes.Agents.Pipeline;
using Limes.Core.Domain;

namespace Limes.Agents.Deterministic;

/// <summary>
/// Fama (renown) — Report. Assembles the final <see cref="AssessmentDeliverable"/> from
/// everything the upstream agents produced: scores, roadmap, skilling plan, and risk register,
/// plus the pipeline trace and grounding-corpus descriptor. Serialization to JSON/Markdown
/// (and later .docx/.pptx) is handled by the reporting writers.
/// </summary>
public sealed class FamaReportAgent : ILimesAgent
{
    public string Codename => "Fama";
    public string Role => "Report";

    public Task RunAsync(AssessmentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var scoring = context.RequireScoring();

        context.Note(Codename, "assembled assessment deliverable.");

        context.Deliverable = new AssessmentDeliverable
        {
            Partner = scoring.Partner,
            Assessment = scoring,
            Roadmap = context.Roadmap,
            SkillingPlan = context.SkillingPlan,
            RiskRegister = context.RiskRegister,
            Mode = context.Mode,
            PipelineTrace = context.Trace.ToList(),
            KnowledgeSource = context.KnowledgeSource,
        };

        return Task.CompletedTask;
    }
}
