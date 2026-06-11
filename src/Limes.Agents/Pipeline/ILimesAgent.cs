namespace Limes.Agents.Pipeline;

/// <summary>
/// One stage of the Limes assessment pipeline. Every Roman-codenamed agent implements this:
/// it reads the shared <see cref="AssessmentContext"/> and writes its contribution back into it.
/// Deterministic implementations run with $0 model cost; MAF-backed implementations layer
/// reasoned narrative on top of the same contract.
/// </summary>
public interface ILimesAgent
{
    /// <summary>Roman-deity codename (e.g. "Janus").</summary>
    string Codename { get; }

    /// <summary>Human-readable role (e.g. "Intake / Assessor").</summary>
    string Role { get; }

    Task RunAsync(AssessmentContext context, CancellationToken cancellationToken = default);
}
