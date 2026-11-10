using Allure.NUnit;
using NUnit.Framework;

namespace ImageGen.E2E.Windows;

[TestFixture]
[AllureNUnit]
[Platform("Win")]
public sealed class VideoRecorderTests
{
    [Test]
    public void CaptureFinalFrame_SavesThenWaitsThenStopsRecording()
    {
        var steps = new List<string>();

        VideoRecorder.CaptureFinalFrame(
            () => steps.Add("screenshot"),
            () => steps.Add("hold"),
            () => steps.Add("stop"));

        Assert.That(steps, Is.EqualTo(new[] { "screenshot", "hold", "stop" }));
    }

    [Test]
    public void BuildArguments_UsesRequiredDesktopRecordingOptions()
    {
        var arguments = VideoRecorder.BuildArguments("tmp/capture.mp4");

        Assert.That(arguments, Is.EqualTo(new[]
        {
            "-y", "-f", "gdigrab", "-framerate", "15", "-i", "desktop",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
            "-movflags", "+frag_keyframe+empty_moov", "tmp/capture.mp4",
        }));
    }
}
