using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tarea.Models;
using Tarea.Services;

namespace Tarea.ViewModels;

public partial class CasaViewModel : ObservableObject
{
    private readonly DataService _dataService;

    [ObservableProperty]
    private ObservableCollection<RoomSummary> _rooms = new();

    [ObservableProperty]
    private string _newRoomTitle = string.Empty;

    public int CardWidth => _dataService.Settings.CardWidth;
    public int CardHeight => _dataService.Settings.CardHeight;

    public event Action<string>? NavigateToRoom;
    public Func<string, bool>? ConfirmAction { get; set; }

    public CasaViewModel(DataService dataService)
    {
        _dataService = dataService;
        Refresh();
    }

    public void Refresh()
    {
        Rooms.Clear();
        foreach (var room in _dataService.Rooms.OrderBy(r => r.Order))
        {
            var cards = _dataService.GetCardsForRoom(room.Id);
            Rooms.Add(new RoomSummary(room, cards, _dataService));
        }

        OnPropertyChanged(nameof(CardWidth));
        OnPropertyChanged(nameof(CardHeight));
    }

    // ── Rooms ─────────────────────────────────────────────────

    [RelayCommand]
    private void AddRoom()
    {
        var title = NewRoomTitle.Trim();
        if (string.IsNullOrEmpty(title)) return;

        _dataService.AddRoom(title, "#6366F1");
        NewRoomTitle = string.Empty;
        Refresh();
    }

    [RelayCommand]
    private void DeleteRoom(string roomId)
    {
        if (_dataService.Settings.ConfirmOnDelete)
        {
            var room = _dataService.Rooms.FirstOrDefault(r => r.Id == roomId);
            var title = room?.Title ?? "this room";
            if (ConfirmAction != null && !ConfirmAction($"Delete \"{title}\" and all its cards?"))
                return;
        }

        _dataService.DeleteRoom(roomId);
        Refresh();
    }

    [RelayCommand]
    private void OpenRoom(string roomId)
    {
        NavigateToRoom?.Invoke(roomId);
    }

    public void ReorderRoom(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Rooms.Count ||
            toIndex < 0 || toIndex >= Rooms.Count ||
            fromIndex == toIndex)
            return;

        Rooms.Move(fromIndex, toIndex);

        // Sync the new order back to the data service
        var orderedIds = Rooms.Select(r => r.Id).ToList();
        _dataService.ReorderRooms(orderedIds);
    }

    public void CancelAllDescriptionEdits()
    {
        foreach (var room in Rooms)
        {
            if (room.IsEditingDescription)
                room.CancelDescription();
        }
    }

    // ── Quick Add ─────────────────────────────────────────────────
    [RelayCommand]
    private void ShowQuickAdd(string roomId)
    {
        // Close any other quick-add first
        CancelAllQuickAdds();
        var room = Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room != null)
            room.IsQuickAdding = true;
    }

    [RelayCommand]
    private void QuickAddCard(string roomId)
    {
        var room = Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room == null)
            return;

        var title = room.QuickAddTitle.Trim();
        if (string.IsNullOrEmpty(title))
        {
            room.CancelQuickAdd();
            return;
        }

        _dataService.AddCard(title, roomId);
        room.CancelQuickAdd();
        Refresh();
        RefreshStats();
    }

    [RelayCommand]
    private void CancelQuickAdd(string roomId)
    {
        var room = Rooms.FirstOrDefault(r => r.Id == roomId);
        room?.CancelQuickAdd();
    }

    public void CancelAllQuickAdds()
    {
        foreach (var room in Rooms)
        {
            if (room.IsQuickAdding)
                room.CancelQuickAdd();
        }
    }

    // ── Stats for the footer ──────────────────────────────────
    public int TotalCards => _dataService.TotalCards;
    public int ActiveCards => _dataService.ActiveCards;
    public int DoneCards => _dataService.DoneCards;

    public void RefreshStats()
    {
        OnPropertyChanged(nameof(TotalCards));
        OnPropertyChanged(nameof(ActiveCards));
        OnPropertyChanged(nameof(DoneCards));
    }
}


/// <summary>
/// Lightweight projection of a Room for display in the Casa grid.
/// Includes the computed task count.
/// </summary>
public partial class RoomSummary : ObservableObject
{
    private readonly DataService _dataService;

