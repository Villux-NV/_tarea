using System;

namespace Tarea.Models;

public class CardNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsCrossedOut { get; set; }

    public CardNote() { }

    public CardNote(string text, int order)
    {
        Text = text;
        Order = order;
    }
}
