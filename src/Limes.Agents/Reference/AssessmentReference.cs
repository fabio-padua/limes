using Limes.Core.Domain;

namespace Limes.Agents.Reference;

/// <summary>
/// Static, deterministic reference data mapping each pillar to grounding citations,
/// Microsoft Learn skilling paths, and roadmap prerequisites. Keeping this in one place
/// makes the deterministic agents auditable and easy to align with the official corpus.
/// </summary>
public static class AssessmentReference
{
    private const string Caf = "Cloud Adoption Framework — Establish an AI Center of Excellence";
    private const string Waf = "Azure Well-Architected Framework — AI workload guidance";
    private const string Rai = "Microsoft Responsible AI Standard";

    /// <summary>Grounding citations to attach to a remediation action for the given pillar.</summary>
    public static IReadOnlyList<string> Citations(Pillar pillar) => pillar switch
    {
        Pillar.BusinessStrategy => [Caf],
        Pillar.AiStrategyAndExperience => [Caf, Waf],
        Pillar.OrganizationAndCulture => [Caf],
        Pillar.DataFoundations => [Waf],
        Pillar.InfrastructureForAi => [Waf],
        Pillar.ModelManagement => [Waf],
        Pillar.GovernanceAndSecurity => [Rai, Waf],
        _ => [Caf],
    };

    /// <summary>
    /// Pillars that should be addressed before the given pillar. Used by Providentia to wire
    /// dependency edges (foundations and exec sponsorship come first).
    /// </summary>
    public static IReadOnlyList<Pillar> Prerequisites(Pillar pillar) => pillar switch
    {
        Pillar.BusinessStrategy => [],
        Pillar.OrganizationAndCulture => [Pillar.BusinessStrategy],
        Pillar.AiStrategyAndExperience => [Pillar.BusinessStrategy],
        Pillar.DataFoundations => [],
        Pillar.InfrastructureForAi => [Pillar.DataFoundations],
        Pillar.ModelManagement => [Pillar.DataFoundations, Pillar.InfrastructureForAi],
        Pillar.GovernanceAndSecurity => [Pillar.DataFoundations],
        _ => [],
    };

    /// <summary>The recommended Microsoft Learn path for skilling on the given pillar.</summary>
    public static (string Path, string Url, string Role) LearnPath(Pillar pillar) => pillar switch
    {
        Pillar.BusinessStrategy => (
            "AI business school: strategy for business leaders",
            "https://learn.microsoft.com/training/paths/ai-business-school/",
            "Executive sponsor"),
        Pillar.AiStrategyAndExperience => (
            "Plan and prepare to develop AI solutions on Azure",
            "https://learn.microsoft.com/training/paths/plan-prepare-develop-ai-solutions-azure/",
            "AI product owner"),
        Pillar.OrganizationAndCulture => (
            "Get started with AI: build AI literacy across the organization",
            "https://learn.microsoft.com/training/paths/get-started-with-artificial-intelligence-on-azure/",
            "Change manager / L&D"),
        Pillar.DataFoundations => (
            "Implement a data governance foundation with Microsoft Purview",
            "https://learn.microsoft.com/training/paths/govern-data-microsoft-purview/",
            "Data engineer"),
        Pillar.InfrastructureForAi => (
            "Build AI apps on a scalable Azure platform (AI Foundry)",
            "https://learn.microsoft.com/training/paths/create-machine-learning-models-azure/",
            "Platform engineer"),
        Pillar.ModelManagement => (
            "Operationalize machine learning with MLOps",
            "https://learn.microsoft.com/training/paths/introduction-machine-learn-operations/",
            "ML engineer"),
        Pillar.GovernanceAndSecurity => (
            "Embrace responsible AI principles and practices",
            "https://learn.microsoft.com/training/modules/embrace-responsible-ai-principles-practices/",
            "Responsible AI lead"),
        _ => ("Microsoft Learn — Azure AI fundamentals",
              "https://learn.microsoft.com/training/paths/get-started-with-artificial-intelligence-on-azure/",
              "All roles"),
    };
}
