using System.Text.Json;
using System.Text.Json.Serialization;
using Limes.Core.Domain;

namespace Limes.Core.Reporting;

/// <summary>Serializes an <see cref="AssessmentResult"/> to structured JSON for downstream tooling.</summary>
public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Write(AssessmentResult result) => JsonSerializer.Serialize(result, Options);

    /// <summary>Serializes the full Phase 2 deliverable (scores + roadmap + skilling + risk).</summary>
    public static string Write(AssessmentDeliverable deliverable) => JsonSerializer.Serialize(deliverable, Options);
}
