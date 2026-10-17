using System.Windows.Threading;
using ClaudeGameHelper.Core.Claude;
using ClaudeGameHelper.Core.Conversations;
using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Settings;
using ClaudeGameHelper.Panel;

namespace ClaudeGameHelper.Tests.Panel;

public class PanelWindowSmokeTests : IDisposable
{
    private readonly StaWindowHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void ShowsEveryKindOfMessageWithoutErrors()
    {
        var saved = new List<PanelBounds>();

        var (shown, height) = _host.Invoke(() =>
        {
            var vm = new PanelViewModel();
            vm.SwitchProfile(new GameProfile("g", "Game", @"C:\g", null, [], "Character", null, null, false, false), ["vault-dweller"]);
            vm.AddNote("A note.");
            var answer = vm.BeginExchange("Is this good?", []);
            vm.Apply(answer, new TextDelta("**Scrip it.**\n\n- one\n- two"));
            vm.Finish(answer, new AskOutcome("**Scrip it.**\n\n- one\n- two", false, false, false, "sonnet"));
            var error = vm.BeginExchange("And this?", []);
            vm.Finish(error, new AskOutcome("You've hit your limit", true, false, false, "opus"));
            vm.StatusText = "Searching the web: x";
            vm.AddWatchMessage(ClaudeGameHelper.Core.Watch.WatchMessageKind.GoalAlert, "Wrong way. Turn around.");
            vm.AddWatchMessage(ClaudeGameHelper.Core.Watch.WatchMessageKind.Tip, "Legendary enemy ahead.");
            vm.WatchGoalText = "Where's the Whitespring?";

            var window = new PanelWindow(vm, null, saved.Add, () => IntPtr.Zero);
            window.ShowPassive();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var result = (window.IsShown, window.ActualHeight);
            window.AllowClose = true;
            window.Close();
            return result;
        });

        Assert.True(shown);
        Assert.True(height > 0);
    }
}
