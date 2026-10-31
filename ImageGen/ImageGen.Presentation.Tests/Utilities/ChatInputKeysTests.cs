using ImageGen.Presentation.Utilities;

namespace ImageGen.Presentation.Tests.Utilities;

public class ChatInputKeysTests
{
    [Fact]
    public void Decide_EnterDownWithoutShift_Sends()
    {
        Assert.Equal(ChatKeyAction.Send, ChatInputKeys.Decide(isEnter: true, isKeyDown: true, isShiftPressed: false));
    }

    [Fact]
    public void Decide_EnterUpWithoutShift_SwallowsSoTheFieldDoesNotSendAgain()
    {
        Assert.Equal(ChatKeyAction.Swallow, ChatInputKeys.Decide(isEnter: true, isKeyDown: false, isShiftPressed: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Decide_ShiftEnter_PassesToField(bool isKeyDown)
    {
        Assert.Equal(ChatKeyAction.PassToField, ChatInputKeys.Decide(isEnter: true, isKeyDown, isShiftPressed: true));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Decide_OtherKey_PassesToField(bool isKeyDown, bool isShiftPressed)
    {
        // Backspace (KEYCODE_DEL) and every other key must reach the field, otherwise the field does not delete.
        Assert.Equal(ChatKeyAction.PassToField, ChatInputKeys.Decide(isEnter: false, isKeyDown, isShiftPressed));
    }
}
