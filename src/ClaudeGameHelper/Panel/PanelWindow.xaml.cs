using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClaudeGameHelper.Core.Layout;
using ClaudeGameHelper.Core.Settings;
using ClaudeGameHelper.Platform;
using Forms = System.Windows.Forms;

namespace ClaudeGameHelper.Panel;

/// <summary>The side panel. Opaque and topmost; takes focus only in Ask mode.</summary>
public partial class PanelWindow : Window, IPanelView
{
    private readonly PanelViewModel _vm;
    private readonly PanelBounds? _savedBounds;
    private readonly Action<PanelBounds> _saveBounds;
    private readonly Func<IntPtr> _gameWindow;
    private readonly DispatcherTimer _saveTimer;
    private bool _placed;

    public PanelWindow(PanelViewModel vm, PanelBounds? savedBounds, Action<PanelBounds> saveBounds, Func<IntPtr> gameWindow)
    {
        _vm = vm;
        _savedBounds = savedBounds;
        _saveBounds = saveBounds;
        _gameWindow = gameWindow;
        InitializeComponent();
        DataContext = vm;

        _saveTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => SaveBounds(), Dispatcher) { IsEnabled = false };
        LocationChanged += (_, _) => RestartSaveTimer();
        SizeChanged += (_, _) => RestartSaveTimer();

        vm.PropertyChanged += OnViewModelChanged;
        WatchMessages(vm.Messages);
    }

    public event Action? SendRequested;
    public event Action? StopRequested;
    public event Action? HideRequested;
    public event Action? NotAGameRequested;

    /// <summary>Set before the app exits so closing really closes instead of hiding.</summary>
    public bool AllowClose { get; set; }

    public bool IsShown => IsVisible;

    public void ShowPassive()
    {
        EnsurePlaced();
        FocusHelper.SetNoActivate(this, true);
        if (!IsVisible)
            Show();
    }

    public void ShowForAsk()
    {
        EnsurePlaced();
        FocusHelper.SetNoActivate(this, false);
        if (!IsVisible)
            Show();
        Activate();
        QuestionBox.Focus();
        Keyboard.Focus(QuestionBox);
        QuestionBox.CaretIndex = QuestionBox.Text.Length;
    }

    public void HidePanel() => Hide();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            HideRequested?.Invoke();
        }
        base.OnClosing(e);
    }

    private void EnsurePlaced()
    {
        if (_placed)
            return;
        _placed = true;

        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        RectD ToDips(System.Drawing.Rectangle r) => new(r.X / scale, r.Y / scale, r.Width / scale, r.Height / scale);

        var workAreas = Forms.Screen.AllScreens.Select(s => ToDips(s.WorkingArea)).ToList();
        var gameHwnd = _gameWindow();
        var gameScreen = gameHwnd != IntPtr.Zero ? Forms.Screen.FromHandle(gameHwnd) : Forms.Screen.PrimaryScreen!;
        var rect = PanelPlacement.Choose(_savedBounds, workAreas, ToDips(gameScreen.WorkingArea));

        Left = rect.X;
        Top = rect.Y;
        Width = rect.Width;
        Height = rect.Height;
    }

    private void RestartSaveTimer()
    {
        if (!_placed)
            return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveBounds()
    {
        _saveTimer.Stop();
        _saveBounds(new PanelBounds(Left, Top, Width, Height));
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PanelViewModel.Messages))
        {
            WatchMessages(_vm.Messages);
            ScrollToEndSoon();
        }
    }

    private void WatchMessages(INotifyCollectionChanged messages)
    {
        messages.CollectionChanged -= OnMessagesChanged;
        messages.CollectionChanged += OnMessagesChanged;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var message in e.NewItems?.OfType<ChatMessage>() ?? [])
            message.PropertyChanged += (_, _) => ScrollToEndSoon();
        ScrollToEndSoon();
    }

    private void ScrollToEndSoon() => Dispatcher.BeginInvoke(DispatcherPriority.Background, () => Scroller.ScrollToEnd());

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => HideRequested?.Invoke();

    private void Stop_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke();

    private void NotAGame_Click(object sender, RoutedEventArgs e) => NotAGameRequested?.Invoke();

    private void RemovePending_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path })
            _vm.RemovePending(path);
    }

    private void QuestionBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            SendRequested?.Invoke();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            HideRequested?.Invoke();
        }
    }

    private void QuestionBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm.IsAskMode)
            return;
        _vm.IsAskMode = true;
        ShowForAsk();
    }
}
