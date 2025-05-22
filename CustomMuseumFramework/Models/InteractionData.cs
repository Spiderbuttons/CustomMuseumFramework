namespace CustomMuseumFramework.Models;

public enum InteractionType
{
    Default,
    Sign,
    Message,
    Letter,
    Custom,
    None
}

public class InteractionData
{
    public InteractionType InteractionType { get; set; } = InteractionType.Default;
    public string? Text { get; set; }
    public string? Action = null;
}