    public string Id { get; }
    public string Title { get; }
    public string Color { get; }
    public int CardCount { get; }
    public int TodoCount { get; }
    public int WipCount { get; }
    public int DoneCount { get; }
    public List<CardTitleInfo> CardTitles { get; }

    public int CardWidth => _dataService.Settings.CardWidth;
    public int CardHeight => _dataService.Settings.CardHeight;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private bool _isQuickAdding;

    [ObservableProperty]
    private string _quickAddTitle = string.Empty;

    [ObservableProperty]
    private bool _isEditingDescription;

    [ObservableProperty]
    private string _editDescriptionText = string.Empty;

    public string DescriptionDisplay => string.IsNullOrWhiteSpace(Description)
        ? "description..."
        : Description;

    public string ProgressSummary => CardCount > 0
        ? $"progress: {DoneCount}/{CardCount} done"
        : "no cards";

    public bool IsDescriptionPlaceholder => string.IsNullOrWhiteSpace(Description);

    public RoomSummary(Room room, List<Card> cards, DataService dataService)
    {
        _dataService = dataService;
        Id = room.Id;
        Title = room.Title;
        Color = room.Color;
        _description = room.Description;
        CardCount = cards.Count;
        TodoCount = cards.Count(c => c.Status == CardStatus.Todo);
        WipCount = cards.Count(c => c.Status == CardStatus.Wip);
        DoneCount = cards.Count(c => c.Status == CardStatus.Done);

        var grouped = cards
            .OrderBy(c => c.Status == CardStatus.Todo ? 0 : c.Status == CardStatus.Wip ? 1 : 2)
            .ThenBy(c => c.Order)
            .ToList();

        var titles = new List<CardTitleInfo>();
        CardStatus? lastStatus = null;

        foreach (var c in grouped)
        {
            if (c.Status != lastStatus)
            {
                var label = c.Status switch
                {
                    CardStatus.Todo => dataService.Settings.TodoLabel.ToLower(),
                    CardStatus.Wip => dataService.Settings.WipLabel.ToLower(),
                    CardStatus.Done => dataService.Settings.DoneLabel.ToLower(),
                    _ => ""
                };
                titles.Add(new CardTitleInfo
                {
                    IsStatusHeader = true,
                    HeaderText = $"── {label} ──",
                    Status = c.Status
                });
                lastStatus = c.Status;
            }

            titles.Add(new CardTitleInfo
            {
                Title = c.Title,
                Status = c.Status,
                IsPastDue = c.DueDate.HasValue && c.DueDate.Value.Date < DateTime.Today,
                IsDueDateWarning = c.DueDate.HasValue
                    && (c.DueDate.Value - DateTime.Today).TotalDays <= dataService.Settings.DueDateWarningDays
            });

            if (titles.Count >= 12) break;
        }

        CardTitles = titles;
    }

    public void StartEditDescription()
    {
        EditDescriptionText = Description;
        IsEditingDescription = true;
    }

    [RelayCommand]
    private void CommitDescription()
    {
        var text = EditDescriptionText.Trim();
        _dataService.UpdateRoom(Id, description: text);
        Description = text;
        IsEditingDescription = false;
        OnPropertyChanged(nameof(DescriptionDisplay));
        OnPropertyChanged(nameof(IsDescriptionPlaceholder));
    }

    [RelayCommand]
    public void CancelDescription()
    {
        EditDescriptionText = Description;
        IsEditingDescription = false;
    }

    public void CancelQuickAdd()
    {
        QuickAddTitle = string.Empty;
        IsQuickAdding = false;
    }
}


public class CardTitleInfo
{
    public string Title { get; set; } = string.Empty;
    public bool IsDueDateWarning { get; set; }
    public bool IsPastDue { get; set; }
    public CardStatus Status { get; set; }
    public bool IsStatusHeader { get; set; }
    public string HeaderText { get; set; } = string.Empty;

    // ── Computed display properties (replace WPF DataTriggers) ──
    public string ForegroundBrushKey => IsStatusHeader
        ? (Status == CardStatus.Done ? "GreenBrush" : "MutedBrush")
        : IsPastDue ? "OrangeBrush"
        : IsDueDateWarning ? "YellowBrush"
        : Status == CardStatus.Done ? "GreenBrush"
        : "MutedBrush";

    public bool IsStrikethrough => !IsStatusHeader && Status == CardStatus.Done;
}
