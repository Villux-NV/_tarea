using System.Collections.Generic;

namespace Tarea.Models;

public class AppData
{
    public List<Room> Rooms { get; set; } = new();
    public List<Card> Cards { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
}
