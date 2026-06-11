using Limes.Agents.Deterministic;
using Limes.Agents.Knowledge;
using Limes.Agents.Maf;
using Limes.Agents.Pipeline;
using Limes.Core.Domain;

namespace Limes.Agents;

/// <summary>
/// Assembles a <see cref="LimesPipeline"/> for a given run mode. Deterministic mode wires the
/// six pure-rules agents ($0 model cost). Agents mode wraps each deterministic agent in a
/// MAF-backed <see cref="NarrativeChatAgent"/> that adds grounded narrative while the
/// deterministic result remains authoritative.
/// </summary>
public static class LimesPipelineFactory
{
    /// <summary>The canonical deterministic pipeline: Janus → Iustitia → Providentia → Egeria → Terminus → Fama.</summary>
    public static LimesPipeline CreateDeterministic(IReadOnlyDictionary<Pillar, double>? pillarWeights = null)
        => new(
        [
            new JanusIntakeAgent(),
            new IustitiaScoringAgent(pillarWeights),
            new ProvidentiaRoadmapAgent(),
            new EgeriaSkillingAgent(),
            new TerminusRiskAgent(),
            new FamaReportAgent(),
        ]);

    /// <summary>
    /// The agents-mode pipeline. Each stage runs its deterministic counterpart for authoritative
    /// structured output, then a Foundry agent (grounded by Minerva, if supplied) for narrative.
    /// </summary>
    public static LimesPipeline CreateAgents(
        FoundryAgentFactory factory,
        IKnowledgeSource? knowledge = null,
        IReadOnlyDictionary<Pillar, double>? pillarWeights = null)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return new LimesPipeline(
        [
            // Janus normalizes structurally only — no narrative needed.
            new JanusIntakeAgent(),
            new NarrativeChatAgent(
                new IustitiaScoringAgent(pillarWeights),
                factory.CreateAgent("Iustitia", AgentPrompts.Iustitia),
                AgentPrompts.ScoringPrompt, knowledge),
            new NarrativeChatAgent(
                new ProvidentiaRoadmapAgent(),
                factory.CreateAgent("Providentia", AgentPrompts.Providentia),
                AgentPrompts.RoadmapPrompt, knowledge),
            new NarrativeChatAgent(
                new EgeriaSkillingAgent(),
                factory.CreateAgent("Egeria", AgentPrompts.Egeria),
                AgentPrompts.SkillingPrompt, knowledge),
            new NarrativeChatAgent(
                new TerminusRiskAgent(),
                factory.CreateAgent("Terminus", AgentPrompts.Terminus),
                AgentPrompts.RiskPrompt, knowledge),
            new NarrativeChatAgent(
                new FamaReportAgent(),
                factory.CreateAgent("Fama", AgentPrompts.Fama),
                AgentPrompts.ReportPrompt, knowledge),
        ]);
    }
}
