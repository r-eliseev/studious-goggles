using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Services;

/// <summary>What a run start did: the run (null when every model was skipped), the models skipped and the reference images not sent.</summary>
/// <param name="ReferencesNotSent">The largest number of attachments that did not fit in one model's reference limit.</param>
public sealed record StartRunResult(Run? Run, IReadOnlyList<SkippedModel> SkippedModels, int ReferencesNotSent);

/// <summary>A model of the session that got no generation in the run, and why.</summary>
public sealed record SkippedModel(ModelRef Model, ModelSkipReason Reason);

public enum ModelSkipReason
{
    /// <summary>The model takes no reference images and the user attached some.</summary>
    NoReferenceSupport,
}
