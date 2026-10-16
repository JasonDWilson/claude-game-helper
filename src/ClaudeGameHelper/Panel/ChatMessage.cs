namespace ClaudeGameHelper.Panel;

public enum ChatRole
{
    You,
    Claude,
    Note,
}

public sealed class ChatMessage(ChatRole role, string text, IReadOnlyList<string>? screenshots = null) : ObservableObject
{
    private string _text = text;
    private string? _modelLabel;
    private bool _isError;

    public ChatRole Role { get; } = role;
    public IReadOnlyList<string> Screenshots { get; } = screenshots ?? [];

    public string Text { get => _text; set => Set(ref _text, value); }
    public string? ModelLabel { get => _modelLabel; set => Set(ref _modelLabel, value); }
    public bool IsError { get => _isError; set => Set(ref _isError, value); }
}
