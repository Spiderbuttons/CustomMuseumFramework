using System;
using System.Collections.Generic;

namespace CustomMuseumFramework.Models;

public class CustomLostBookData
{
    public string Id { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string? OnReceive { get; set; } = null;
    public string? BroadcastMessage { get; set; } = null;
    public List<InteractionData> Entries { get; set; } = [];
}