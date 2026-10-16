using ClaudeGameHelper.Core.Conversations;
using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Settings;

namespace ClaudeGameHelper.Tests.Conversations;

public class ModelSelectorTests
{
    private static GameProfile Profile(ModelOverrides? models = null) =>
        new("g", "G", @"C:\g", null, [], null, null, models, false, false);

    [Theory]
    [InlineData(0, false, "sonnet", "low", false)]
    [InlineData(1, false, "sonnet", "low", false)]
    [InlineData(2, false, "opus", "medium", true)]
    [InlineData(0, true, "opus", "medium", true)]
    public void ChoosesModel(int shots, bool deep, string model, string effort, bool isDeep) =>
        Assert.Equal(new ModelChoice(model, effort, isDeep), ModelSelector.Choose(new AppSettings(), Profile(), shots, deep));

    [Fact]
    public void ProfileModelOverrideWins()
    {
        var p = Profile(new ModelOverrides("haiku", null, null, "high"));

        Assert.Equal(new ModelChoice("haiku", "low", false), ModelSelector.Choose(new AppSettings(), p, 1, false));
        Assert.Equal(new ModelChoice("opus", "high", true), ModelSelector.Choose(new AppSettings(), p, 1, true));
    }
}
