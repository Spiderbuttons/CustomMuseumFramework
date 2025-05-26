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
    public string? Text { get; set; } = " - {0} - ^{1}";
    public string? Action = null;
}