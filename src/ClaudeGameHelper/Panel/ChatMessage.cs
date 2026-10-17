namespace ClaudeGameHelper.Panel;

public enum ChatRole
{
    You,
    Claude,
    Note,

    /// <summary>Something watch mode said without being asked.</summary>
    Watch,
}

public sealed class ChatMessage(ChatRole role, string text, IReadOnlyList<string>? screenshots = null) : ObservableObject
{
    private string _text = text;
    private string? _modelLabel;
    private bool _isError;

    public ChatRole Role { get; } = role;

    /// <summary>A time-sensitive watch alert (wrong way, about to miss it), shown with the accent color.</summary>
    public bool IsAlert { get; init; }
    public IReadOnlyList<string> Screenshots { get; } = screenshots ?? [];

    public string Text { get => _text; set => Set(ref _text, value); }
    public string? ModelLabel { get => _modelLabel; set => Set(ref _modelLabel, value); }
    public bool IsError { get => _isError; set => Set(ref _isError, value); }
}
