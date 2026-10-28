using System.Reflection;
using Avalonia.Markup.Xaml;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Avalonia.Markup;

/// <summary>
/// <c>{markup:Res Key}</c>: the text of the <see cref="AppResources"/> property <c>Key</c>. Used instead of
/// <c>x:Static</c>, which the Avalonia XAML compiler does not resolve for types of the presentation assembly.
/// </summary>
public sealed class ResExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Lookup(key);

    public static string Lookup(string key)
    {
        var property = typeof(AppResources).GetProperty(key, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Нет строки интерфейса «{key}».");
        return (string?)property.GetValue(null) ?? string.Empty;
    }
}
