using ImageGen.Core.Domain.Descriptions;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace ImageGen.Presentation.Tests.Services;

public class ErrorAlertTests
{
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingLogger<ErrorAlert> _logger = new();
    private readonly ErrorAlert _alert;

    public ErrorAlertTests() => _alert = new ErrorAlert(_dialogs, _logger);

    [Fact]
    public async Task ShowAsync_ProviderException_UsesLocalizedCodeInsteadOfMessage()
    {
        await _alert.ShowAsync(new ProviderException(ErrorKind.ModelWithoutReferences, "core English detail"));

        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, AppResources.ErrorKindModelWithoutReferences);
    }

    [Fact]
    public async Task ShowAsync_ImageDescriptionException_UsesLocalizedDescriptionError()
    {
        await _alert.ShowAsync(new ImageDescriptionException(DescriptionError.SourceMissing, "private path detail"));

        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, AppResources.DescriptionSourceMissing);
    }

    [Fact]
    public async Task ShowAsync_UnknownException_UsesGenericTextWithoutDetails()
    {
        var exception = new InvalidOperationException("/private/user/path/database.db");
        await _alert.ShowAsync(exception);

        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, AppResources.ErrorKindUnknown);
        await _dialogs.DidNotReceive().AlertAsync(Arg.Any<string>(), Arg.Is<string>(text => text.Contains("database.db")));
        var warning = Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.DoesNotContain("database.db", warning.Message);
        Assert.Equal(exception, Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Debug).Exception);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
