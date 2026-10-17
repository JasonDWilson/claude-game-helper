using System.Collections.ObjectModel;
using ClaudeGameHelper.Core.Claude;
using ClaudeGameHelper.Core.Conversations;
using ClaudeGameHelper.Core.Profiles;
using ClaudeGameHelper.Core.Watch;

namespace ClaudeGameHelper.Panel;

/// <summary>Everything the panel shows: one chat history per profile, the Ask box, and the status line.</summary>
public sealed class PanelViewModel : ObservableObject
{
    public const string RolloverNote = "Started a fresh conversation (the last one got long). Your tracking files carry over.";

    private readonly Dictionary<string, ObservableCollection<ChatMessage>> _histories = [];
    private readonly Dictionary<string, string?> _activeTracking = [];
    private string _profileId = "";
    private string _profileName = "";
    private string? _trackingLabel;
    private bool _isAutoProfile;
    private string? _watchGoalText;
    private ObservableCollection<ChatMessage> _messages = [];
    private string? _activeTrackingName;
    private string _questionText = "";
    private bool _includeScreenshot = true;
    private bool _deep;
    private bool _isAskMode;
    private bool _isBusy;
    private string? _statusText;

    public string ProfileName { get => _profileName; private set => Set(ref _profileName, value); }
    public string? TrackingLabel { get => _trackingLabel; private set => Set(ref _trackingLabel, value); }
    public bool IsAutoProfile { get => _isAutoProfile; private set => Set(ref _isAutoProfile, value); }
    public ObservableCollection<ChatMessage> Messages { get => _messages; private set => Set(ref _messages, value); }
    public ObservableCollection<string> TrackingNames { get; } = [];
    public ObservableCollection<string> PendingScreenshots { get; } = [];

    public string? ActiveTrackingName
    {
        get => _activeTrackingName;
        set
        {
            if (Set(ref _activeTrackingName, value))
                _activeTracking[_profileId] = value;
        }
    }

    public string QuestionText { get => _questionText; set => Set(ref _questionText, value); }
    public bool IncludeScreenshot { get => _includeScreenshot; set => Set(ref _includeScreenshot, value); }
    public bool Deep { get => _deep; set => Set(ref _deep, value); }
    public bool IsAskMode { get => _isAskMode; set => Set(ref _isAskMode, value); }
    public bool IsBusy { get => _isBusy; set => Set(ref _isBusy, value); }
    public string? StatusText { get => _statusText; set => Set(ref _statusText, value); }

    public void SwitchProfile(GameProfile profile, IReadOnlyList<string> trackingNames)
    {
        _profileId = profile.Id;
        ProfileName = profile.DisplayName;
        TrackingLabel = profile.TrackingLabel;
        IsAutoProfile = profile.IsAuto;

        if (!_histories.TryGetValue(profile.Id, out var history))
        {
            history = [];
            _histories[profile.Id] = history;
        }
        Messages = history;

        ReplaceTrackingNames(trackingNames);
        _activeTracking.TryGetValue(profile.Id, out var remembered);
        ActiveTrackingName = remembered != null && trackingNames.Contains(remembered) ? remembered
            : trackingNames.Count == 1 ? trackingNames[0]
            : null;
    }

    /// <summary>Refreshes the tracking list; selects a newly added file when none is active.</summary>
    public void UpdateTrackingNames(IReadOnlyList<string> names)
    {
        var added = names.Except(TrackingNames).ToList();
        ReplaceTrackingNames(names);

        if (ActiveTrackingName != null && !names.Contains(ActiveTrackingName))
            ActiveTrackingName = null;
        if (ActiveTrackingName == null && added.Count > 0)
            ActiveTrackingName = added[0];
    }

    public ChatMessage BeginExchange(string question, IReadOnlyList<string> screenshots)
    {
        Messages.Add(new ChatMessage(ChatRole.You, question, screenshots));
        var answer = new ChatMessage(ChatRole.Claude, "");
        Messages.Add(answer);
        IsBusy = true;
        StatusText = "Working…";
        return answer;
    }

    public void Apply(ChatMessage answer, ClaudeEvent e)
    {
        switch (e)
        {
            case TextDelta delta:
                answer.Text += delta.Text;
                break;
            case Activity activity:
                StatusText = activity.Text;
                break;
            case UsageLimit limit:
                StatusText = limit.ResetsAt is { } at
                    ? $"Claude usage limit reached — resets at {at.ToLocalTime():h:mm tt}"
                    : "Claude usage limit reached.";
                break;
        }
    }

    public void Finish(ChatMessage answer, AskOutcome outcome)
    {
        answer.Text = outcome.AnswerText;
        answer.ModelLabel = outcome.Model;
        answer.IsError = outcome.IsError;
        IsBusy = false;
        StatusText = null;

        if (outcome.RolledOver)
        {
            var index = Messages.IndexOf(answer);
            Messages.Insert(Math.Max(0, index - 1), new ChatMessage(ChatRole.Note, RolloverNote));
        }
    }

    public void AddPending(string path)
    {
        PendingScreenshots.Add(path);
        IncludeScreenshot = true;
    }

    public void RemovePending(string path) => PendingScreenshots.Remove(path);

    public void ClearAsk()
    {
        QuestionText = "";
        PendingScreenshots.Clear();
        Deep = false;
        IncludeScreenshot = true;
    }

    public void AddNote(string text) => Messages.Add(new ChatMessage(ChatRole.Note, text));

    public void AddWatchMessage(WatchMessageKind kind, string text) =>
        Messages.Add(new ChatMessage(ChatRole.Watch, text) { IsAlert = kind == WatchMessageKind.GoalAlert });

    /// <summary>The goal watch mode is following (shown in the header), or null.</summary>
    public string? WatchGoalText { get => _watchGoalText; set => Set(ref _watchGoalText, value); }

    private void ReplaceTrackingNames(IReadOnlyList<string> names)
    {
        TrackingNames.Clear();
        foreach (var name in names)
            TrackingNames.Add(name);
    }
}
