using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tarea.Models;

namespace Tarea.Services;

public class DataService
{
    private static readonly string AppFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tarea");

    private static readonly string FilePath =
        Path.Combine(AppFolder, "projects.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private AppData _data = new();

    public IReadOnlyList<Room> Rooms => _data.Rooms.AsReadOnly();
    public IReadOnlyList<Card> Cards => _data.Cards.AsReadOnly();
    public AppSettings Settings => _data.Settings;


    // ── Lifecycle ──────────────────────────────────────────────
    public void Load()
    {
        EnsureFolder();

        if (!File.Exists(FilePath))
        {
            _data = CreateSeedData();
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            _data = JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? new AppData();
        }
        catch
        {
            // Corrupted file — start fresh but back up the old one
            var backup = FilePath + $".backup-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(FilePath, backup, overwrite: true);
            _data = CreateSeedData();
            Save();
        }
    }

    public void Save()
    {
        EnsureFolder();
        var json = JsonSerializer.Serialize(_data, JsonOptions);
        File.WriteAllText(FilePath, json);
    }


    // ── Room CRUD ──────────────────────────────────────────────
    public Room AddRoom(string title, string color)
    {
        var maxOrder = _data.Rooms.Count > 0
            ? _data.Rooms.Max(r => r.Order) + 1
            : 0;

        var room = new Room(title, color, maxOrder);
        _data.Rooms.Add(room);
        Save();

        return room;
    }

    public void UpdateRoom(string id, string? title = null, string? color = null, string? description = null)
    {
        var room = _data.Rooms.FirstOrDefault(r => r.Id == id);
        if (room == null)
            return;

        if (title != null)
            room.Title = title;

        if (color != null)
            room.Color = color;

        if (description != null)
            room.Description = description;

        Save();
    }

    public void DeleteRoom(string id)
    {
        _data.Rooms.RemoveAll(r => r.Id == id);
        _data.Cards.RemoveAll(c => c.RoomId == id);
        ReorderRooms();
        Save();
    }

    public void ReorderRooms(List<string>? orderedIds = null)
    {
        if (orderedIds != null)
        {
            for (int i = 0; i < orderedIds.Count; i++)
            {
                var room = _data.Rooms.FirstOrDefault(r => r.Id == orderedIds[i]);
                if (room != null) room.Order = i;
            }
        }
        else
        {
            var sorted = _data.Rooms.OrderBy(r => r.Order).ToList();
            for (int i = 0; i < sorted.Count; i++)
                sorted[i].Order = i;
        }
        Save();
    }


    // ── Card CRUD ──────────────────────────────────────────────
    public Card AddCard(string title, string roomId)
    {
        var roomCards = _data.Cards.Where(c => c.RoomId == roomId).ToList();
        var maxOrder = roomCards.Count > 0
            ? roomCards.Max(c => c.Order) + 1
            : 0;

        var card = new Card(title, roomId, maxOrder)
        {
            Status = _data.Settings.DefaultCardStatus
        };

        _data.Cards.Add(card);
        Save();
        return card;
    }

    public void UpdateCard(string cardId, CardStatus? status = null,
        CardUrgency? urgency = null, DateTime? dueDate = null,
        bool clearDueDate = false, DateTime? completedAt = null,
        bool clearCompletedAt = false)
    {
        var card = GetCard(cardId);
        if (card == null)
            return;

        if (status.HasValue)
            card.Status = status.Value;
        if (urgency.HasValue)
            card.Urgency = urgency.Value;
        if (dueDate.HasValue)
            card.DueDate = dueDate.Value;
        if (clearDueDate)
            card.DueDate = null;

        // CompletedAt — pass DateTime.Now to set, null clears via the status cycle logic
        if (completedAt.HasValue)
            card.CompletedAt = completedAt.Value;
        else if (status.HasValue && status.Value != CardStatus.Done)
            card.CompletedAt = null;

        Save();
    }

    public void DeleteCard(string id)
    {
        var card = _data.Cards.FirstOrDefault(c => c.Id == id);
        if (card == null) return;

        var roomId = card.RoomId;
        _data.Cards.Remove(card);
        ReorderCards(roomId);
        Save();
    }

    public void MoveCard(string cardId, string newRoomId, int newOrder)
    {
        var card = _data.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card == null) return;

        var oldRoomId = card.RoomId;
        card.RoomId = newRoomId;
        card.Order = newOrder;

        // Reorder both source and target rooms
        ReorderCards(oldRoomId);
        ReorderCards(newRoomId);
        Save();
    }

    public void ReorderCards(string roomId, List<string>? orderedIds = null)
    {
        var roomCards = _data.Cards
            .Where(c => c.RoomId == roomId)
            .OrderBy(c => c.Order)
            .ToList();

        if (orderedIds != null)
        {
            for (int i = 0; i < orderedIds.Count; i++)
            {
                var card = roomCards.FirstOrDefault(c => c.Id == orderedIds[i]);
                if (card != null) card.Order = i;
            }
        }
        else
        {
            for (int i = 0; i < roomCards.Count; i++)
                roomCards[i].Order = i;
        }
        // Don't call Save() here — callers handle it
    }

    public List<Card> GetCardsForRoom(string roomId)
    {
        return _data.Cards
            .Where(c => c.RoomId == roomId)
            .OrderBy(c => c.Order)
            .ToList();
    }


    // ── Note CRUD ──────────────────────────────────────────────
    public CardNote AddNote(string cardId, string text)
    {
        var card = _data.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card == null)
            return new CardNote();

        var maxOrder = card.Notes.Count > 0
            ? card.Notes.Max(n => n.Order) + 1
            : 0;

