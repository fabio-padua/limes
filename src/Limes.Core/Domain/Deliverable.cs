namespace Limes.Core.Domain;

/// <summary>Which engine produced an assessment deliverable.</summary>
public enum AssessmentMode
{
    /// <summary>Pure rules engine, $0 model cost (Phase 1 baseline; Iustitia's fallback).</summary>
    Deterministic = 0,

    /// <summary>MAF + Foundry multi-agent pipeline layered on the deterministic core.</summary>
    Agents = 1,
}

/// <summary>Relative effort/impact classification for a remediation action.</summary>
public enum ActionPriority
{
    /// <summary>Foundational, high-leverage fix to sequence first.</summary>
    QuickWin = 1,

    /// <summary>Larger, longer-horizon investment.</summary>
    Strategic = 2,
}

/// <summary>Severity of an identified risk (Terminus).</summary>
public enum RiskSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

/// <summary>
/// A single, dependency-aware remediation action produced by Providentia. Actions are
/// grouped into waves so the partner sequences quick wins before strategic bets.
/// </summary>
public sealed record RemediationAction
{
    public required string Id { get; init; }
    public required Pillar Pillar { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }

    /// <summary>1-based wave; lower waves are sequenced first.</summary>
    public required int Wave { get; init; }
    public required ActionPriority Priority { get; init; }

    /// <summary>Ids of actions that must complete before this one starts.</summary>
    public IReadOnlyList<string> DependsOn { get; init; } = [];

    /// <summary>Grounding citations (CAF / WAF / Microsoft Learn).</summary>
    public IReadOnlyList<string> Citations { get; init; } = [];
}

/// <summary>The prioritized, wave-based remediation plan (Providentia).</summary>
public sealed record Roadmap
{
    public required IReadOnlyList<RemediationAction> Actions { get; init; }
}

/// <summary>A Microsoft Learn skilling recommendation mapped to a specific gap (Egeria).</summary>
public sealed record SkillingRecommendation
{
    public required Pillar Pillar { get; init; }
    public required string Gap { get; init; }
    public required string LearnPath { get; init; }
    public string? Url { get; init; }
    public string? Role { get; init; }
}

/// <summary>The role-based skilling plan (Egeria).</summary>
public sealed record SkillingPlan
{
    public required IReadOnlyList<SkillingRecommendation> Recommendations { get; init; }
}

/// <summary>A risk anchored to Responsible AI + WAF security/governance (Terminus).</summary>
public sealed record Risk
{
    public required string Id { get; init; }
    public required Pillar Pillar { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required RiskSeverity Severity { get; init; }
    public required string Mitigation { get; init; }
    public IReadOnlyList<string> Citations { get; init; } = [];
}

/// <summary>The full risk register (Terminus).</summary>
public sealed record RiskRegister
{
    public required IReadOnlyList<Risk> Risks { get; init; }
}

/// <summary>
/// The complete assessment deliverable assembled by Fama: the scored result plus the
/// roadmap, skilling plan, and risk register. This is the canonical Phase 2 output that
/// both the deterministic and agents-mode pipelines produce.
/// </summary>
public sealed record AssessmentDeliverable
{
    public required PartnerProfile Partner { get; init; }
    public required AssessmentResult Assessment { get; init; }
    public Roadmap? Roadmap { get; init; }
    public SkillingPlan? SkillingPlan { get; init; }
    public RiskRegister? RiskRegister { get; init; }
    public required AssessmentMode Mode { get; init; }

    /// <summary>Ordered, human-readable trace of each agent stage (for transparency / review).</summary>
    public IReadOnlyList<string> PipelineTrace { get; init; } = [];

    /// <summary>Name + content hash of the knowledge corpus used to ground the agents, if any.</summary>
    public string? KnowledgeSource { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
