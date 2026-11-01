using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Services;

/// <summary>The assistant model assigned to newly created sessions.</summary>
public interface IDefaultAssistantModel
{
    ModelRef Current { get; }

    Task SaveAsync(ModelRef model, CancellationToken cancellationToken);
}
