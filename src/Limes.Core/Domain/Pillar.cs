namespace Limes.Core.Domain;

/// <summary>
/// The seven pillars of Microsoft's AI Readiness model. Limes scores a partner's
/// maturity against each pillar; the set is intentionally fixed so output maps 1:1
/// to official Microsoft AI CoE guidance.
/// </summary>
public enum Pillar
{
    BusinessStrategy = 1,
    AiStrategyAndExperience = 2,
    OrganizationAndCulture = 3,
    DataFoundations = 4,
    InfrastructureForAi = 5,
    ModelManagement = 6,
    GovernanceAndSecurity = 7,
}

public static class PillarInfo
{
    public static readonly IReadOnlyList<Pillar> All =
    [
        Pillar.BusinessStrategy,
        Pillar.AiStrategyAndExperience,
        Pillar.OrganizationAndCulture,
        Pillar.DataFoundations,
        Pillar.InfrastructureForAi,
        Pillar.ModelManagement,
        Pillar.GovernanceAndSecurity,
    ];

    public static string DisplayName(this Pillar pillar) => pillar switch
    {
        Pillar.BusinessStrategy => "Business Strategy",
        Pillar.AiStrategyAndExperience => "AI Strategy & Experience",
        Pillar.OrganizationAndCulture => "Organization & Culture",
        Pillar.DataFoundations => "Data Foundations",
        Pillar.InfrastructureForAi => "Infrastructure for AI",
        Pillar.ModelManagement => "Model Management",
        Pillar.GovernanceAndSecurity => "AI Governance & Security",
        _ => pillar.ToString(),
    };
}
