using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Settings;

namespace ClaudeGameHelper.Core.Conversations;

public sealed record ModelChoice(string Model, string Effort, bool IsDeep);

public static class ModelSelector
{
    /// <summary>The stronger model for two or more screenshots or a "Deep" question; the fast model otherwise.</summary>
    public static ModelChoice Choose(AppSettings settings, GameProfile profile, int screenshotCount, bool deep)
    {
        var models = profile.Models;
        return deep || screenshotCount >= 2
            ? new ModelChoice(models?.DeepModel ?? settings.DeepModel, models?.DeepEffort ?? settings.DeepEffort, true)
            : new ModelChoice(models?.FastModel ?? settings.FastModel, models?.FastEffort ?? settings.FastEffort, false);
    }
}
