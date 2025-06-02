using System.Collections.Generic;

namespace CustomMuseumFramework.Models;

public class CustomLostBookData
{
    public string Id { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string? ReceiveText { get; set; } = null;
    public string? BroadcastText { get; set; } = null;
    public string? MissingText { get; set; } = "";
    public List<InteractionData> Entries { get; set; } = [];
}