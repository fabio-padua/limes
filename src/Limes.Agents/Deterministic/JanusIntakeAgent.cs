using Limes.Agents.Pipeline;
using Limes.Core.Domain;

namespace Limes.Agents.Deterministic;

/// <summary>
/// Janus (doorways; present ↔ future) — Intake / Assessor. Normalizes the raw intake into a
/// canonical shape: deduplicates repeated question ids within a pillar (keeping the first),
/// and ensures all seven AI Readiness pillars are represented so the assessment is complete.
/// The deterministic implementation performs structural normalization only — no LLM follow-up
/// questions — which keeps it CI-safe.
/// </summary>
public sealed class JanusIntakeAgent : ILimesAgent
{
    public string Codename => "Janus";
    public string Role => "Intake / Assessor";

    public Task RunAsync(AssessmentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var byPillar = new Dictionary<Pillar, PillarResponse>();
        var duplicatePillars = 0;
        foreach (var p in context.Intake.Pillars)
        {
            // Tolerate duplicate pillar blocks in the intake: keep the first occurrence.
            if (!byPillar.TryAdd(p.Pillar, p))
                duplicatePillars++;
        }

        var normalized = new List<PillarResponse>(PillarInfo.All.Count);
        var duplicatesDropped = 0;
        var pillarsAdded = 0;

        foreach (var pillar in PillarInfo.All)
        {
            if (byPillar.TryGetValue(pillar, out var existing))
            {
                var deduped = Dedupe(existing.Responses, out var dropped);
                duplicatesDropped += dropped;
                normalized.Add(existing with { Responses = deduped });
            }
            else
            {
                pillarsAdded++;
                normalized.Add(new PillarResponse { Pillar = pillar, Responses = [] });
            }
        }

        context.Intake = context.Intake with { Pillars = normalized };
        context.Note(Codename,
            $"normalized {normalized.Count} pillars (added {pillarsAdded} missing, dropped " +
            $"{duplicatePillars} duplicate pillar block(s) and {duplicatesDropped} duplicate response(s)).");

        return Task.CompletedTask;
    }

    private static IReadOnlyList<QuestionResponse> Dedupe(IReadOnlyList<QuestionResponse> responses, out int dropped)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<QuestionResponse>(responses.Count);
        foreach (var r in responses)
        {
            if (seen.Add(r.QuestionId))
                result.Add(r);
        }
        dropped = responses.Count - result.Count;
        return result;
    }
}