        var note = new CardNote(text, maxOrder);
        card.Notes.Add(note);
        Save();
        return note;
    }

    public void UpdateNote(string cardId, string noteId, string text)
    {
        var card = _data.Cards.FirstOrDefault(c => c.Id == cardId);
        var note = card?.Notes.FirstOrDefault(n => n.Id == noteId);
        if (note == null)
            return;

        note.Text = text;
        Save();
    }

    public void MoveNote(string fromCardId, string toCardId, string noteId, int toIndex)
    {
        var fromCard = _data.Cards.FirstOrDefault(c => c.Id == fromCardId);
        var toCard = _data.Cards.FirstOrDefault(c => c.Id == toCardId);
        if (fromCard == null || toCard == null)
            return;

        var note = fromCard.Notes.FirstOrDefault(n => n.Id == noteId);
        if (note == null)
            return;

        fromCard.Notes.Remove(note);
        ReorderNotes(fromCard);

        note.Order = toIndex;
        toCard.Notes.Insert(Math.Min(toIndex, toCard.Notes.Count), note);
        ReorderNotes(toCard);

        Save();
    }

    public void ToggleNoteCrossedOut(string cardId, string noteId)
    {
        var card = _data.Cards.FirstOrDefault(c => c.Id == cardId);
        var note = card?.Notes.FirstOrDefault(n => n.Id == noteId);
        if (note == null)
            return;

        note.IsCrossedOut = !note.IsCrossedOut;
        Save();
    }

    public void DeleteNote(string cardId, string noteId)
    {
        var card = _data.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card == null)
            return;

        card.Notes.RemoveAll(n => n.Id == noteId);
        ReorderNotes(card);
        Save();
    }

    public void ReorderNotes(string cardId, List<string>? orderedIds = null)
    {
        var card = _data.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card == null)
            return;

        ReorderNotes(card, orderedIds);
        Save();
    }

    private void ReorderNotes(Card card, List<string>? orderedIds = null)
    {
        if (orderedIds != null)
        {
            for (int i = 0; i < orderedIds.Count; i++)
            {
                var note = card.Notes.FirstOrDefault(n => n.Id == orderedIds[i]);
                if (note != null)
                    note.Order = i;
            }
        }
        else
        {
            var sorted = card.Notes.OrderBy(n => n.Order).ToList();
            for (int i = 0; i < sorted.Count; i++)
                sorted[i].Order = i;
        }
    }


    // ── Stats (for the footer) ─────────────────────────────────
    public int TotalCards => _data.Cards.Count;
    public int ActiveCards => _data.Cards.Count(c => c.Status != CardStatus.Done);
    public int DoneCards => _data.Cards.Count(c => c.Status == CardStatus.Done);


    // ── Helpers ────────────────────────────────────────────────
    public Card? GetCard(string cardId)
    {
        return _data.Cards.FirstOrDefault(c => c.Id == cardId);
    }

    private static void EnsureFolder()
    {
        if (!Directory.Exists(AppFolder))
            Directory.CreateDirectory(AppFolder);
    }

    private static AppData CreateSeedData()
    {
        var room = new Room("welcome to tarea", "#6366F1", 0)
        {
            Description = "your first room — each card below shows a different feature"
        };

        var cards = new List<Card>
        {
            // Card 1: Getting started — basic navigation
            new("getting started", room.Id, 0)
            {
                Status = CardStatus.Todo,
                Urgency = CardUrgency.None,
                Notes = new List<CardNote>
                {
                    new("double-click a room card to open it", 0),
                    new("click the status badge to cycle: todo → wip → done", 1),
                    new("drag the /// handle to reorder cards", 2),
                }
            },

            // Card 2: Notes — demonstrate note features including cross-out
            new("using notes", room.Id, 1)
            {
                Status = CardStatus.Wip,
                Urgency = CardUrgency.Low,
                Notes = new List<CardNote>
                {
                    new("click + to add a note to any card", 0),
                    new("click a note to select it, double-click to edit", 1),
                    new("crossed-out notes look like this", 2) { IsCrossedOut = true },
                    new("drag notes between cards to move them", 3),
                }
            },

            // Card 3: Urgency & due dates — high urgency with near due date
            new("urgency & due dates", room.Id, 2)
            {
                Status = CardStatus.Todo,
                Urgency = CardUrgency.High,
                DueDate = DateTime.Today.AddDays(3),
                Notes = new List<CardNote>
                {
                    new("click the urgency label to cycle: none → low → med → high", 0),
                    new("click the date to set or clear a due date", 1),
                    new("cards highlight when a due date is close", 2),
                }
            },

            // Card 4: Progress tracking — completed card
            new("track your progress", room.Id, 3)
            {
                Status = CardStatus.Done,
                Urgency = CardUrgency.Medium,
                CompletedAt = DateTime.Now.AddDays(-1),
                Notes = new List<CardNote>
                {
                    new("done cards can be hidden with the [x] toggle", 0),
                    new("the footer bar shows your room's progress", 1),
                    new("flip a card to see its back for quick actions", 2),
                }
            },

            // Card 5: Customization — settings and themes
            new("make it yours", room.Id, 4)
            {
                Status = CardStatus.Wip,
                Urgency = CardUrgency.None,
                Notes = new List<CardNote>
                {
                    new("press S to open settings", 0),
                    new("try the amber or integrale theme", 1),
                    new("adjust card sizes, fonts, and animations", 2),
                }
            },
        };

        return new AppData
        {
            Rooms = new List<Room> { room },
            Cards = cards
        };
    }
}
