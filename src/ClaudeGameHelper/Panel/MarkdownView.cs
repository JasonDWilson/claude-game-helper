using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ClaudeGameHelper.Panel;

/// <summary>
/// Read-only Markdown, rendered with MdXaml into a RichTextBox (which sizes to its content inside a
/// ScrollViewer). While text streams in, it re-renders at most every 150 ms.
/// </summary>
public sealed class MarkdownView : ContentControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(MarkdownView), new PropertyMetadata("", (d, _) => ((MarkdownView)d).ScheduleRender()));

    private static readonly TimeSpan Throttle = TimeSpan.FromMilliseconds(150);
    private readonly MdXaml.Markdown _engine = new();
    private readonly RichTextBox _box;
    private readonly DispatcherTimer _timer;

    public MarkdownView()
    {
        _box = new RichTextBox
        {
            IsReadOnly = true,
            IsDocumentEnabled = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Content = _box;
        _timer = new DispatcherTimer(Throttle, DispatcherPriority.Background, (_, _) => Render(), Dispatcher) { IsEnabled = false };
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void ScheduleRender()
    {
        if (!_timer.IsEnabled)
            _timer.Start();
    }

    private void Render()
    {
        _timer.Stop();
        var document = _engine.Transform(Text ?? "");
        document.Foreground = Foreground;
        document.FontFamily = FontFamily;
        document.FontSize = FontSize;
        document.PagePadding = new Thickness(0);
        document.TextAlignment = TextAlignment.Left;
        _box.Foreground = Foreground;
        _box.Document = document;
    }
}
