using Limes.Agents.Knowledge;
using Limes.Agents.Pipeline;
using Microsoft.Agents.AI;

namespace Limes.Agents.Maf;

/// <summary>
/// A MAF-backed pipeline stage that wraps a deterministic fallback agent. It first runs the
/// deterministic agent (which computes the authoritative structured result — scores, roadmap,
/// etc.), then asks its Foundry <see cref="AIAgent"/> for a grounded narrative that is appended
/// to the pipeline trace. If the LLM call fails, the deterministic result still stands, so the
/// pipeline degrades gracefully and never hallucinates structured output.
/// </summary>
public sealed class NarrativeChatAgent : ChatAgentBase
{
    private readonly ILimesAgent _fallback;
    private readonly Func<AssessmentContext, string> _promptBuilder;

    public NarrativeChatAgent(
        ILimesAgent fallback,
        AIAgent agent,
        Func<AssessmentContext, string> promptBuilder,
        IKnowledgeSource? knowledge = null)
        : base(agent, knowledge)
    {
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
    }

    public override string Codename => _fallback.Codename;
    public override string Role => _fallback.Role;

    public override async Task RunAsync(AssessmentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Record the grounding descriptor before the fallback runs so that when Fama assembles
        // the deliverable it already carries the knowledge source.
        if (KnowledgeDescriptor is not null && string.IsNullOrEmpty(context.KnowledgeSource))
            context.KnowledgeSource = KnowledgeDescriptor;

        // Deterministic structured computation is always authoritative.
        await _fallback.RunAsync(context, cancellationToken).ConfigureAwait(false);

        try
        {
            var narrative = await AskAsync(_promptBuilder(context), cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(narrative))
                context.Note(Codename, $"narrative — {Condense(narrative)}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Note(Codename, $"LLM narrative unavailable, using deterministic result ({ex.GetType().Name}).");
        }

        // If a deliverable was already assembled (e.g. this stage wraps Fama), refresh it so the
        // returned deliverable reflects the final trace — including the narrative note appended
        // above — and the grounding descriptor.
        if (context.Deliverable is not null)
        {
            context.Deliverable = context.Deliverable with
            {
                PipelineTrace = context.Trace.ToList(),
                KnowledgeSource = context.KnowledgeSource,
            };
        }
    }

    private static string Condense(string text)
    {
        var oneLine = string.Join(' ', text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return oneLine.Length <= 280 ? oneLine : oneLine[..280] + "…";
    }
}
