using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Storage;
using ImageGen.Maui.Resources.Strings;

namespace ImageGen.Maui.PageModels;

/// <summary>Card of one generation. Commands call back into the owning page model.</summary>
public sealed partial class GenerationItemViewModel : ObservableObject
{
    private readonly IImageFileStore _files;

    public GenerationItemViewModel(
        Generation generation,
        IImageFileStore files,
        Func<GenerationItemViewModel, Task> repeat,
        Func<GenerationItemViewModel, Task> newSeed,
        Func<GenerationItemViewModel, Task> edit)
    {
        _files = files;
        Id = generation.Id;
        Prompt = generation.Prompt;
        RepeatCommand = new AsyncRelayCommand(() => repeat(this), () => IsFinished);
        NewSeedCommand = new AsyncRelayCommand(() => newSeed(this), () => IsFinished);
        EditCommand = new AsyncRelayCommand(() => edit(this), () => IsFinished);
        Update(generation);
    }

    public Guid Id { get; }

    /// <summary>Exact prompt of the generation, used by the Edit action.</summary>
    public string Prompt { get; }

    public IAsyncRelayCommand RepeatCommand { get; }

    public IAsyncRelayCommand NewSeedCommand { get; }

    public IAsyncRelayCommand EditCommand { get; }

    [ObservableProperty]
    public partial ImageSource? Image { get; set; }

    [ObservableProperty]
    public partial bool HasImage { get; set; }

    [ObservableProperty]
    public partial bool IsFinished { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    /// <summary>Applies the latest saved state of the generation. Call on the UI thread.</summary>
    public void Update(Generation generation)
    {
        StatusText = generation.Status switch
        {
            GenerationStatus.Queued => AppResources.StatusQueued,
            GenerationStatus.Running => AppResources.StatusRunning,
            GenerationStatus.Succeeded => AppResources.StatusSucceeded,
            GenerationStatus.Failed => AppResources.StatusFailed,
            GenerationStatus.Cancelled => AppResources.StatusCancelled,
            GenerationStatus.Filtered => AppResources.StatusFiltered,
            _ => generation.Status.ToString(),
        };

        var seedText = generation.SeedApplied
            ? string.Format(CultureInfo.CurrentCulture, AppResources.SeedFormat, generation.Seed)
            : AppResources.SeedNotSupported;
        var costText = generation.CostUsd.HasValue
            ? " · " + string.Format(CultureInfo.CurrentCulture, AppResources.CostFormat, generation.CostUsd.Value.ToString("0.####", CultureInfo.InvariantCulture))
            : string.Empty;
        DetailsText = $"{generation.ModelId} · {seedText}{costText}";
        ErrorText = generation.ErrorMessage ?? string.Empty;

        HasImage = generation.Status == GenerationStatus.Succeeded && generation.ImagePath is not null;
        Image = HasImage ? ImageSource.FromFile(_files.GetFullPath(generation.ImagePath!)) : null;
        IsRunning = generation.Status is GenerationStatus.Queued or GenerationStatus.Running;
        IsFinished = generation.IsFinished;
        RepeatCommand.NotifyCanExecuteChanged();
        NewSeedCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
    }
}
