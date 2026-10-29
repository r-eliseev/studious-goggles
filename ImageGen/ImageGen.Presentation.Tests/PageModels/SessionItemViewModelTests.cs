using System.Globalization;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.Tests.PageModels;

public class SessionItemViewModelTests
{
    private static readonly DateTimeOffset UpdatedAt = new(2026, 10, 27, 9, 30, 0, TimeSpan.Zero);

    private static Session CreateSession() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Red fox in snow",
        CreatedAt = UpdatedAt,
        UpdatedAt = UpdatedAt,
        Settings = SessionSettings.CreateDefault(),
    };

    [Fact]
    public void Constructor_CopiesIdAndTitle()
    {
        var session = CreateSession();

        var item = new SessionItemViewModel(session, _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask);

        Assert.Equal(session.Id, item.Id);
        Assert.Equal("Red fox in snow", item.Title);
    }

    [Fact]
    public void Constructor_FormatsUpdatedTimeInLocalTimeWithGeneralFormat()
    {
        var session = CreateSession();

        var item = new SessionItemViewModel(session, _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask);

        Assert.Equal(
            UpdatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
            item.UpdatedText);
    }

    [Fact]
    public async Task OpenCommand_Executes_CallsOpenWithThisItem()
    {
        SessionItemViewModel? opened = null;
        var item = new SessionItemViewModel(
            CreateSession(),
            open: target => { opened = target; return Task.CompletedTask; },
            rename: _ => Task.CompletedTask,
            delete: _ => Task.CompletedTask);

        item.OpenCommand.Execute(null);
        await item.OpenCommand.ExecutionTask!;

        Assert.Same(item, opened);
    }

    [Fact]
    public async Task DeleteCommand_Executes_CallsDeleteWithThisItem()
    {
        SessionItemViewModel? deleted = null;
        var item = new SessionItemViewModel(
            CreateSession(),
            open: _ => Task.CompletedTask,
            rename: _ => Task.CompletedTask,
            delete: target => { deleted = target; return Task.CompletedTask; });

        item.DeleteCommand.Execute(null);
        await item.DeleteCommand.ExecutionTask!;

        Assert.Same(item, deleted);
    }

    [Fact]
    public void DetailsText_OneRun_UsesSingularWord()
    {
        var item = new SessionItemViewModel(CreateSession(), _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask)
        {
            RunCount = 1,
        };

        Assert.Equal($"{item.UpdatedText} · {AppResources.RunsCountOne}", item.DetailsText);
    }

    [Fact]
    public void DetailsText_SeveralRuns_ShowsCount()
    {
        var item = new SessionItemViewModel(CreateSession(), _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask)
        {
            RunCount = 3,
        };

        Assert.Equal($"{item.UpdatedText} · {string.Format(CultureInfo.CurrentCulture, AppResources.RunsCountFormat, 3)}", item.DetailsText);
    }

    [Fact]
    public async Task RenameCommand_Executes_CallsRenameWithThisItem()
    {
        SessionItemViewModel? renamed = null;
        var item = new SessionItemViewModel(
            CreateSession(),
            open: _ => Task.CompletedTask,
            rename: target => { renamed = target; return Task.CompletedTask; },
            delete: _ => Task.CompletedTask);

        item.RenameCommand.Execute(null);
        await item.RenameCommand.ExecutionTask!;

        Assert.Same(item, renamed);
    }
}
