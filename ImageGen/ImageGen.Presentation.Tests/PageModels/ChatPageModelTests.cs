using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Descriptions;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;

namespace ImageGen.Presentation.Tests.PageModels;

public class ChatPageModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private const string ImageModel = "vendor/image";
    private const string AssistantModel = "vendor/assistant";
    private static readonly byte[] Image = [1, 2, 3];

    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IChatMessageRepository _messageRepository = Substitute.For<IChatMessageRepository>();
    private readonly IRunRepository _runRepository = Substitute.For<IRunRepository>();
    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
    private readonly IModelProvider _generator = Substitute.For<IModelProvider>();
    private readonly ISeedSource _seeds = Substitute.For<ISeedSource>();
    private readonly IImageModelRepository _modelRepository = Substitute.For<IImageModelRepository>();
    private readonly IAssistantChatClient _chat = Substitute.For<IAssistantChatClient>();
    private readonly IConversationSummaryRepository _summaries = Substitute.For<IConversationSummaryRepository>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IMainThread _mainThread = Substitute.For<IMainThread>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly IFilePickerService _filePicker = Substitute.For<IFilePickerService>();
    private readonly IChatAttachmentRepository _chatAttachments = Substitute.For<IChatAttachmentRepository>();
    private AttachmentService _attachments = null!;
    private ImageDescriptionService _descriptions = null!;

    // Stored rows. Guarded by _gate because background generations write to them.
    private readonly object _gate = new();
    private readonly List<ChatMessage> _messages = [];
    private readonly List<Run> _runs = [];
    private readonly List<Generation> _rows = [];

    private readonly Session _session;
    private readonly Session _otherSession;
    private readonly SessionService _sessionService;
    private readonly GenerationService _generationService;
    private readonly AssistantService _assistant;
    private readonly ChatPageModel _model;

    public ChatPageModelTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);

        _modelRepository.ListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
            [TestModels.Model(ImageModel, "Image")]);
        var catalog = new ModelCatalogService(ProviderDoubles.Router(), _modelRepository, NullLogger<ModelCatalogService>.Instance);

        _session = NewSession("Red fox", runsPerModel: 2);
        _otherSession = NewSession("Other", runsPerModel: 1);
        _sessionRepository.GetAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(_session);
        _sessionRepository.GetAsync(_otherSession.Id, Arg.Any<CancellationToken>()).Returns(_otherSession);
        _sessionRepository.UpdateAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        _messageRepository.AddAsync(Arg.Any<ChatMessage>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            lock (_gate)
            {
                _messages.Add(ci.Arg<ChatMessage>());
            }

            return Task.CompletedTask;
        });
        _messageRepository.ListBySessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var sessionId = ci.Arg<Guid>();
            lock (_gate)
            {
                return Task.FromResult<IReadOnlyList<ChatMessage>>(_messages.Where(m => m.SessionId == sessionId).ToList());
            }
        });

        _runRepository.AddRunAsync(Arg.Any<Run>(), Arg.Any<IReadOnlyList<Generation>>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            lock (_gate)
            {
                _runs.Add(ci.Arg<Run>());
                _rows.AddRange(ci.Arg<IReadOnlyList<Generation>>());
            }

            return Task.CompletedTask;
        });
        _runRepository.ListRunsBySessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var sessionId = ci.Arg<Guid>();
            lock (_gate)
            {
                return Task.FromResult<IReadOnlyList<Run>>(_runs.Where(r => r.SessionId == sessionId).ToList());
            }
        });
        _runRepository.ListGenerationsBySessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var sessionId = ci.Arg<Guid>();
            lock (_gate)
            {
                return Task.FromResult<IReadOnlyList<Generation>>(_rows.Where(g => g.SessionId == sessionId).ToList());
            }
        });
        _runRepository.GetGenerationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var id = ci.Arg<Guid>();
            lock (_gate)
            {
                return Task.FromResult(_rows.FirstOrDefault(g => g.Id == id));
            }
        });
        _runRepository.GetRunAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var id = ci.Arg<Guid>();
            lock (_gate)
            {
                return Task.FromResult(_runs.FirstOrDefault(r => r.Id == id));
            }
        });
        _runRepository.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        _seeds.NextSeed().Returns(7L);
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ImageGenerationResult(Image, "image/png", 0.014m));
        _files.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("images/s/g.png");
        _files.GetFullPath(Arg.Any<string>()).Returns(ci => "/data/" + ci.Arg<string>());

        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AssistantChatResult("Here you go.", [], null));

        _mainThread.When(m => m.BeginInvokeOnMainThread(Arg.Any<Action>()))
            .Do(ci => ci.Arg<Action>().Invoke());

        _sessionService = new SessionService(
            _sessionRepository, _messageRepository, _runRepository, _files,
            new ChatModelCatalogService(ProviderDoubles.Router(chat: _chat), NullLogger<ChatModelCatalogService>.Instance),
            time, _summaries, new SessionChangeNotifier());
        _attachments = new AttachmentService(_chatAttachments, _files, time);
        _descriptions = new ImageDescriptionService(
            _sessionRepository, _runRepository, ProviderDoubles.Router(chat: _chat),
            new ChatModelCatalogService(ProviderDoubles.Router(chat: _chat), NullLogger<ChatModelCatalogService>.Instance),
            _files, Substitute.For<IImageDescriptionRepository>(), time);
        _files.SaveAttachmentAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => $"images/s/attachments/{ci.ArgAt<Guid>(1):N}.png");
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _generationService = new GenerationService(
            _sessionRepository, _runRepository, ProviderDoubles.Router(_generator), _files, catalog, _seeds, _attachments, time, NullLogger<GenerationService>.Instance, new SessionChangeNotifier());
        _assistant = new AssistantService(
            _sessionService, _messageRepository, _runRepository, ProviderDoubles.Router(chat: _chat), _generationService,
            new ChatModelCatalogService(ProviderDoubles.Router(chat: _chat), NullLogger<ChatModelCatalogService>.Instance),
            _files, _attachments, time, NullLogger<AssistantService>.Instance, new ImageGen.Core.Context.TokenCorrection(),
            Substitute.For<IConversationSummaryRepository>(),
            new ConversationSummaryService(
                Substitute.For<ISessionRepository>(), Substitute.For<IChatMessageRepository>(), Substitute.For<IRunRepository>(),
                Substitute.For<IConversationSummaryRepository>(), ProviderDoubles.Router(chat: _chat),
                new ChatModelCatalogService(ProviderDoubles.Router(chat: _chat), NullLogger<ChatModelCatalogService>.Instance),
                new ImageGen.Core.Context.TokenCorrection(), time, NullLogger<ConversationSummaryService>.Instance),
            ImageGen.Core.Diagnostics.NullDebugTrace.Instance);

        _model = CreateModel(_session.Id);
    }

    private static Session NewSession(string title, int runsPerModel) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        CreatedAt = Now,
        UpdatedAt = Now,
        Settings = new SessionSettings
        {
            ImageModels = [new ModelRef(ProviderIds.OpenRouter, ImageModel)],
            RunsPerModel = runsPerModel,
            AssistantModel = new ModelRef(ProviderIds.OpenRouter, AssistantModel),
        },
    };

    private readonly CurrentSessionTracker _tracker = new();

    private ChatModelCatalogService ChatModels => new(ProviderDoubles.Router(chat: _chat), NullLogger<ChatModelCatalogService>.Instance);

    private ModelCatalogService Catalog => new(ProviderDoubles.Router(), _modelRepository, NullLogger<ModelCatalogService>.Instance);

    private SessionModelsPageModel NewModelsPane() =>
        new(_sessionService, Catalog, ChatModels, _navigation, _dialogs);

    private SessionParametersPageModel NewParametersPane() =>
        new(_sessionService, Catalog, _navigation, _dialogs);

    private ChatPageModel CreateModel(Guid sessionId)
    {
        var model = new ChatPageModel(
            _sessionService,
            _assistant,
            _generationService,
            _files,
            _navigation,
            _dialogs,
            _mainThread,
            _attachments,
            _filePicker,
            _descriptions,
            _clipboard,
            Substitute.For<IImageSaver>(),
            _tracker,
            NewModelsPane,
            NewParametersPane);
        model.ApplyQueryAttributes(new Dictionary<string, object>
        {
            [Routes.SessionIdParameter] = sessionId.ToString("D"),
        });
        return model;
    }

    private IEnumerable<GenerationItemViewModel> Cards =>
        _model.Items.OfType<RunItemViewModel>().SelectMany(run => run.Generations);

    private IReadOnlyList<Run> StoredRuns
    {
        get
        {
            lock (_gate)
            {
                return _runs.ToList();
            }
        }
    }

    private List<Generation> RowsOf(Guid runId)
    {
        lock (_gate)
        {
            return _rows.Where(g => g.RunId == runId).ToList();
        }
    }

    private void StoreMessage(ChatMessage message)
    {
        lock (_gate)
        {
            _messages.Add(message);
        }
    }

    private void StoreRun(Run run, params Generation[] generations)
    {
        lock (_gate)
        {
            _runs.Add(run);
            _rows.AddRange(generations);
        }
    }

    private static Run NewRun(Guid sessionId, string prompt) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        Prompt = prompt,
        Origin = RunOrigin.Assistant,
        CreatedAt = Now,
    };

    private static Generation NewGeneration(Run run, long seed, GenerationStatus status = GenerationStatus.Succeeded) => new()
    {
        Id = Guid.NewGuid(),
        RunId = run.Id,
        SessionId = run.SessionId,
        ProviderId = "openrouter",
        ModelId = ImageModel,
        Prompt = run.Prompt,
        Seed = seed,
        SeedApplied = true,
        Status = status,
        ImagePath = status == GenerationStatus.Succeeded ? "images/s/done.png" : null,
        CreatedAt = Now,
        SortOrder = 0,
    };

    [Fact]
    public async Task OnAppearingAsync_EmptySession_ShowsTitleModelsButtonAndNoItems()
    {
        await _model.OnAppearingAsync();

        Assert.Equal("Red fox", _model.Title);
        Assert.Equal(string.Format(AppResources.ModelsButtonFormat, 1, 2), _model.ModelsButtonText);
        Assert.Empty(_model.Items);
    }

    [Fact]
    public async Task OnAppearingAsync_ShowsMessagesAndRunsInTimelineOrder()
    {
        var run = NewRun(_session.Id, "red fox");
        var generations = new[] { NewGeneration(run, 5), NewGeneration(run, 6) };
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.User, Text = "draw a fox", CreatedAt = Now });
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.Assistant, Text = "Drawing.", CreatedAt = Now, RunId = run.Id });
        StoreRun(run, generations);

        await _model.OnAppearingAsync();

        Assert.Equal(3, _model.Items.Count);
        Assert.IsType<MessageItemViewModel>(_model.Items[0]);
        Assert.IsType<MessageItemViewModel>(_model.Items[1]);
        var runItem = Assert.IsType<RunItemViewModel>(_model.Items[2]);
        Assert.Equal(2, runItem.Generations.Count);
    }

    [Fact]
    public async Task OnAppearingAsync_LastMessageIsError_CanRetry()
    {
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.User, Text = "a fox", CreatedAt = Now });
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.Assistant, Text = "error", CreatedAt = Now.AddSeconds(1), IsError = true });

        await _model.OnAppearingAsync();

        Assert.True(Assert.IsType<MessageItemViewModel>(_model.Items[^1]).CanRetry);
    }

    [Fact]
    public async Task OnAppearingAsync_ErrorNotLast_CannotRetry()
    {
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.User, Text = "a fox", CreatedAt = Now });
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.Assistant, Text = "error", CreatedAt = Now.AddSeconds(1), IsError = true });
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.Assistant, Text = "Here", CreatedAt = Now.AddSeconds(2) });

        await _model.OnAppearingAsync();

        Assert.All(_model.Items.OfType<MessageItemViewModel>(), item => Assert.False(item.CanRetry));
    }

    [Fact]
    public async Task Retry_SendsLastMessageAgainAndReloadsTimeline()
    {
        // The reply is stamped with the test clock (Now), so the stored messages are older than it.
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.User, Text = "a fox", CreatedAt = Now.AddMinutes(-2) });
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.Assistant, Text = "error", CreatedAt = Now.AddMinutes(-1), IsError = true });
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AssistantChatResult("Here you go", [], null));
        await _model.OnAppearingAsync();

        await Assert.IsType<MessageItemViewModel>(_model.Items[^1]).RetryCommand.ExecuteAsync(null);

        await _chat.Received(1).CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>());
        var last = Assert.IsType<MessageItemViewModel>(_model.Items[^1]);
        Assert.Equal("Here you go", last.Text);
        Assert.False(last.IsError);
        Assert.False(_model.IsBusy);
    }

    [Fact]
    public async Task Retry_WhileBusy_CannotExecute()
    {
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.User, Text = "a fox", CreatedAt = Now });
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.Assistant, Text = "error", CreatedAt = Now.AddSeconds(1), IsError = true });
        await _model.OnAppearingAsync();
        var error = Assert.IsType<MessageItemViewModel>(_model.Items[^1]);
        Assert.True(error.RetryCommand.CanExecute(null));

        _model.IsBusy = true;

        Assert.False(error.RetryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Query_WithMessage_SendsItOnceAfterLoad()
    {
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AssistantChatResult("Hi", [], null));
        _model.ApplyQueryAttributes(new Dictionary<string, object>
        {
            [Routes.SessionIdParameter] = _session.Id.ToString("D"),
            [Routes.MessageParameter] = Uri.EscapeDataString("A lighthouse at dusk"),
        });

        await _model.OnAppearingAsync();

        await _chat.Received(1).CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>());
        Assert.Contains(_messages, m => m.Role == ChatRole.User && m.Text == "A lighthouse at dusk");
    }

    [Fact]
    public async Task Query_WithoutMessage_SendsNothing()
    {
        await _model.OnAppearingAsync();

        await _chat.DidNotReceive().CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reload_AfterPendingMessageSent_DoesNotSendAgain()
    {
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AssistantChatResult("Hi", [], null));
        _model.ApplyQueryAttributes(new Dictionary<string, object>
        {
            [Routes.SessionIdParameter] = _session.Id.ToString("D"),
            [Routes.MessageParameter] = Uri.EscapeDataString("A lighthouse at dusk"),
        });
        await _model.OnAppearingAsync();

        await _model.OnAppearingAsync();

        await _chat.Received(1).CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAppearingAsync_WithSummary_InsertsMarkerAfterCoveredMessage()
    {
        var first = new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.User, Text = "first", CreatedAt = Now.AddMinutes(-3) };
        var covered = new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.Assistant, Text = "covered", CreatedAt = Now.AddMinutes(-2) };
        var later = new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.User, Text = "later", CreatedAt = Now.AddMinutes(-1) };
        StoreMessage(first);
        StoreMessage(covered);
        StoreMessage(later);
        _summaries.GetAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(NewSummary(covered.Id));

        await _model.OnAppearingAsync();

        Assert.IsType<SummaryMarkerViewModel>(_model.Items[2]);
        Assert.Equal("later", Assert.IsType<MessageItemViewModel>(_model.Items[3]).Text);
    }

    [Fact]
    public async Task OnAppearingAsync_NoSummary_NoMarker()
    {
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.User, Text = "hi", CreatedAt = Now });

        await _model.OnAppearingAsync();

        Assert.DoesNotContain(_model.Items, item => item is SummaryMarkerViewModel);
    }

    [Fact]
    public async Task ShowSummary_AlertsSummaryWithModelAndPrice()
    {
        var covered = Guid.NewGuid();
        StoreMessage(new ChatMessage { Id = covered, SessionId = _session.Id, Role = ChatRole.User, Text = "hi", CreatedAt = Now });
        _summaries.GetAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(NewSummary(covered));
        await _model.OnAppearingAsync();
        var marker = Assert.IsType<SummaryMarkerViewModel>(_model.Items[1]);

        await marker.ShowCommand.ExecuteAsync(null);

        await _dialogs.Received(1).AlertAsync(
            AppResources.SummaryTitle,
            Arg.Is<string>(text => text.StartsWith("The user wants a red fox.", StringComparison.Ordinal)
                && text.Contains("vendor/assistant", StringComparison.Ordinal)
                && text.Contains("$0.002", StringComparison.Ordinal)));
    }

    private static ConversationSummary NewSummary(Guid coversUpTo) => new()
    {
        SessionId = Guid.Empty,
        Text = "The user wants a red fox.",
        CoversUpToMessageId = coversUpTo,
        Model = new ModelRef(ProviderIds.OpenRouter, "vendor/assistant"),
        CostUsd = 0.002m,
        CreatedAt = Now,
    };

    [Fact]
    public async Task SelectCard_SetsSelectedCard()
    {
        var run = NewRun(_session.Id, "red fox");
        var generations = new[] { NewGeneration(run, 5) };
        StoreRun(run, generations);
        await _model.OnAppearingAsync();
        var card = Assert.IsType<RunItemViewModel>(_model.Items[^1]).Generations[0];
        Assert.Null(_model.SelectedCard);

        _model.SelectCardCommand.Execute(card);

        Assert.Same(card, _model.SelectedCard);
    }

    [Fact]
    public async Task OnAppearingAsync_SetsCurrentSession()
    {
        await _model.OnAppearingAsync();

        Assert.Equal(_session.Id, _tracker.CurrentId);
    }

    [Fact]
    public async Task OnAppearingAsync_LegacyRunMessageWithoutOwnText_ShowsPromptOnceInRun()
    {
        var run = NewRun(_session.Id, "red fox");
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.Assistant, Text = "Prompt: red fox", CreatedAt = Now, RunId = run.Id });
        StoreRun(run, [NewGeneration(run, 5)]);

        await _model.OnAppearingAsync();

        var runItem = Assert.IsType<RunItemViewModel>(Assert.Single(_model.Items));
        Assert.Equal(string.Format(AppResources.RunPromptFormat, "red fox"), runItem.PromptText);
    }

    [Fact]
    public async Task OnAppearingAsync_LegacyRunMessageWithText_ShowsTextWithoutPromptLine()
    {
        var run = NewRun(_session.Id, "red fox");
        StoreMessage(new ChatMessage { Id = Guid.NewGuid(), SessionId = _session.Id, Role = ChatRole.Assistant, Text = "Drawing.\n\nPrompt: red fox", CreatedAt = Now, RunId = run.Id });
        StoreRun(run, [NewGeneration(run, 5)]);

        await _model.OnAppearingAsync();

        Assert.Equal(2, _model.Items.Count);
        Assert.Equal("Drawing.", Assert.IsType<MessageItemViewModel>(_model.Items[0]).Text);
    }

    [Fact]
    public async Task ReloadAsync_UnknownSession_ShowsAlert()
    {
        var unknown = Guid.NewGuid();
        _sessionRepository.GetAsync(unknown, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Session?>(null));
        var model = CreateModel(unknown);

        await model.OnAppearingAsync();

        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, Arg.Any<string>());
    }

    [Fact]
    public void ApplyQueryAttributes_MissingSessionId_Throws()
    {
        var model = new ChatPageModel(
            _sessionService, _assistant, _generationService, _files, _navigation, _dialogs, _mainThread, _attachments, _filePicker, _descriptions, _clipboard, Substitute.For<IImageSaver>(), _tracker, NewModelsPane, NewParametersPane);

        Assert.Throws<ArgumentException>(() => model.ApplyQueryAttributes(new Dictionary<string, object>()));
    }

    [Fact]
    public void SendCommand_EmptyInput_CannotExecute()
    {
        _model.InputText = "";

        Assert.False(_model.SendCommand.CanExecute(null));
    }

    [Fact]
    public void SendCommand_WhitespaceInput_CannotExecute()
    {
        _model.InputText = "   ";

        Assert.False(_model.SendCommand.CanExecute(null));
    }

    [Fact]
    public void SendCommand_TextEntered_CanExecute()
    {
        _model.InputText = "draw a fox";

        Assert.True(_model.SendCommand.CanExecute(null));
    }

    private void PickNextImage(string fileName = "fox.png") =>
        _filePicker.PickImageAsync(Arg.Any<CancellationToken>()).Returns(new PickedImage([1, 2], "image/png", fileName));

    [Fact]
    public async Task AttachImageCommand_ConfirmedAndPicked_AddsPendingAttachment()
    {
        await _model.OnAppearingAsync();
        PickNextImage();

        await _model.AttachImageCommand.ExecuteAsync(null);

        Assert.Single(_model.PendingAttachments);
        Assert.Equal("fox.png", _model.PendingAttachments[0].FileName);
        Assert.NotEmpty(_model.AttachmentsText);
    }

    [Fact]
    public async Task AttachImageCommand_ConsentDeclined_PicksNothing()
    {
        await _model.OnAppearingAsync();
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await _model.AttachImageCommand.ExecuteAsync(null);

        Assert.Empty(_model.PendingAttachments);
        await _filePicker.DidNotReceive().PickImageAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AttachImageCommand_ConsentAskedOnlyOnce()
    {
        await _model.OnAppearingAsync();
        PickNextImage();

        await _model.AttachImageCommand.ExecuteAsync(null);
        await _model.AttachImageCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        Assert.Equal(2, _model.PendingAttachments.Count);
    }

    [Fact]
    public async Task AttachImageCommand_FifthImage_AlertsAndAddsNothing()
    {
        await _model.OnAppearingAsync();
        PickNextImage();
        for (var i = 0; i < 4; i++)
        {
            await _model.AttachImageCommand.ExecuteAsync(null);
        }

        await _model.AttachImageCommand.ExecuteAsync(null);

        Assert.Equal(4, _model.PendingAttachments.Count);
        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, Arg.Any<string>());
    }

    [Fact]
    public async Task RemoveAttachmentCommand_RemovesItFromPending()
    {
        await _model.OnAppearingAsync();
        PickNextImage();
        await _model.AttachImageCommand.ExecuteAsync(null);

        _model.RemoveAttachmentCommand.Execute(_model.PendingAttachments[0]);

        Assert.Empty(_model.PendingAttachments);
        Assert.Equal("", _model.AttachmentsText);
    }

    [Fact]
    public async Task SendCommand_WithPendingAttachment_LinksItAndClearsPending()
    {
        await _model.OnAppearingAsync();
        PickNextImage();
        await _model.AttachImageCommand.ExecuteAsync(null);
        var attachmentId = _model.PendingAttachments[0].Id;
        _model.InputText = "";

        await _model.SendCommand.ExecuteAsync(null);

        Assert.Empty(_model.PendingAttachments);
        await _chatAttachments.Received(1).LinkToMessageAsync(
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.Contains(attachmentId)),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendCommand_OnlyAttachmentAndNoText_IsEnabled()
    {
        await _model.OnAppearingAsync();
        PickNextImage();
        await _model.AttachImageCommand.ExecuteAsync(null);

        Assert.True(_model.SendCommand.CanExecute(null));
    }

    private static ImageDescription Description(string text) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        SourceGenerationId = Guid.NewGuid(),
        Mode = DescriptionMode.Description,
        ProviderId = ProviderIds.OpenRouter,
        ModelId = "vendor/vision",
        Text = text,
        CostUsd = 0.0004m,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public async Task ShowDescriptionAsync_EmptyInputAndInsertAccepted_PutsTextIntoInput()
    {
        await _model.OnAppearingAsync();
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), AppResources.DescriptionInsert, AppResources.DescriptionClose).Returns(true);

        await _model.ShowDescriptionAsync(Description("A red fox."));

        Assert.Equal("A red fox.", _model.InputText);
    }

    [Fact]
    public async Task ShowDescriptionAsync_DialogShowsTextAndPriceWithoutTouchingInput()
    {
        await _model.OnAppearingAsync();
        _model.InputText = "keep me";
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), AppResources.DescriptionInsert, AppResources.DescriptionClose).Returns(false);

        await _model.ShowDescriptionAsync(Description("A red fox."));

        Assert.Equal("keep me", _model.InputText);
        await _dialogs.Received(1).ConfirmAsync(
            AppResources.DescriptionTitle,
            Arg.Is<string>(m => m.StartsWith("A red fox.", StringComparison.Ordinal) && m.Contains("$0.0004", StringComparison.Ordinal)),
            AppResources.DescriptionInsert,
            AppResources.DescriptionClose);
    }

    [Fact]
    public async Task ShowDescriptionAsync_InputHasTextAndReplaceChosen_ReplacesIt()
    {
        await _model.OnAppearingAsync();
        _model.InputText = "old";
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), AppResources.DescriptionInsert, AppResources.DescriptionClose).Returns(true);
        _dialogs.ConfirmAsync(AppResources.DescriptionTitle, AppResources.DescriptionReplaceQuestion, AppResources.DescriptionReplace, AppResources.DescriptionAppend).Returns(true);

        await _model.ShowDescriptionAsync(Description("A red fox."));

        Assert.Equal("A red fox.", _model.InputText);
    }

    [Fact]
    public async Task ShowDescriptionAsync_InputHasTextAndAppendChosen_AddsLine()
    {
        await _model.OnAppearingAsync();
        _model.InputText = "old";
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), AppResources.DescriptionInsert, AppResources.DescriptionClose).Returns(true);
        _dialogs.ConfirmAsync(AppResources.DescriptionTitle, AppResources.DescriptionReplaceQuestion, AppResources.DescriptionReplace, AppResources.DescriptionAppend).Returns(false);

        await _model.ShowDescriptionAsync(Description("A red fox."));

        Assert.Equal("old\nA red fox.", _model.InputText);
    }

    [Fact]
    public async Task SendCommand_SendsMessageToAssistantClearsFieldAndShowsIt()
    {
        await _model.OnAppearingAsync();
        _model.InputText = "draw a fox";

        await _model.SendCommand.ExecuteAsync(null);

        Assert.Equal("", _model.InputText);
        Assert.False(_model.IsBusy);
        await _chat.Received(1).CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>());
        var user = Assert.IsType<MessageItemViewModel>(_model.Items.First());
        Assert.Equal("draw a fox", user.Text);
        Assert.True(user.IsUser);
    }

    [Fact]
    public async Task SendCommand_AssistantStartsRun_RunAppearsInTimeline()
    {
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>()).Returns(
            new AssistantChatResult(null, [new AssistantToolCall(AssistantPrompts.GenerateImagesToolName, "{\"prompt\":\"red fox\"}")], null));
        await _model.OnAppearingAsync();
        _model.InputText = "draw a red fox";

        await _model.SendCommand.ExecuteAsync(null);

        var run = Assert.IsType<RunItemViewModel>(_model.Items.Last());
        Assert.Equal(string.Format(AppResources.RunPromptFormat, "red fox"), run.PromptText);
        await _generationService.WhenIdleAsync();
    }

    [Fact]
    public async Task SendCommand_AssistantFailure_RestoresTextShowsAlertAndStopsBusy()
    {
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("no network"));
        await _model.OnAppearingAsync();
        _model.InputText = "draw a fox";

        await _model.SendCommand.ExecuteAsync(null);

        Assert.Equal("draw a fox", _model.InputText);
        Assert.False(_model.IsBusy);
        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, "no network");
    }

    [Fact]
    public async Task SendCommand_WhileSending_IsBusyShowsThinkingAndCannotSendAgain()
    {
        var reply = new TaskCompletionSource<AssistantChatResult>();
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>()).Returns(reply.Task);
        await _model.OnAppearingAsync();
        _model.InputText = "draw a fox";

        var sending = _model.SendCommand.ExecuteAsync(null);

        Assert.True(_model.IsBusy);
        Assert.False(_model.SendCommand.CanExecute(null));

        reply.SetResult(new AssistantChatResult("Here you go.", [], null));
        await sending;

        Assert.False(_model.IsBusy);
    }

    [Fact]
    public async Task GeneratingCard_UpdatesInPlaceWithoutReload()
    {
        // Two cards mean two generator calls; wait for both, not only the first.
        var allEntered = new TaskCompletionSource();
        var release = new TaskCompletionSource<ImageGenerationResult>();
        var entered = 0;
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                allEntered.TrySetResult();
            }

            return release.Task;
        });
        await _model.OnAppearingAsync();
        await _generationService.StartRunAsync(_session.Id, "red fox", TestContext.Current.CancellationToken);
        await allEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await _model.ReloadAsync();
        var cards = Cards.ToList();
        Assert.Equal(2, cards.Count);
        Assert.All(cards, card => Assert.Equal(AppResources.StatusRunning, card.StatusText));
        var item = _model.Items.Last();

        release.SetResult(new ImageGenerationResult(Image, "image/png", 0.014m));
        await _generationService.WhenIdleAsync();

        Assert.All(cards, card =>
        {
            Assert.Equal(AppResources.StatusSucceeded, card.StatusText);
            Assert.True(card.HasImage);
            Assert.Equal("/data/images/s/g.png", card.ImageFilePath);
        });
        Assert.Same(item, _model.Items.Last());
    }

    [Fact]
    public async Task GeneratingCard_Done_UpdatesRunHeader()
    {
        var allEntered = new TaskCompletionSource();
        var release = new TaskCompletionSource<ImageGenerationResult>();
        var entered = 0;
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                allEntered.TrySetResult();
            }

            return release.Task;
        });
        await _model.OnAppearingAsync();
        await _generationService.StartRunAsync(_session.Id, "red fox", TestContext.Current.CancellationToken);
        await allEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await _model.ReloadAsync();
        var item = Assert.IsType<RunItemViewModel>(_model.Items.Last());

        release.SetResult(new ImageGenerationResult(Image, "image/png", 0.014m));
        await _generationService.WhenIdleAsync();

        Assert.Same(item, _model.Items.Last());
        Assert.Contains("2 of 2", item.HeaderText);
        Assert.EndsWith("$0.014", item.HeaderText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnDisappearing_StopsUpdatingCards()
    {
        await _model.OnAppearingAsync();
        _model.OnDisappearing();

        await _generationService.StartRunAsync(_session.Id, "red fox", TestContext.Current.CancellationToken);
        await _generationService.WhenIdleAsync();

        _mainThread.DidNotReceive().BeginInvokeOnMainThread(Arg.Any<Action>());
    }

    [Fact]
    public async Task OnAppearingAsync_CalledTwice_SubscribesOnlyOnce()
    {
        await _model.OnAppearingAsync();
        await _model.OnAppearingAsync();

        await _generationService.StartRunAsync(_session.Id, "red fox", TestContext.Current.CancellationToken);
        await _generationService.WhenIdleAsync();

        // Two generations, each reports "Generating" and "Done": four events for one subscription.
        _mainThread.Received(4).BeginInvokeOnMainThread(Arg.Any<Action>());
    }

    [Fact]
    public async Task GenerationUpdated_OtherSession_IsIgnored()
    {
        await _model.OnAppearingAsync();

        await _generationService.StartRunAsync(_otherSession.Id, "other fox", TestContext.Current.CancellationToken);
        await _generationService.WhenIdleAsync();

        _mainThread.DidNotReceive().BeginInvokeOnMainThread(Arg.Any<Action>());
        Assert.Empty(_model.Items);
    }

    [Fact]
    public async Task RepeatCommand_StartsRunWithSamePromptModelAndSeedThenReloads()
    {
        var source = NewRun(_session.Id, "red fox");
        StoreRun(source, NewGeneration(source, 5));
        await _model.OnAppearingAsync();

        await Assert.Single(Cards).RepeatCommand.ExecuteAsync(null);

        var created = StoredRuns.Single(r => r.Origin == RunOrigin.Repeat);
        Assert.Equal("red fox", created.Prompt);
        Assert.Equal(2, _model.Items.OfType<RunItemViewModel>().Count());
        await _generationService.WhenIdleAsync();
        var generation = Assert.Single(RowsOf(created.Id));
        Assert.Equal(5L, generation.Seed);
        Assert.Equal(ImageModel, generation.ModelId);
        Assert.Equal("red fox", generation.Prompt);
    }

    [Fact]
    public async Task NewSeedCommand_StartsRunWithSamePromptAndNewSeed()
    {
        var source = NewRun(_session.Id, "red fox");
        StoreRun(source, NewGeneration(source, 5));
        _seeds.NextSeed().Returns(99L);
        await _model.OnAppearingAsync();

        await Assert.Single(Cards).NewSeedCommand.ExecuteAsync(null);

        var created = StoredRuns.Single(r => r.Origin == RunOrigin.NewSeed);
        Assert.Equal("red fox", created.Prompt);
        await _generationService.WhenIdleAsync();
        Assert.Equal(99L, Assert.Single(RowsOf(created.Id)).Seed);
    }

    [Fact]
    public async Task EditCommand_NewPrompt_StartsEditRunWithSameModelAndSeed()
    {
        var source = NewRun(_session.Id, "red fox");
        StoreRun(source, NewGeneration(source, 5));
        _dialogs.PromptAsync(AppResources.EditPromptTitle, AppResources.EditPromptMessage, AppResources.Generate, AppResources.Cancel, "red fox")
            .Returns("  red fox at night  ");
        await _model.OnAppearingAsync();

        await Assert.Single(Cards).EditCommand.ExecuteAsync(null);

        var created = StoredRuns.Single(r => r.Origin == RunOrigin.Edit);
        Assert.Equal("red fox at night", created.Prompt);
        await _generationService.WhenIdleAsync();
        var generation = Assert.Single(RowsOf(created.Id));
        Assert.Equal(5L, generation.Seed);
        Assert.Equal(ImageModel, generation.ModelId);
    }

    [Fact]
    public async Task EditCommand_Cancelled_StartsNothing()
    {
        var source = NewRun(_session.Id, "red fox");
        StoreRun(source, NewGeneration(source, 5));
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns((string?)null);
        await _model.OnAppearingAsync();

        await Assert.Single(Cards).EditCommand.ExecuteAsync(null);

        Assert.Single(StoredRuns);
    }

    [Fact]
    public async Task EditCommand_BlankText_StartsNothing()
    {
        var source = NewRun(_session.Id, "red fox");
        StoreRun(source, NewGeneration(source, 5));
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns("   ");
        await _model.OnAppearingAsync();

        await Assert.Single(Cards).EditCommand.ExecuteAsync(null);

        Assert.Single(StoredRuns);
    }

    [Fact]
    public async Task EditCommand_ShowsEditPromptWithExactGenerationPrompt()
    {
        var source = NewRun(_session.Id, "red fox");
        StoreRun(source, NewGeneration(source, 5));
        await _model.OnAppearingAsync();

        await Assert.Single(Cards).EditCommand.ExecuteAsync(null);

        await _dialogs.Received(1).PromptAsync(
            AppResources.EditPromptTitle, AppResources.EditPromptMessage, AppResources.Generate, AppResources.Cancel, "red fox");
    }

    [Fact]
    public async Task RepeatCommand_ServiceFails_ShowsAlertAndReloads()
    {
        var source = NewRun(_session.Id, "red fox");
        StoreRun(source, NewGeneration(source, 5));
        _runRepository.AddRunAsync(Arg.Any<Run>(), Arg.Any<IReadOnlyList<Generation>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("disk full"));
        await _model.OnAppearingAsync();

        await Assert.Single(Cards).RepeatCommand.ExecuteAsync(null);

        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, "disk full");
        Assert.Single(_model.Items.OfType<RunItemViewModel>());
    }

    [Fact]
    public async Task OpenParameters_Narrow_Navigates()
    {
        _model.IsWide = false;

        await _model.OpenParametersCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(Routes.SessionParametersFor(_session.Id));
        Assert.Null(_model.ParametersPane);
    }

    [Fact]
    public async Task OpenModels_Narrow_Navigates()
    {
        _model.IsWide = false;

        await _model.OpenModelsCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(Routes.SessionModelsFor(_session.Id));
        Assert.Null(_model.ModelsPane);
    }

    [Fact]
    public async Task OpenModels_Wide_OpensPane()
    {
        _model.IsWide = true;

        await _model.OpenModelsCommand.ExecuteAsync(null);

        Assert.NotNull(_model.ModelsPane);
        Assert.True(_model.ModelsPane!.HostedInPane);
        Assert.Null(_model.ParametersPane);
        await _navigation.DidNotReceive().GoToAsync(Routes.SessionModelsFor(_session.Id));
    }

    [Fact]
    public async Task OpenModelsTwice_ClosesPane()
    {
        _model.IsWide = true;

        await _model.OpenModelsCommand.ExecuteAsync(null);
        await _model.OpenModelsCommand.ExecuteAsync(null);

        Assert.Null(_model.ModelsPane);
    }

    [Fact]
    public async Task OpenParameters_WhenModelsOpen_ReplacesPane()
    {
        _model.IsWide = true;
        await _model.OpenModelsCommand.ExecuteAsync(null);

        await _model.OpenParametersCommand.ExecuteAsync(null);

        Assert.Null(_model.ModelsPane);
        Assert.NotNull(_model.ParametersPane);
    }

    [Fact]
    public async Task SaveInPane_ClosesPaneAndAppliesToNextRun()
    {
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ChatModel>>(
            [new ChatModel(AssistantModel, "Assistant", SupportsImageInput: true, ContextLength: 1000, SupportsTools: true)]));
        _model.IsWide = true;
        await _model.OnAppearingAsync();
        await _model.OpenModelsCommand.ExecuteAsync(null);
        var pane = _model.ModelsPane!;
        Assert.False(pane.IsBusy);
        pane.RunsPerModel = 5;

        await pane.SaveCommand.ExecuteAsync(null);

        Assert.Null(_model.ModelsPane);
        await _sessionRepository.Received().UpdateAsync(
            Arg.Is<Session>(s => s.Id == _session.Id && s.Settings.RunsPerModel == 5),
            Arg.Any<CancellationToken>());
    }
}
