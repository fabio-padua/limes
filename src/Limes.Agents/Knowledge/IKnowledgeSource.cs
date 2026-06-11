namespace Limes.Agents.Knowledge;

/// <summary>
/// Minerva's grounding contract. Provides a citable <c>REFERENCE KNOWLEDGE</c> block that is
/// prompt-injected into the MAF agents, plus a content hash for drift detection. The lean v1
/// is file-backed; the upgrade path is a Microsoft Learn MCP / Foundry knowledge source.
/// </summary>
public interface IKnowledgeSource
{
    /// <summary>Short identifier for the corpus (e.g. file name).</summary>
    string Name { get; }

    /// <summary>SHA-256 hex hash of the corpus content, for drift detection.</summary>
    string ContentHash { get; }

    /// <summary>The reference text injected into agent prompts.</summary>
    string ReferenceBlock { get; }
}
