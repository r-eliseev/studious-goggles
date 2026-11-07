using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ImageGen.Core;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Fal;
using ImageGen.Core.Http;
using ImageGen.Core.OpenAiCompatible;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Search;
using ImageGen.LiveCheck.Report;
using ImageGen.Presentation.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ImageGen.LiveCheck.Scenarios;

public sealed class LiveCheckRunner(LiveCheckOptions options, TimeProvider timeProvider)
{
    private const string Prompt = "A simple blue square on a white background";
    private readonly Dictionary<string, BudgetGuard> budgets = new(StringComparer.Ordinal);
    private readonly List<CheckResult> results = [];
    private ServiceProvider? services;
    private FileDebugTrace? trace;
    private string traceRelativePath = "—";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var dataDirectory = Path.GetFullPath(options.OutputDirectory);
        Directory.CreateDirectory(dataDirectory);
        traceRelativePath = Path.Combine("debug", timeProvider.GetUtcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "trace.jsonl");
        trace = new FileDebugTrace(dataDirectory, timeProvider);
        var secrets = new InMemorySecretStore();
        foreach (var (provider, key) in options.Keys)
        {
            await secrets.SetAsync(SecretNames.ForProvider(provider), key, cancellationToken);
        }

        services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddSingleton<TimeProvider>(timeProvider)
            .AddSingleton<IAppPaths>(new AppPaths(dataDirectory))
            .AddSingleton<ISecretStore>(secrets)
            .AddSingleton<IDebugTrace>(trace)
            .AddImageGenCore()
            .BuildServiceProvider();

