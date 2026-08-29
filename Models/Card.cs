using System;
using System.Collections.Generic;

namespace Tarea.Models;

public enum CardStatus
{
    Todo,
    Wip,
    Done
}

public enum CardUrgency
{
    None,
    Low,
    Medium,
    High
}

public class Card
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public List<CardNote> Notes { get; set; } = new();
    public string RoomId { get; set; } = string.Empty;
    public int Order { get; set; }
    public CardStatus Status { get; set; } = CardStatus.Todo;
    public CardUrgency Urgency { get; set; } = CardUrgency.None;
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Card() { }

    public Card(string title, string roomId, int order)
    {
        Title = title;
        RoomId = roomId;
        Order = order;
    }
}
