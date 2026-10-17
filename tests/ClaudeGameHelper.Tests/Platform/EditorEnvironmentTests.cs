using ClaudeGameHelper.Platform;

namespace ClaudeGameHelper.Tests.Platform;

public class EditorEnvironmentTests
{
    [Fact]
    public void ClearsTheElectronRunAsNodeVariable()
    {
        var before = Environment.GetEnvironmentVariable("ELECTRON_RUN_AS_NODE");
        Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", "1");
        try
        {
            EditorEnvironment.RemoveInheritedVariables();

            Assert.Null(Environment.GetEnvironmentVariable("ELECTRON_RUN_AS_NODE"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", before);
        }
    }
}
