using System.Text.Json;
using System.Text.Json.Serialization;
using Limes.Core.Domain;

namespace Limes.Core.Intake;

/// <summary>Loads an <see cref="AssessmentIntake"/> from JSON (file-drop intake, v1).</summary>
public static class IntakeLoader
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AssessmentIntake FromJson(string json)
    {
        var intake = JsonSerializer.Deserialize<AssessmentIntake>(json, JsonOptions)
            ?? throw new InvalidDataException("Intake JSON deserialized to null.");
        return intake;
    }

    public static async Task<AssessmentIntake> FromFileAsync(string path, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(path, ct);
        return FromJson(json);
    }
}
