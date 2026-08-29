using System;
using System.Linq;
using System.Text;
using Tarea.Models;

namespace Tarea.Services;

public static class ExportService
{
    public static string ExportAllToMarkdown(DataService dataService)
    {
        var sb = new StringBuilder();
        var rooms = dataService.Rooms;

        for (int r = 0; r < rooms.Count; r++)
        {
            var room = rooms[r];
            var cards = dataService.GetCardsForRoom(room.Id);

            // Room header
            sb.AppendLine($"# {room.Title}");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(room.Description))
            {
                sb.AppendLine($"> {room.Description}");
                sb.AppendLine();
            }

            // Group cards by status in display order
            var groups = new[]
            {
                (Label: "TODO", Status: CardStatus.Todo),
                (Label: "WIP",  Status: CardStatus.Wip),
                (Label: "DONE", Status: CardStatus.Done),
            };

            foreach (var group in groups)
            {
                var groupCards = cards.Where(c => c.Status == group.Status).ToList();
                if (groupCards.Count == 0)
                    continue;

                sb.AppendLine($"## {group.Label}");
                sb.AppendLine();

                foreach (var card in groupCards)
                {
                    // Checkbox style matches status
                    var checkbox = card.Status == CardStatus.Done ? "[x]" : "[ ]";
                    sb.AppendLine($"- {checkbox} {card.Title}");

                    // Notes as indented sub-items
                    if (card.Notes != null)
                    {
                        foreach (var note in card.Notes)
                        {
                            var noteText = note.IsCrossedOut
                                ? $"~~{note.Text}~~"
                                : note.Text;
                            sb.AppendLine($"  - {noteText}");
                        }
                    }

                    // Metadata line
                    var meta = new StringBuilder();
                    if (card.DueDate.HasValue)
                        meta.Append($"due: {card.DueDate.Value:yyyy-MM-dd}");

                    if (card.Urgency != CardUrgency.None)
                    {
                        if (meta.Length > 0) meta.Append(" | ");
                        meta.Append($"urgency: {card.Urgency.ToString().ToLower()}");
                    }

                    if (meta.Length > 0)
                    {
                        sb.AppendLine($"  {meta}");
                    }

                    sb.AppendLine();
                }
            }

            // Room separator
            if (r < rooms.Count - 1)
            {
                sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }
}
