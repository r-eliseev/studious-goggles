using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Storage;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using NSubstitute;

namespace ImageGen.Presentation.Tests.PageModels;

public class RunItemViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static Run MakeRun(string prompt = "red fox") => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Prompt = prompt,
        Origin = RunOrigin.Assistant,
        CreatedAt = Now,
    };

    private static GenerationItemViewModel MakeCard(int sortOrder)
    {
        var generation = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ProviderId = "openrouter",
            ModelId = "vendor/model",
            Prompt = "red fox",
            Seed = sortOrder,
            SeedApplied = true,
            Status = GenerationStatus.Queued,
            CreatedAt = Now,
            SortOrder = sortOrder,
        };
        var files = Substitute.For<IImageFileStore>();
        return new GenerationItemViewModel(generation, files, _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
    }

    private static CopyTextAction Copy() =>
        new(Substitute.For<IClipboardService>(), Substitute.For<IMainThread>(), "text");

    [Fact]
    public void PromptText_UsesRunPromptFormat()
    {
        var item = new RunItemViewModel(MakeRun("red fox"), [], Copy());

        Assert.Equal(string.Format(AppResources.RunPromptFormat, "red fox"), item.PromptText);
    }

    [Fact]
    public void Constructor_CopiesIdAndKeepsCardsInGivenOrder()
    {
        var run = MakeRun();
        var first = MakeCard(0);
        var second = MakeCard(1);

        var item = new RunItemViewModel(run, [first, second], Copy());

        Assert.Equal(run.Id, item.Id);
        Assert.Equal(2, item.Generations.Count);
        Assert.Same(first, item.Generations[0]);
        Assert.Same(second, item.Generations[1]);
    }

    [Fact]
    public void Constructor_NullRun_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RunItemViewModel(null!, [], Copy()));
    }

    [Fact]
    public void Constructor_NullGenerations_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RunItemViewModel(MakeRun(), null!, Copy()));
    }
}
