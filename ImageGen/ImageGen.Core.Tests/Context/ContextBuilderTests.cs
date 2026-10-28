using ImageGen.Core.Context;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Context;

public class ContextBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private static ChatMessage Msg(string text, ChatRole role = ChatRole.User, bool isError = false, int index = 0) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Role = role,
        Text = text,
        CreatedAt = Now.AddSeconds(index),
        IsError = isError,
    };

    [Fact]
    public void Select_AllFit_ReturnsAllInOrder()
    {
        var messages = new[] { Msg("one", index: 0), Msg("two", ChatRole.Assistant, index: 1), Msg("three", index: 2) };

        var selected = ContextBuilder.Select(messages, historyBudget: 10_000, factor: 1.0);

        Assert.Equal(["one", "two", "three"], selected.Select(m => m.Text));
        Assert.All(selected, m => Assert.False(m.Shortened));
    }

    [Fact]
    public void Select_SkipsErrors()
    {
        var messages = new[] { Msg("ok", index: 0), Msg("failed", ChatRole.Assistant, isError: true, index: 1), Msg("next", index: 2) };

        var selected = ContextBuilder.Select(messages, historyBudget: 10_000, factor: 1.0);

        Assert.Equal(["ok", "next"], selected.Select(m => m.Text));
    }

    [Fact]
    public void Select_OverBudget_KeepsNewestAndCurrentMessage()
    {
        var old = Msg(new string('a', 3500), index: 0);
        var middle = Msg(new string('b', 3500), index: 1);
        var current = Msg("now", index: 2);

        var selected = ContextBuilder.Select([old, middle, current], historyBudget: 2_000, factor: 1.0);

        // The oldest does not fit; the middle one is longer than half the budget, so it is shortened (C1.5) and kept.
        Assert.Equal(2, selected.Count);
        Assert.True(selected[0].Shortened);
        Assert.Contains('b', selected[0].Text);
        Assert.Equal("now", selected[1].Text);
    }

    [Fact]
    public void Select_CurrentMessageIsKeptVerbatimEvenWhenItAloneIsOverBudget()
    {
        var text = new string('c', 7000);
        var current = Msg(text, index: 0);

        var selected = ContextBuilder.Select([current], historyBudget: 500, factor: 1.0);

        var only = Assert.Single(selected);
        Assert.False(only.Shortened);
        Assert.Equal(text, only.Text);
    }

    [Fact]
    public void Select_LongMessage_IsShortenedWithMarker()
    {
        var long_ = new string('x', 6000);

        var selected = ContextBuilder.Select([Msg(long_, index: 0), Msg("now", index: 1)], historyBudget: 2_000, factor: 1.0);

        var older = selected[0];
        Assert.True(older.Shortened);
        Assert.Contains("shortened for the assistant", older.Text);
        Assert.True(older.Text.Length < long_.Length);
        Assert.Equal("now", selected[1].Text);
    }

    [Fact]
    public void Select_FactorIncreasesCost_SoFewerMessagesFit()
    {
        var messages = new[] { Msg(new string('a', 700), index: 0), Msg(new string('b', 700), index: 1) };

        var plain = ContextBuilder.Select(messages, historyBudget: 500, factor: 1.0);
        var corrected = ContextBuilder.Select(messages, historyBudget: 500, factor: 2.0);

        Assert.True(plain.Count >= corrected.Count);
    }
}
