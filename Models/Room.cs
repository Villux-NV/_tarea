using System;

namespace Tarea.Models;

public class Room
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#6366F1";  // default indigo
    public int Order { get; set; }

    public Room() { }

    public Room(string title, string color, int order)
    {
        Title = title;
        Color = color;
        Order = order;
    }
}
