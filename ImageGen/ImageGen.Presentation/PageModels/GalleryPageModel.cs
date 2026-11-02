using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Gallery;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Storage;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.PageModels;

public enum GalleryPeriod { All, Last7Days, Last30Days }

/// <summary>Virtualized gallery backed by stable keyset pages from storage.</summary>
public sealed partial class GalleryPageModel(
    IGalleryRepository gallery,
    ISessionRepository sessions,
    IImageFileStore files,
    INavigationService navigation,
    IDialogService dialogs,
    TimeProvider timeProvider,
    TimeZoneInfo? localTimeZone = null) : ObservableObject, IDisposable
{
    public const int PageSize = 40;
    private readonly TimeZoneInfo _zone = localTimeZone ?? TimeZoneInfo.Local;
    private CancellationTokenSource? _loadCancellation;
    private int _requestEpoch;

    public ObservableCollection<GalleryItemViewModel> Items { get; } = [];
    public ObservableCollection<GalleryModelFilter> Models { get; } = [];
    public IReadOnlyList<string> PeriodOptions { get; } =
        [AppResources.GalleryAllTime, AppResources.GalleryLast7Days, AppResources.GalleryLast30Days];

    [ObservableProperty] public partial GalleryPeriod SelectedPeriod { get; set; } = GalleryPeriod.All;
    [ObservableProperty] public partial string SelectedPeriodLabel { get; set; } = AppResources.GalleryAllTime;
    [ObservableProperty] public partial GalleryModelFilter? SelectedModel { get; set; }
    [ObservableProperty] public partial bool HasMore { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string ErrorText { get; set; } = "";

    partial void OnSelectedPeriodLabelChanged(string value) => SelectedPeriod = value switch
    {
        var label when label == AppResources.GalleryLast7Days => GalleryPeriod.Last7Days,
        var label when label == AppResources.GalleryLast30Days => GalleryPeriod.Last30Days,
        _ => GalleryPeriod.All,
    };

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var selected = SelectedModel?.Reference;
            Models.Clear();
            Models.Add(new GalleryModelFilter(null, AppResources.GalleryAllModels));
            foreach (var model in await gallery.ListModelsAsync(CancellationToken.None))
            {
                Models.Add(new GalleryModelFilter(model, $"{model.ProviderId} · {model.ModelId}"));
            }
            SelectedModel = Models.FirstOrDefault(option => option.Reference == selected) ?? Models[0];
            await ResetAndLoadAsync();
        }
        catch (Exception)
        {
            ErrorText = AppResources.GalleryLoadFailed;
        }
    }

    [RelayCommand]
    private Task ApplyFiltersAsync() => ResetAndLoadAsync();

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || !HasMore) return;
        await LoadPageAsync(clear: false);
    }

    [RelayCommand]
    private async Task OpenAsync(GalleryItemViewModel? item)
    {
        if (item is null) return;
        var session = await sessions.GetAsync(item.SessionId, CancellationToken.None);
        if (session is null || !File.Exists(files.GetFullPath(item.RelativeImagePath)))
        {
            item.IsUnavailable = true;
            item.StatusText = AppResources.GalleryUnavailableMessage;
            await dialogs.AlertAsync(AppResources.GalleryTitle, item.StatusText);
            return;
        }
        await navigation.GoToAsync(Routes.ChatFor(item.SessionId));
    }

    private async Task ResetAndLoadAsync()
    {
        _requestEpoch++;
        CancelActiveLoad();
        HasMore = true;
        await LoadPageAsync(clear: true);
    }

    private async Task LoadPageAsync(bool clear)
    {
        var epoch = _requestEpoch;
        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        IsBusy = true;
        ErrorText = "";
        if (clear)
        {
            Items.Clear();
            _cursor = null;
        }
        try
        {
            var (from, to) = PeriodBounds();
            var query = new GalleryQuery(PageSize, clear ? null : CursorFromItems(), SelectedModel?.Reference, from, to);
            var page = await gallery.ListAsync(query, cancellation.Token);
            if (epoch != _requestEpoch) return;
            foreach (var row in page.Items)
            {
                var item = new GalleryItemViewModel(row, files.GetFullPath(row.ImagePath));
                if (!File.Exists(files.GetFullPath(row.ImagePath)))
                {
                    item.IsUnavailable = true;
                    item.StatusText = AppResources.GalleryUnavailableMessage;
                }
                Items.Add(item);
            }
            _cursor = page.NextCursor;
            HasMore = page.NextCursor is not null;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (epoch == _requestEpoch) ErrorText = AppResources.GalleryLoadFailed;
        }
        finally
        {
            if (epoch == _requestEpoch)
            {
                IsBusy = false;
            }
            if (ReferenceEquals(_loadCancellation, cancellation)) _loadCancellation = null;
            cancellation.Dispose();
        }
    }

    private GalleryCursor? CursorFromItems() => _cursor;

    private GalleryCursor? _cursor;

    private (DateTimeOffset? From, DateTimeOffset? To) PeriodBounds()
    {
        if (SelectedPeriod == GalleryPeriod.All) return (null, null);
        var now = timeProvider.GetUtcNow();
        var localToday = TimeZoneInfo.ConvertTime(now, _zone).Date;
        var days = SelectedPeriod == GalleryPeriod.Last7Days ? 7 : 30;
        var localStart = DateTime.SpecifyKind(localToday.AddDays(-(days - 1)), DateTimeKind.Unspecified);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, _zone);
        return (new DateTimeOffset(fromUtc), now);
    }

    private void CancelActiveLoad()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
    }

    public void Dispose()
    {
        _requestEpoch++;
        CancelActiveLoad();
    }
}