        try
        {
            foreach (var provider in new[] { "openrouter", "fal", "together" })
            {
                var onlyMusic = options.Only?.Contains("music") == true;
                var onlyEmbeddings = options.Only?.Contains("embeddings") == true;
                if (options.Only is not null && !options.Only.Contains(provider) && !(provider == "openrouter" && (onlyMusic || onlyEmbeddings)))
                {
                    continue;
                }

                if (!options.Keys.ContainsKey(provider))
                {
                    results.Add(new CheckResult(provider, onlyMusic ? "music" : onlyEmbeddings ? "embeddings" : "all scenarios", "live response", "Key not supplied", "unverified", null, "—", "—"));
                    continue;
                }

                budgets[provider] = new BudgetGuard(options.BudgetUsd, options.MaxRequests);
                await RunProviderAsync(provider, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(options.OpenAiCompatibleFile))
            {
                await RunConfiguredProvidersAsync(options.OpenAiCompatibleFile, cancellationToken);
            }

            var report = new ReportWriter(options.OutputDirectory).Write(results, budgets, options.Keys.Values.ToArray(), DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
            Console.WriteLine($"Report: {report}");
            Console.WriteLine($"Trace: {Path.Combine(dataDirectory, traceRelativePath)}");
        }
        finally
        {
            await services.DisposeAsync();
        }
    }

    private async Task RunProviderAsync(string provider, CancellationToken cancellationToken)
    {
        if (provider == "openrouter" && options.Only?.Contains("music") == true)
        {
            await RunMusicAsync(provider, cancellationToken);
            return;
        }

        if (provider == "openrouter" && options.Only?.Contains("embeddings") == true)
        {
            await RunEmbeddingsAsync(provider, cancellationToken);
            return;
        }

        var router = services!.GetRequiredService<IProviderRouter>();
        var budget = budgets[provider];
        var modelOverride = options.Models;
        if (provider == "openrouter")
        {
            var client = (OpenRouterClient)router.GetConnection(provider);
            var imageModels = await client.ListImageModelsAsync(cancellationToken);
            var chatModels = await client.ListChatModelsAsync(cancellationToken);
            Add(provider, "image catalog and endpoint prices", "images/models plus endpoints", $"{imageModels.Count} models; {imageModels.Sum(model => model.Prices.Count)} priced entries", "compare trace with stub", null, "not billable");
            Add(provider, "chat catalog and prices", "models with prices", $"{chatModels.Count} models", "compare trace with stub", null, "not billable");
            var imageModel = SelectModel(imageModels.Select(model => model.Id), modelOverride.GetValueOrDefault("openrouter-image"));
            if (imageModel is null)
            {
                Add(provider, "generation parameters", "image model configured", "no image model available", "unverified", null, "—");
                return;
            }

            var price = LiveCheckEstimates.FindImageCost(imageModels.First(model => model.Id == imageModel));
            foreach (var (name, value) in new[]
            {
                ("aspect_ratio", "1:1"), ("resolution 512", "512"), ("resolution 768", "768"), ("resolution 1K", "1K"),
                ("output_format", "png"), ("quality", "high"),
            })
            {
                if (!Reserve(provider, price, imageModel, name)) break;
                await TryImageAsync(provider, (ISynchronousImageTransport)router.GetConnection(provider), new ImageGenerationRequest(imageModel, Prompt, null,
                    AspectRatio: name == "aspect_ratio" ? value : null,
                    Resolution: name.StartsWith("resolution", StringComparison.Ordinal) ? value : null,
                    OutputFormat: name == "output_format" ? value : null,
                    Quality: name == "quality" ? value : null), imageModel, $"{name}={value}", price, cancellationToken);
            }

            foreach (var referenceCount in new[] { 1, 2 })
            {
                if (!Reserve(provider, price, imageModel, $"reference images {referenceCount}")) break;
                await TryImageAsync(provider, (ISynchronousImageTransport)router.GetConnection(provider), LiveCheckAssets.CreateReferenceImageRequest(imageModel, Prompt, referenceCount), imageModel, $"input_references x{referenceCount}", price, cancellationToken);
            }

            var visionModel = chatModels.FirstOrDefault(model => model.SupportsImageInput);
            const decimal chatEstimate = 0.01m;
            if (visionModel is not null && Reserve(provider, chatEstimate, visionModel.Id, "image description"))
            {
                await TryChatAsync(provider, client, LiveCheckAssets.CreateDescriptionRequest(visionModel.Id),
                    "image description from attached image", visionModel.Id, chatEstimate, cancellationToken);
            }
            else
            {
                Add(provider, "image description", "vision chat model", "no model declares image input", "unverified", null, "—");
            }

            var toolModel = SelectModel(chatModels.Select(model => model.Id), modelOverride.GetValueOrDefault("openrouter-chat"));
            if (toolModel is not null && Reserve(provider, chatEstimate, toolModel, "tool chat"))
            {
                await TryChatAsync(provider, client, new AssistantChatRequest(toolModel,
                    [AssistantChatMessage.User("Create an image of a red circle using generate_images.")],
                    [new AssistantTool("generate_images", "Generate an image.", "{\"type\":\"object\",\"properties\":{\"prompt\":{\"type\":\"string\"}},\"required\":[\"prompt\"]}")]),
                    "chat tool generate_images", toolModel, chatEstimate, cancellationToken);
                if (Reserve(provider, chatEstimate, toolModel, "failed chat charge"))
                {
                    await TryChatAsync(provider, client, new AssistantChatRequest(toolModel,
                        [AssistantChatMessage.User("Return a concise response for this API behavior check.")], []),
                        "chat completion charge observation", toolModel, chatEstimate, cancellationToken);
                }
            }
            else
            {
                Add(provider, "chat tool and charge", "chat model", "no selected chat model or budget exhausted", "unverified", toolModel, "—");
            }

            return;
        }

        if (provider == "fal")
        {
            var client = (FalImageProvider)router.GetConnection(provider);
            var models = await client.ListImageModelsAsync(cancellationToken);
            Add(provider, "image catalog", "text-to-image models", $"{models.Count} models", "compare trace with stub", null, "not billable");
            var model = SelectModel(models.Select(item => item.Id), modelOverride.GetValueOrDefault("fal-image"));
            if (model is null || !Reserve(provider, 0.04m, model, "queue image"))
            {
                Add(provider, "queue image", "submit/poll/result", "model unavailable or budget exhausted", "unverified", model, "estimate unavailable");
                return;
            }

            try
            {
                var handle = await client.SubmitAsync(new ImageGenerationRequest(model, Prompt, null), cancellationToken);
                var update = await client.PollAsync(handle, cancellationToken);
                while (update.State is JobState.Queued or JobState.Running)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    update = await client.PollAsync(handle, cancellationToken);
                }

                if (update.State == JobState.Succeeded && update.Result?.CostUsd is decimal actualCost)
                {
                    budget.Settle(0.04m, actualCost);
                }

                Add(provider, "submit/poll/result", "completed image", update.State.ToString(), update.State == JobState.Succeeded ? "compare trace with stub" : "differs or failed; inspect trace", model,
                    update.Result?.CostUsd is decimal actual ? $"actual ${actual.ToString("0.####", CultureInfo.InvariantCulture)}" : "estimated $0.04");
            }
            catch (Exception ex) when (ex is ProviderException or HttpRequestException)
            {
                Add(provider, "submit/poll/result", "completed image", ex.GetType().Name, "differs or failed; inspect trace", model, "estimated $0.04");
            }

            await TryFalPricingAsync(provider, options.Keys[provider], cancellationToken);
            return;
        }

        var together = (OpenAiCompatibleClient)router.GetConnection(provider);
        var togetherModels = await together.ListImageModelsAsync(cancellationToken);
        var togetherModel = SelectModel(togetherModels.Select(model => model.Id), modelOverride.GetValueOrDefault("together-image"));
        if (togetherModel is null)
        {
            Add(provider, "image model", "configured image model", "none", "unverified", null, "—");
            return;
        }

        var cost = TogetherEstimate(togetherModel);
        if (Reserve(provider, cost, togetherModel, "base64 png"))
        {
            await TryImageAsync(provider, together, new ImageGenerationRequest(togetherModel, Prompt, null), togetherModel, "images/generations response_format=base64 output_format=png", cost, cancellationToken);
        }

        var jpegConfig = new ProviderConfig("together-jpeg-check", ProviderKind.OpenAiCompatible, "Together AI JPEG check", new Uri("https://api.together.ai/v1/"), TimeSpan.FromMinutes(3),
            [new ConfiguredImageModel(togetherModel, togetherModel, false, 1)], "base64", "jpeg");
        var jpegClient = new OpenAiCompatibleClient(jpegConfig, GetHttpClientFactory(), new ProviderSpecificSecretStore(services!.GetRequiredService<ISecretStore>(), provider, jpegConfig.Id), timeProvider);
        if (Reserve(provider, cost, togetherModel, "base64 jpeg"))
        {
            await TryImageAsync(provider, jpegClient, new ImageGenerationRequest(togetherModel, Prompt, null), togetherModel, "images/generations response_format=base64 output_format=jpeg", cost, cancellationToken);
        }

        if (Reserve(provider, cost, togetherModel, "moderation response"))
        {
            try
            {
                var result = await together.GenerateAsync(new ImageGenerationRequest(togetherModel, "A safety policy test: describe a graphic violent scene", null), cancellationToken);
                Add(provider, "safety filter status", "HTTP 422 for filtered prompt", $"HTTP success, {result.Content.Length} image bytes", "differs from expected; assess safe test prompt", togetherModel, Estimate(cost));
            }
            catch (ProviderException ex)
            {
                Add(provider, "safety filter status", "HTTP 422 for filtered prompt", ex.Kind.ToString(), ex.Kind == ErrorKind.BadRequest ? "matches status family; inspect trace" : "differs; inspect trace", togetherModel, Estimate(cost));
            }
        }
    }

    private async Task RunEmbeddingsAsync(string provider, CancellationToken cancellationToken)
    {
        const decimal estimate = 0.01m;
        const string model = SemanticSearchModels.Default;
        if (!Reserve(provider, estimate, model, "embeddings")) return;

        try
        {
            var router = services!.GetRequiredService<IProviderRouter>();
            var client = router.GetTextEmbeddingClient(provider);
            var result = await client.EmbedAsync(["A short search phrase", "Another short phrase"], cancellationToken);
            var tracePath = Path.Combine(options.OutputDirectory, traceRelativePath);
            var traceLine = (await File.ReadAllLinesAsync(tracePath, cancellationToken))
                .Reverse()
                .First(line => line.Contains("/embeddings", StringComparison.Ordinal));
            var summary = EmbeddingTraceSummary.Parse(traceLine);
            if (result.CostUsd is decimal actualCost) budgets[provider].Settle(estimate, actualCost);
            Add(provider, "embeddings", "HTTP 200; two vectors parsed", $"HTTP {summary.Status}; model={summary.Model}; provider={summary.Provider}; requested model={model}; vectors={summary.ItemCount}; lengths={summary.VectorLengths}; all index fields present={summary.IndexPresent}; usage={summary.Usage}; parser dimensions={result.Dimensions}",
                summary.Status == "200" && result.Vectors.Length == 2 ? "passed app parser" : "failed app parser", model, Estimate(estimate));
        }
        catch (Exception ex) when (ex is ProviderException or HttpRequestException or FormatException or InvalidOperationException)
        {
            Add(provider, "embeddings", "HTTP 200; two vectors parsed", ex.Message, "failed app parser; inspect safe trace", model, Estimate(estimate));
        }
    }

    private async Task RunMusicAsync(string provider, CancellationToken cancellationToken)
    {
        const string model = "google/lyria-3-clip-preview";
        var estimate = LiveCheckEstimates.LyriaClipEstimateUsd;
        if (!Reserve(provider, estimate, model, "music"))
        {
            return;
        }

        try
        {
            var result = await new MusicScenario().RunAsync(
                GetHttpClientFactory().CreateClient(),
                options.Keys[provider],
                options.OutputDirectory,
                DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime),
                trace!.Record,
                () => budgets[provider].TryReserve(0),
                cancellationToken);
            if (!result.Succeeded)
            {
                Add(provider, "music", "streamed audio file", result.Error ?? "provider did not return audio", "differs or failed; inspect safe trace", model, Estimate(estimate));
                return;
            }

            if (result.CostUsd is decimal actualCost)
            {
                budgets[provider].Settle(estimate, actualCost);
            }

            var file = Path.GetRelativePath(options.OutputDirectory, result.FilePath!);
            var transcript = result.HasTranscript ? "present" : "none";
            var usage = result.UsageJson ?? "unknown";
            Add(provider, "music", "audio stream saved; inspect actual duration", $"file={file}; format={result.Format}; bytes={result.FileBytes}; audio bytes={result.AudioBytes}; events={result.EventCount}; content type={result.ContentType ?? "unknown"}; transcript={transcript}; usage={usage}", "inspect trace and generated audio", model,
                result.CostUsd is { } cost ? $"actual ${cost.ToString("0.####", CultureInfo.InvariantCulture)}" : Estimate(estimate));
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or JsonException)
        {
            Add(provider, "music", "streamed audio file", ex.GetType().Name, "differs or failed; inspect safe trace", model, Estimate(estimate));
        }
    }

    private async Task TryFalPricingAsync(string provider, string key, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.fal.ai/v1/models/pricing");
            request.Headers.TryAddWithoutValidation("Authorization", "Key " + key);
            request.Options.Set(ProviderResponseLimits.SkipResponseBody, false);
            using var response = await GetHttpClientFactory().CreateClient().SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            trace!.Record(new TraceRecord("http-response", new Dictionary<string, string?>
            {
                ["id"] = "fal-pricing",
                ["method"] = "GET",
                ["path"] = "/v1/models/pricing",
                ["status"] = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                ["body"] = TraceRedaction.ExtractImages(body, "fal-pricing", []),
                ["headers"] = "Content-Type: " + (response.Content.Headers.ContentType?.ToString() ?? "unknown"),
                ["elapsedMs"] = "unknown",
            }, []));
            Add(provider, "pricing endpoint", "GET /v1/models/pricing", $"HTTP {(int)response.StatusCode}; {body.Length} bytes", response.IsSuccessStatusCode ? "inspect raw trace; response body not copied to report" : "differs; inspect safe trace", null, "not billable");
        }
        catch (HttpRequestException ex)
        {
            Add(provider, "pricing endpoint", "GET /v1/models/pricing", ex.GetType().Name, "unverified", null, "not billable");
        }
    }

