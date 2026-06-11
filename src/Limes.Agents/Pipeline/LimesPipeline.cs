using Limes.Core.Domain;

namespace Limes.Agents.Pipeline;

/// <summary>
/// Runs an ordered set of <see cref="ILimesAgent"/> stages over one intake and returns the
/// assembled <see cref="AssessmentDeliverable"/>. The canonical order is
/// Janus → Iustitia → Providentia → Egeria → Terminus → Fama.
/// </summary>
public sealed class LimesPipeline
{
    private readonly IReadOnlyList<ILimesAgent> _agents;

    public LimesPipeline(IEnumerable<ILimesAgent> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);
        _agents = agents.ToList();
        if (_agents.Count == 0)
            throw new ArgumentException("A pipeline requires at least one agent.", nameof(agents));
    }

    public IReadOnlyList<ILimesAgent> Agents => _agents;

    public async Task<AssessmentDeliverable> RunAsync(
        AssessmentIntake intake,
        AssessmentMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intake);

        var context = new AssessmentContext { Intake = intake, Mode = mode };

        foreach (var agent in _agents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await agent.RunAsync(context, cancellationToken).ConfigureAwait(false);
        }

        return context.Deliverable
            ?? throw new InvalidOperationException(
                "Pipeline completed without producing a deliverable — Fama (the report agent) must run last.");
    }
}
