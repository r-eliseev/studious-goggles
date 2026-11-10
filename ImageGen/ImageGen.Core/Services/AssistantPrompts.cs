using ImageGen.Core.Context;
namespace ImageGen.Core.Services;

/// <summary>Texts and constants sent to the assistant model.</summary>
public static class AssistantPrompts
{
    /// <summary>Number of the latest session messages sent to the assistant.</summary>
    public const int HistoryMessageCount = 20;

    /// <summary>Most images of the last run that go to the assistant (S2.4).</summary>
    public const int MaxRunImages = 4;

    public const string GenerateImagesToolName = "generate_images";

    public const string GenerateVideoToolName = "generate_video";

    public const string GenerateAudioToolName = "generate_audio";

    public const string GenerateImagesToolDescription = "Generate images from a complete, self-contained prompt in English.";

    /// <summary>JSON Schema of the <c>generate_images</c> arguments.</summary>
    public const string GenerateImagesToolSchema = """
        {"type":"object","properties":{"prompt":{"type":"string","description":"Complete image prompt in English."}},"required":["prompt"]}
        """;

    public const string GenerateVideoToolDescription = "Generate a video from a complete, self-contained prompt in English; optionally use a successful image from this session as the first frame.";

    /// <summary>JSON Schema of the <c>generate_video</c> arguments.</summary>
    public const string GenerateVideoToolSchema = """
        {"type":"object","properties":{"prompt":{"type":"string","description":"Complete video prompt in English."},"first_frame_image_id":{"type":"string","description":"Optional id of a successful image generation from this session."}},"required":["prompt"]}
        """;

    public const string GenerateAudioToolDescription = "Generate music, songs, melodies, sounds, ambience, or spoken audio when the user asks for it.";

    /// <summary>JSON Schema of the <c>generate_audio</c> arguments.</summary>
    public const string GenerateAudioToolSchema = """
        {"type":"object","properties":{"prompt":{"type":"string","description":"Description of the requested sound or music."},"speech_text":{"type":"string","description":"Exact text to speak for speech models; when omitted, use prompt."}},"required":["prompt"]}
        """;

    public const string AudioSystemInstruction =
        "When the user explicitly asks for music, a song, melody, sound effect, ambience, or spoken audio, call generate_audio. "
        + "Use speech_text only for exact text to be spoken.";

    public const string VideoSystemInstruction =
        "When the user explicitly asks for a video, clip, or animation, call generate_video. "
        + "For image requests, continue to call generate_images.";

    public const string System = """
        You are an assistant inside an image generation app. The user describes the image they want.
        Your job is to turn the conversation into one complete, detailed image prompt in English and call the generate_images tool with it.
        Rules:
        - If the request is clear enough, call generate_images right away. Do not ask for confirmation.
        - If something essential is missing or ambiguous, ask one short clarifying question instead of calling the tool.
        - When the user asks for changes, start from the current prompt, apply only the requested changes, and call generate_images with the full updated prompt.
        - The prompt must be self-contained: never refer to previous images or messages inside the prompt.
        - If the user only wants to talk or asks a question, answer briefly without calling the tool.
        - Reply in the user's language. The prompt is always in English.
        """;

    public const string NoCurrentPrompt = "Current prompt: (none yet)";

    public static string CurrentPrompt(string prompt) => "Current prompt: " + prompt;

    /// <summary>C3.3: what the summary must keep, in the assistant's own words. The summary goes in as a system message.</summary>
    public const string SummaryInstruction =
        "Summarize the conversation so far for an assistant that writes image prompts. Keep the user's goals, the chosen style, "
        + "the rejected ideas and the latest prompt. At most 300 words. Write in the language of the conversation.";

    public const string SummaryPrefix = "Summary of the earlier conversation: ";

    public static string SummaryLine(string summary) => SummaryPrefix + summary;

    /// <summary>
    /// Estimated tokens of the fixed part of every request: the system prompt, the current prompt line and the tool
    /// description and schema (C1.1).
    /// </summary>
    public static int FixedTokens(string currentLine, bool includeVideoTool = false, bool includeAudioTool = false) =>
        TokenEstimator.Estimate(System) + TokenEstimator.Estimate(currentLine)
        + TokenEstimator.Estimate(GenerateImagesToolDescription) + TokenEstimator.Estimate(GenerateImagesToolSchema)
        + (includeVideoTool
            ? TokenEstimator.Estimate(VideoSystemInstruction) + TokenEstimator.Estimate(GenerateVideoToolDescription) + TokenEstimator.Estimate(GenerateVideoToolSchema)
            : 0)
        + (includeAudioTool
            ? TokenEstimator.Estimate(AudioSystemInstruction) + TokenEstimator.Estimate(GenerateAudioToolDescription) + TokenEstimator.Estimate(GenerateAudioToolSchema)
            : 0);

    /// <summary>
    /// Text of an assistant message that started a run: only what the assistant said, possibly empty.
    /// The prompt is not repeated here: the run block shows it (S2.3).
    /// </summary>
    public static string RunMessage(string? assistantText) => assistantText?.Trim() ?? string.Empty;
}
