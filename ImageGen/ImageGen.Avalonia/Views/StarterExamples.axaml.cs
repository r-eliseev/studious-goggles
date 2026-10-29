using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ImageGen.Avalonia.Views;

/// <summary>The three example prompts of the empty state; the data context is the sessions page model.</summary>
public partial class StarterExamples : UserControl
{
    public StarterExamples()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
