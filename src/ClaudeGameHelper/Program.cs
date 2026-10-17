using ClaudeGameHelper.Platform;
using Velopack;

namespace ClaudeGameHelper;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        EditorEnvironment.RemoveInheritedVariables();

        // Velopack first: during install, update, and uninstall it runs this exe with special arguments,
        // handles them here, and exits before any UI starts.
        VelopackApp.Build()
            .OnAfterInstallFastCallback(_ => new StartupRegistration().Enable(Environment.ProcessPath!))
            .OnBeforeUninstallFastCallback(_ => new StartupRegistration().Disable())
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
