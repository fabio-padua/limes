using System.Security.Cryptography;
using System.Text;

namespace Limes.Agents.Knowledge;

/// <summary>
/// Minerva — file-backed knowledge grounding. Loads the curated AI CoE corpus
/// (<c>knowledge/ai-coe-knowledge.md</c>) and content-hashes it so agents can cite a
/// stable, drift-detectable reference. This is the lean Phase 2 grounding strategy.
/// </summary>
public sealed class MinervaKnowledgeSource : IKnowledgeSource
{
    public string Name { get; }
    public string ContentHash { get; }
    public string ReferenceBlock { get; }

    public MinervaKnowledgeSource(string name, string content)
    {
        Name = name;
        ReferenceBlock = content ?? string.Empty;
        ContentHash = ComputeHash(ReferenceBlock);
    }

    /// <summary>Loads the corpus from a Markdown file. Returns <c>null</c> if the file is missing.</summary>
    public static MinervaKnowledgeSource? TryLoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        var content = File.ReadAllText(path);
        return new MinervaKnowledgeSource(Path.GetFileName(path), content);
    }

    /// <summary>A compact "name@shorthash" descriptor for deliverable footers.</summary>
    public string Descriptor => $"{Name}@{ContentHash[..Math.Min(12, ContentHash.Length)]}";

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
