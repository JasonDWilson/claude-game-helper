using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using ClaudeGameHelper.Capture;
using ClaudeGameHelper.Core;
using ClaudeGameHelper.Core.Claude;
using ClaudeGameHelper.Core.Conversations;
using ClaudeGameHelper.Core.Hotkeys;
using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Settings;
using ClaudeGameHelper.Panel;
using ClaudeGameHelper.Platform;
using ClaudeGameHelper.Tray;

namespace ClaudeGameHelper;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private SettingsStore? _store;
    private AppSettings _settings = new();
    private bool _settingsFileIsBroken;
    private string _dataRoot = "";
    private string _templateInstructions = "";
    private TrayIcon? _tray;
    private PanelViewModel? _vm;
    private PanelWindow? _panel;
    private HelperController? _controller;
    private ForegroundWatcher? _watcher;
    private GlobalHotkeys? _hotkeys;
    private HotkeyBindingSet? _bindings;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, "ClaudeGameHelper.SingleInstance", out var firstInstance);
        if (!firstInstance)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            ReportCrash("Unhandled UI exception.", args.Exception);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ReportCrash("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Error("Fatal exception.", args.ExceptionObject as Exception);

        var startup = new StartupRegistration();
        _tray = new TrayIcon(ShowPanel, () => _controller?.NewConversation(), OpenTrackingFile, TrackingState, OpenSettings,
            () => startup.IsEnabled,
            on => { if (on) startup.Enable(Environment.ProcessPath!); else startup.Disable(); },
            AppVersion.Current, Quit);

        _store = new SettingsStore(AppPaths.SettingsFile);
        var loaded = _store.Load();
        _settings = loaded.Settings;
        _settingsFileIsBroken = loaded.Error != null;
        _dataRoot = AppPaths.DataRoot(_settings.DataFolder);
        Log.Init(AppPaths.LogsDir(_dataRoot));
        Log.Info($"Starting. Data: {_dataRoot}");
        if (loaded.Error != null)
        {
            Log.Info(loaded.Error);
            _tray.Balloon(loaded.Error);
        }

        var profiles = ProfileLoader.LoadAll(AppPaths.BuiltInProfilesDir, _dataRoot, _settings.NotGames);
        _templateInstructions = profiles.TemplateInstructionsPath;
        foreach (var error in profiles.Errors)
            Log.Info(error);
        if (profiles.Fallback == null)
        {
            Fatal(string.Join(Environment.NewLine, profiles.Errors));
            return;
        }
        if (profiles.Errors.Count > 0)
            _tray.Balloon(string.Join(Environment.NewLine, profiles.Errors));

        var ask = new AskService(_settings, LocateClaude, new ClaudeRunner(), () => DateTime.Now);
        _vm = new PanelViewModel();
        _panel = new PanelWindow(_vm, _settings.Panel, SavePanelBounds, () => _controller?.GameWindow ?? IntPtr.Zero);
        _controller = new HelperController(profiles.Profiles, profiles.Fallback, ask, new WindowCapture(() => _settings),
            _panel, _vm, FocusHelper.ReturnTo, Environment.ProcessId,
            w => AutoProfiles.TryCreate(_dataRoot, _templateInstructions, w.ProcessName, w.Title, _settings.NotGames),
            MarkNotAGame);

        _panel.SendRequested += () => Run(_controller.SendAskBoxAsync);
        _panel.StopRequested += _controller.Stop;
        _panel.HideRequested += _controller.HidePanel;
        _panel.NotAGameRequested += _controller.NotAGame;

        _watcher = new ForegroundWatcher();
        _watcher.Changed += _controller.OnForegroundChanged;
        if (ForegroundWatcher.Current() is { } current)
            _controller.OnForegroundChanged(current);

        _hotkeys = new GlobalHotkeys();
        _hotkeys.Pressed += OnHotkey;
        ApplyHotkeys(_controller.ActiveProfile);
        _controller.ActiveProfileChanged += ApplyHotkeys;

        Log.Info($"Ready ({AppVersion.Current}). Profiles: {string.Join(", ", profiles.Profiles.Select(p => p.Id))}; active: {_controller.ActiveProfile.Id}.");
        _ = UpdateChecker.RunAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _watcher?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        Log.Info("Exited.");
        base.OnExit(e);
    }

    private static ClaudeLocateResult LocateClaude() => ClaudeLocator.Find(
        ((App)Current)._settings.ClaudePath,
        Environment.GetEnvironmentVariable("PATH"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vscode", "extensions"),
        ClaudeLocator.ProbeVersion);

    private void OnHotkey(HotkeyAction action)
    {
        if (_controller == null)
            return;

        switch (action)
        {
            case HotkeyAction.Screen:
                Run(_controller.OnScreenHotkeyAsync);
                break;
            case HotkeyAction.Ask:
                Run(_controller.OnAskHotkeyAsync);
                break;
            case HotkeyAction.Toggle:
                _controller.OnToggleHotkey();
                break;
        }
    }

    private void ApplyHotkeys(GameProfile profile)
    {
        HotkeyBindingSet set;
        try
        {
            set = HotkeyBindings.Resolve(_settings, profile);
        }
        catch (FormatException ex)
        {
            _tray?.Balloon($"{ex.Message} Using the default hotkeys.");
            set = HotkeyBindings.Resolve(new AppSettings(), null);
        }

        if (set == _bindings || _hotkeys == null)
            return;
        _bindings = set;

        foreach (var chord in _hotkeys.Apply(set))
        {
            Log.Info($"Hotkey {chord} is taken.");
            _tray?.Balloon($"{chord} is already used by another app. Change it in settings.json and restart the helper.");
        }
    }

    private void ShowPanel() => _panel?.ShowPassive();

    private (string Label, bool Enabled) TrackingState()
    {
        var label = _vm?.TrackingLabel ?? "tracking";
        return (label, _vm?.TrackingLabel != null && _vm.ActiveTrackingName != null);
    }

    private void OpenTrackingFile()
    {
        if (_controller == null || _vm?.ActiveTrackingName is not { } name)
            return;
        OpenWithShell(TrackingFiles.FullPath(_controller.ActiveProfile, name));
    }

    private void OpenSettings()
    {
        OpenWithShell(AppPaths.SettingsFile);
        _tray?.Balloon("Changes take effect the next time the helper starts.");
    }

    private static void OpenWithShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
        }
    }

    private void SavePanelBounds(PanelBounds bounds) => SaveSettings(_settings with { Panel = bounds });

    private void MarkNotAGame(GameProfile profile) =>
        SaveSettings(_settings with { NotGames = [.. _settings.NotGames, profile.Id] });

    private void SaveSettings(AppSettings settings)
    {
        _settings = settings;
        // Never overwrite a settings file someone broke by hand; they'd lose their edits.
        if (!_settingsFileIsBroken)
            _store?.Save(_settings);
    }

    private void Quit()
    {
        if (_panel != null)
        {
            _panel.AllowClose = true;
            _panel.Close();
        }
        Shutdown();
    }

    private void Fatal(string message)
    {
        Log.Error(message);
        MessageBox.Show(message, "Claude Game Helper", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown();
    }

    private async void Run(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ReportCrash("Hotkey action failed.", ex);
        }
    }

    private void ReportCrash(string message, Exception ex)
    {
        Log.Error(message, ex);
        _tray?.Balloon("Something went wrong. Details are in the log.");
    }
}
