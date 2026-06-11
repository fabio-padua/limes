using Limes.Agents.Pipeline;
using Limes.Agents.Reference;
using Limes.Core.Domain;

namespace Limes.Agents.Deterministic;

/// <summary>
/// Egeria (the counselor) — Skilling. Maps each below-target pillar to a role-based Microsoft
/// Learn path, with extra emphasis on Organization &amp; Culture gaps. Recommendations cite the
/// specific gap they address so the skilling plan is traceable back to the assessment.
/// </summary>
public sealed class EgeriaSkillingAgent : ILimesAgent
{
    /// <summary>
    /// Skilling score threshold. A pillar generates a recommendation when it scores below this
    /// value <em>or</em> has any flagged gaps — so an above-threshold pillar with residual gaps is
    /// still covered (see <c>needsSkilling</c> in <see cref="RunAsync"/>).
    /// </summary>
    public const double SkillThreshold = 3.0;

    public string Codename => "Egeria";
    public string Role => "Skilling";

    public Task RunAsync(AssessmentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var scoring = context.RequireScoring();

        var recommendations = new List<SkillingRecommendation>();
        foreach (var p in scoring.PillarScores)
        {
            var needsSkilling = p.Score < SkillThreshold || p.Gaps.Count > 0;
            if (!needsSkilling)
                continue;

            var (path, url, role) = AssessmentReference.LearnPath(p.Pillar);
            recommendations.Add(new SkillingRecommendation
            {
                Pillar = p.Pillar,
                Gap = p.Gaps.Count > 0 ? p.Gaps[0] : $"Below-target maturity ({p.Level.DisplayName()}).",
                LearnPath = path,
                Url = url,
                Role = role,
            });
        }

        var ordered = recommendations.OrderBy(r => (int)r.Pillar).ToList();
        context.SkillingPlan = new SkillingPlan { Recommendations = ordered };
        context.Note(Codename, $"mapped {ordered.Count} Microsoft Learn skilling recommendation(s).");

        return Task.CompletedTask;
    }
}
