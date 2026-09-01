using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tarea.Models;
using Tarea.Services;

namespace Tarea.ViewModels;

public partial class RoomViewModel : ObservableObject
{
    private readonly DataService _dataService;
    private bool _isFreshLoad;

    [ObservableProperty]
    private string _roomId = string.Empty;

    [ObservableProperty]
    private string _roomTitle = string.Empty;

    [ObservableProperty]
    private string _roomColor = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CardViewModel> _cards = new();

    [ObservableProperty]
    private string _newCardDescription = string.Empty;

    [ObservableProperty]
    private bool _showCompletedCards;

    public Action<CardViewModel>? AnimateHideCard { get; set; }

    public int CardWidth => _dataService.Settings.CardWidth;
    public int CardHeight => _dataService.Settings.CardHeight;
    public int TodoCardCount => Cards.Count(c => c.Status == CardStatus.Todo);
    public int WipCardCount => Cards.Count(c => c.Status == CardStatus.Wip);
    public int DoneCardCount => Cards.Count(c => c.Status == CardStatus.Done);

    public ObservableCollection<CardViewModel> CompletedCards =>
        new(Cards.Where(c => c.Status == CardStatus.Done)
            .OrderByDescending(c => c.CompletedAtRaw));

    public int CompletedCardCount => Cards.Count(c => c.Status == CardStatus.Done);

    public string ProgressSummary => $"{DoneCardCount} done · {WipCardCount} wip · {TodoCardCount} todo";

    public Func<string, Task<bool>>? ConfirmAction { get; set; }

    public RoomViewModel(DataService dataService)
    {
        _dataService = dataService;
    }

    public void LoadRoom(string roomId)
    {
        RoomId = roomId;
        var room = _dataService.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room == null)
            return;

        RoomTitle = room.Title;
        RoomColor = room.Color;
        OnPropertyChanged(nameof(CardWidth));
        OnPropertyChanged(nameof(CardHeight));

        _isFreshLoad = true;
        RefreshCards();
    }


    // ── Cards ─────────────────────────────────────────────────
    internal void RefreshCards()
    {
        var hiddenIds = _isFreshLoad
            ? new System.Collections.Generic.HashSet<string>()
            : Cards.Where(c => c.IsHidden).Select(c => c.Id).ToHashSet();

        var isFresh = _isFreshLoad;
        _isFreshLoad = false;

        Cards.Clear();
        foreach (var card in _dataService.GetCardsForRoom(RoomId))
        {
            var vm = new CardViewModel(card, _dataService, RefreshCards, ConfirmAction,
                findCardById: id => Cards.FirstOrDefault(c => c.Id == id));
            vm.AnimateHideAction = AnimateHideCard;

            // Restore hidden state from before refresh
            if (hiddenIds.Contains(vm.Id))
                vm.IsHidden = true;

            // Auto-hide done cards on fresh load if setting is enabled
            if (isFresh
                && _dataService.Settings.HideOnComplete
                && vm.Status == CardStatus.Done)
                vm.IsHidden = true;

            Cards.Add(vm);
        }

        OnPropertyChanged(nameof(TodoCardCount));
        OnPropertyChanged(nameof(WipCardCount));
        OnPropertyChanged(nameof(DoneCardCount));
        OnPropertyChanged(nameof(CompletedCards));
        OnPropertyChanged(nameof(CompletedCardCount));
        OnPropertyChanged(nameof(ProgressSummary));
    }

    [RelayCommand]
    private void AddCard()
    {
        var title = NewCardDescription.Trim();
        if (string.IsNullOrEmpty(title))
            return;

        _dataService.AddCard(title, RoomId);
        NewCardDescription = string.Empty;
        RefreshCards();
    }

    public void ReorderCard(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Cards.Count ||
            toIndex < 0 || toIndex >= Cards.Count ||
            fromIndex == toIndex)
            return;

        Cards.Move(fromIndex, toIndex);

        var orderedIds = Cards.Select(c => c.Id).ToList();
        _dataService.ReorderCards(RoomId, orderedIds);
        _dataService.Save();
    }


    // ── Notes ─────────────────────────────────────────────────
    public void DeselectAllNotes(string? exceptCardId = null, string? exceptNoteId = null)
    {
        foreach (var card in Cards)
        {
            if (card.Id == exceptCardId)
                card.DeselectAllNotes(exceptNoteId);
            else
                card.DeselectAllNotes();
        }
    }

    public void CancelAllNoteAdding()
    {
        foreach (var card in Cards)
        {
            if (card.IsAddingNote)
            {
                card.NewNoteText = string.Empty;
                card.IsAddingNote = false;
            }
        }
    }


    // ── Stats ─────────────────────────────────────────────────
    public int TotalCards => _dataService.TotalCards;
    public int ActiveCards => _dataService.ActiveCards;
    public int DoneCards => _dataService.DoneCards;
}
