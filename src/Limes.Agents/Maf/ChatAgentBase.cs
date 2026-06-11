using Limes.Agents.Knowledge;
using Limes.Agents.Pipeline;
using Microsoft.Agents.AI;

namespace Limes.Agents.Maf;

/// <summary>
/// Base class for MAF-backed (Foundry) agents. Wraps a Microsoft Agent Framework
/// <see cref="AIAgent"/> and, when a <see cref="IKnowledgeSource"/> is supplied, prepends a
/// citable <c>REFERENCE KNOWLEDGE</c> block to every prompt (Minerva's prompt-level grounding).
/// </summary>
public abstract class ChatAgentBase : ILimesAgent
{
    private readonly AIAgent _agent;
    private readonly IKnowledgeSource? _knowledge;

    protected ChatAgentBase(AIAgent agent, IKnowledgeSource? knowledge = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _knowledge = knowledge;
    }

    public abstract string Codename { get; }
    public abstract string Role { get; }

    /// <summary>The grounding corpus injected into prompts, if any (visible to subclasses).</summary>
    protected IKnowledgeSource? Knowledge => _knowledge;

    /// <summary>A compact "name@shorthash" descriptor for the grounding corpus, or <c>null</c> when ungrounded.</summary>
    protected string? KnowledgeDescriptor => _knowledge is null
        ? null
        : $"{_knowledge.Name}@{ShortHash(_knowledge.ContentHash)}";

    public abstract Task RunAsync(AssessmentContext context, CancellationToken cancellationToken = default);

    /// <summary>Runs a single-turn prompt against the underlying MAF agent, with grounding.</summary>
    protected async Task<string> AskAsync(string prompt, CancellationToken cancellationToken)
    {
        var grounded = _knowledge is null
            ? prompt
            : $"REFERENCE KNOWLEDGE (source: {_knowledge.Name}, hash: {ShortHash(_knowledge.ContentHash)}):\n" +
              $"{_knowledge.ReferenceBlock}\n\n---\n\n{prompt}";

        var response = await _agent.RunAsync(grounded, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Text?.Trim() ?? string.Empty;
    }

    private static string ShortHash(string hash) => hash[..Math.Min(12, hash.Length)];
}