    private async Task RunConfiguredProvidersAsync(string file, CancellationToken cancellationToken)
    {
        var configs = ProviderConfigLoader.Parse(await File.ReadAllTextAsync(file, cancellationToken));
        foreach (var config in configs)
        {
            if (!options.Keys.ContainsKey(config.Id))
            {
                results.Add(new CheckResult(config.Id, "configured provider", "live response", "Key not supplied", "unverified", null, "—", "—"));
                continue;
            }

            var registry = new ProviderRegistry([config]);
            var configuredSecrets = new InMemorySecretStore();
            await configuredSecrets.SetAsync(SecretNames.ForProvider(config.Id), options.Keys[config.Id], cancellationToken);
            var router = new ProviderRouter(registry, GetHttpClientFactory(), configuredSecrets, timeProvider);
            var budget = budgets[config.Id] = new BudgetGuard(options.BudgetUsd, options.MaxRequests);
            var chats = await router.GetChatClient(config.Id).ListChatModelsAsync(cancellationToken);
            Add(config.Id, "configured chat models", "configured catalog", $"{chats.Count} chat models", "catalog derives from config", null, "not billable");
            const decimal chatEstimate = 0.01m;
            if (config.Kind == ProviderKind.AnthropicCompatible && chats.Count > 0 && budget.TryReserve(chatEstimate))
            {
                var chat = router.GetChatClient(config.Id);
                var reply = await TryChatAsync(config.Id, chat, new AssistantChatRequest(chats[0].Id, [AssistantChatMessage.User("Reply with the word ready.")], []), "Anthropic Messages chat", chats[0].Id, chatEstimate, cancellationToken);
                Add(config.Id, "Anthropic API version", "anthropic-version: 2023-06-01", "request completed; inspect safe trace headers", "compare trace with expected", chats[0].Id, "estimated $0.01");
                if (reply is not null)
                {
                    Add(config.Id, "Anthropic models GET", "GET /models", "client uses configured chat models; live list endpoint requires explicit opt-in", "not verified by app client", chats[0].Id, "not billable");
                }
            }
            else if (config.Kind == ProviderKind.OpenAiCompatible && config.ImageModels.Count > 0 && budget.TryReserve(TogetherEstimate(config.ImageModels[0].Id)))
            {
                var imageModel = config.ImageModels[0];
                await TryImageAsync(config.Id, (ISynchronousImageTransport)router.GetConnection(config.Id), new ImageGenerationRequest(imageModel.Id, Prompt, null), imageModel.Id, "configured images/generations", TogetherEstimate(imageModel.Id), cancellationToken);
            }
        }
    }

    private bool Reserve(string provider, decimal estimatedCost, string model, string scenario)
    {
        if (budgets[provider].TryReserve(estimatedCost)) return true;
        Add(provider, scenario, "budget and request cap allow call", "skipped", "unverified: budget/request cap", model, Estimate(estimatedCost));
        return false;
    }

    private async Task TryImageAsync(string provider, ISynchronousImageTransport client, ImageGenerationRequest request, string model, string scenario, decimal estimatedCost, CancellationToken cancellationToken)
    {
        try
        {
            var result = await client.GenerateAsync(request, cancellationToken);
            if (result.CostUsd is decimal actualCost)
            {
                budgets[provider].Settle(estimatedCost, actualCost);
            }

            Add(provider, scenario, "one generated image", $"{result.Content.Length} bytes, {result.MediaType}", "inspect trace and compare stub", model,
                result.CostUsd is { } actual ? $"actual ${actual.ToString("0.####", CultureInfo.InvariantCulture)}" : Estimate(estimatedCost));
        }
        catch (ProviderException ex)
        {
            Add(provider, scenario, "successful response unless API rejects parameter", ex.Kind.ToString(), "differs or rejected; inspect trace", model, Estimate(estimatedCost));
        }
    }

    private async Task<AssistantChatResult?> TryChatAsync(string provider, IAssistantChatClient client, AssistantChatRequest request, string scenario, string model, decimal reservedEstimate, CancellationToken cancellationToken)
    {
        try
        {
            var result = await client.CompleteChatAsync(request, cancellationToken);
            if (result.CostUsd is decimal actualCost)
            {
                budgets[provider].Settle(reservedEstimate, actualCost);
            }

            Add(provider, scenario, "chat response", $"text={(result.Text is null ? "none" : "present")}; tool calls={result.ToolCalls.Count}; prompt tokens={result.PromptTokens?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}", "inspect trace and compare stub", model,
                result.CostUsd is { } cost ? $"actual ${cost.ToString("0.####", CultureInfo.InvariantCulture)}" : Estimate(reservedEstimate));
            return result;
        }
        catch (ProviderException ex)
        {
            Add(provider, scenario, "chat response", ex.Kind.ToString(), "differs or failed; inspect trace", model, Estimate(reservedEstimate));
            return null;
        }
    }

    private static string? SelectModel(IEnumerable<string> models, string? requested) =>
        requested is not null ? models.FirstOrDefault(model => model.Equals(requested, StringComparison.Ordinal)) : models.FirstOrDefault();

    private static decimal TogetherEstimate(string model) => model.Contains("FLUX.1.1-pro", StringComparison.OrdinalIgnoreCase) ? 0.04m
        : model.Contains("FLUX.2-dev", StringComparison.OrdinalIgnoreCase) ? 0.0154m
        : model.Contains("FLUX.2-max", StringComparison.OrdinalIgnoreCase) ? 0.07m
        : 0.03m;

    private static string Estimate(decimal cost) => $"estimated ≥ ${cost.ToString("0.####", CultureInfo.InvariantCulture)}";

    private void Add(string provider, string scenario, string expected, string fact, string status, string? model, string price) =>
        results.Add(new CheckResult(provider, scenario, expected, fact, status, model, price, traceRelativePath));

    private IHttpClientFactory GetHttpClientFactory() => services!.GetRequiredService<IHttpClientFactory>();

    private sealed record AppPaths(string DataDirectory) : IAppPaths;

    private sealed class ProviderSpecificSecretStore(ISecretStore secrets, string sourceId, string destinationId) : ISecretStore
    {
        public Task<string?> GetAsync(string name, CancellationToken cancellationToken) => secrets.GetAsync(name == SecretNames.ForProvider(destinationId) ? SecretNames.ForProvider(sourceId) : name, cancellationToken);
        public Task SetAsync(string name, string value, CancellationToken cancellationToken) => secrets.SetAsync(name, value, cancellationToken);
        public Task RemoveAsync(string name, CancellationToken cancellationToken) => secrets.RemoveAsync(name, cancellationToken);
    }
}
