using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tarea.Models;
using Tarea.Services;

namespace Tarea.ViewModels;

public partial class CardViewModel : ObservableObject
{
    private readonly Card _card;
    private readonly DataService _dataService;
    private readonly Action _refreshParent;
    private readonly Func<string, CardViewModel?>? _findCardById;
    private readonly Func<string, bool>? _confirmAction;

    [ObservableProperty]
    private bool _isDatePickerOpen;

    [ObservableProperty]
    private ObservableCollection<CardNoteViewModel> _notes = new();

    [ObservableProperty]
    private bool _isAddingNote;

    [ObservableProperty]
    private string _newNoteText = string.Empty;

    [ObservableProperty]
    private bool _isHidden;

    [ObservableProperty]
    private bool _isHideAnimating;

    public Action<CardViewModel>? AnimateHideAction { get; set; }

    public string Id => _card.Id;
    public string Title => _card.Title;
    public CardStatus Status => _card.Status;
    public CardUrgency Urgency => _card.Urgency;
    public int Order => _card.Order;
    public bool HasDueDate => _card.DueDate.HasValue;
    public DateTime? CompletedAtRaw => _card.CompletedAt;
    public int CardWidth => _dataService.Settings.CardWidth;
    public int CardHeight => _dataService.Settings.CardHeight;

    public string DueDateDisplay => _card.DueDate.HasValue
        ? _card.DueDate.Value.ToString("MMM dd")
        : "no due date";

    public string DueDateBrushKey => IsPastDue ? "OrangeBrush"
        : IsDueDateWarning ? "YellowBrush"
        : "MutedBrush";

    public bool IsDueDateWarning
    {
        get
        {
            if (!_card.DueDate.HasValue)
                return false;
            var daysUntil = (_card.DueDate.Value - DateTime.Today).TotalDays;
            return daysUntil <= _dataService.Settings.DueDateWarningDays;
        }
    }

    public bool IsPastDue
    {
        get
        {
            if (!_card.DueDate.HasValue)
                return false;

            return _card.DueDate.Value.Date < DateTime.Today;
        }
    }

    public DateTime SelectedDate
    {
        get => _card.DueDate ?? DateTime.Today;
        set
        {
            _dataService.UpdateCard(_card.Id, dueDate: value);
            _card.DueDate = value;
            OnPropertyChanged(nameof(SelectedDate));
            OnPropertyChanged(nameof(DueDateDisplay));
            OnPropertyChanged(nameof(HasDueDate));
            OnPropertyChanged(nameof(IsDueDateWarning));
            OnPropertyChanged(nameof(DueDateBrushKey));
            OnPropertyChanged(nameof(IsPastDue));
            IsDatePickerOpen = false;
        }
    }

    public string CompletedAtDisplay => _card.CompletedAt.HasValue
        ? $"done {_card.CompletedAt.Value:MMM dd, h:mm tt}"
        : "";

    public string CompletedTooltip => _card.CompletedAt.HasValue
        ? $"{Title}\ndone {_card.CompletedAt.Value:MMM dd, h:mm tt}"
        : Title;

    public string UrgencyLabel => _card.Urgency switch
    {
        CardUrgency.Low => "LOW",
        CardUrgency.Medium => "MED",
        CardUrgency.High => "HIGH",
        _ => ""
    };

    public string StatusLabel => _card.Status switch
    {
        CardStatus.Todo => _dataService.Settings.TodoLabel,
        CardStatus.Wip => _dataService.Settings.WipLabel,
        CardStatus.Done => _dataService.Settings.DoneLabel,
        _ => _dataService.Settings.TodoLabel
    };

    public CardViewModel(Card card, DataService dataService, Action refreshParent,
        Func<string, bool>? confirmAction = null, Func<string, CardViewModel?>? findCardById = null)
    {
        _card = card;
        _dataService = dataService;
        _refreshParent = refreshParent;
        _confirmAction = confirmAction;
        _findCardById = findCardById;

        foreach (var note in card.Notes.OrderBy(n => n.Order))
        {
            Notes.Add(new CardNoteViewModel(note, dataService, card.Id, RefreshNotes));
        }
    }


    // ── Status Cycling ─────────────────────────────────────────
    [RelayCommand]
    private void CycleStatus()
    {
        var next = _card.Status switch
        {
            CardStatus.Todo => CardStatus.Wip,
            CardStatus.Wip => CardStatus.Done,
            CardStatus.Done => CardStatus.Todo,
            _ => CardStatus.Todo
        };

        DateTime? completedAt = next == CardStatus.Done ? DateTime.Now : null;

        _dataService.UpdateCard(_card.Id, status: next, completedAt: completedAt);
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(CompletedAtDisplay));
        OnPropertyChanged(nameof(CompletedTooltip));
        OnPropertyChanged(nameof(CompletedAtRaw));

        // If cycling away from Done, make sure it's visible again
        if (next != CardStatus.Done)
        {
            IsHidden = false;
        }

        _refreshParent();

        // Auto-hide on complete (after refresh so the new instance exists)
        if (next == CardStatus.Done && _dataService.Settings.HideOnComplete)
        {
            var cardId = _card.Id;
            var delaySeconds = _dataService.Settings.HideOnCompleteDelay;

            // Subtract animation duration so total time matches the setting
            int animDuration = 900; // pulse (600ms) + fade (300ms)
            int timerDelayMs = Math.Max(0, (delaySeconds * 1000) - animDuration);

            if (timerDelayMs <= 0)
            {
                var current = _findCardById?.Invoke(cardId);
                if (current != null)
                {
                    if (current.AnimateHideAction != null)
                        current.AnimateHideAction(current);
                    else
                        current.IsHidden = true;
                }
            }
            else
            {
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(timerDelayMs)
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    var current = _findCardById?.Invoke(cardId);
                    if (current != null && current.Status == CardStatus.Done)
                    {
                        if (current.AnimateHideAction != null)
                            current.AnimateHideAction(current);
                        else
                            current.IsHidden = true;
                    }
                };
                timer.Start();
            }
        }
    }

    [RelayCommand]
    private void CycleUrgency()
    {
        var next = _card.Urgency switch
        {
            CardUrgency.None => CardUrgency.Low,
            CardUrgency.Low => CardUrgency.Medium,
            CardUrgency.Medium => CardUrgency.High,
            CardUrgency.High => CardUrgency.None,
            _ => CardUrgency.None
        };

        _dataService.UpdateCard(_card.Id, urgency: next);
        OnPropertyChanged(nameof(Urgency));
        OnPropertyChanged(nameof(UrgencyLabel));
    }

    [RelayCommand]
    private void ToggleDatePicker()
    {
        IsDatePickerOpen = !IsDatePickerOpen;
    }

    [RelayCommand]
    private void ClearDueDate()
    {
        _dataService.UpdateCard(_card.Id, clearDueDate: true);
        _card.DueDate = null;
        OnPropertyChanged(nameof(DueDateDisplay));
        OnPropertyChanged(nameof(HasDueDate));
        OnPropertyChanged(nameof(IsDueDateWarning));
        OnPropertyChanged(nameof(IsPastDue));
        OnPropertyChanged(nameof(DueDateBrushKey));
        IsDatePickerOpen = false;
    }

    [RelayCommand]
    private void Delete()
    {
        if (_dataService.Settings.ConfirmOnCardDelete)
        {
            if (_confirmAction != null && !_confirmAction($"Delete this card?"))
                return;
        }

        _dataService.DeleteCard(_card.Id);
        _refreshParent();
    }


    // ── Notes ──────────────────────────────────────────────────
    [RelayCommand]
    private void ShowAddNote()
    {
        DeselectAllNotes();
        IsAddingNote = true;
    }

    [RelayCommand]
    private void AddNote()
    {
        var text = NewNoteText.Trim();
        if (string.IsNullOrEmpty(text))
        {
            IsAddingNote = false;
            return;
        }

        _dataService.AddNote(_card.Id, text);
        NewNoteText = string.Empty;
        IsAddingNote = false;
        RefreshNotes();
    }

    [RelayCommand]
    private void CancelAddNote()
    {
        NewNoteText = string.Empty;
        IsAddingNote = false;
    }

    private void RefreshNotes()
    {
        Notes.Clear();
        foreach (var note in _card.Notes.OrderBy(n => n.Order))
        {
            Notes.Add(new CardNoteViewModel(note, _dataService, _card.Id, RefreshNotes));
        }
    }

    public void ReorderNote(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex)
            return;

        Notes.Move(fromIndex, toIndex);

        var orderedIds = Notes.Select(n => n.Id).ToList();
        _dataService.ReorderNotes(_card.Id, orderedIds);
    }

    public void DeselectAllNotes(string? exceptId = null)
    {
        foreach (var note in Notes)
        {
            if (note.Id != exceptId)
                note.Deselect();
        }
    }

    public void AcceptNoteFromOutside(CardNoteViewModel sourceNote, int insertIndex)
    {
        var sourceCardVm = sourceNote.CardId;
        _dataService.MoveNote(sourceCardVm, _card.Id, sourceNote.Id, insertIndex);

        // Refresh both cards
        _refreshParent();
    }

    public void AcceptNoteAtEnd(CardNoteViewModel sourceNote)
    {
        var insertIndex = Notes.Count;
        _dataService.MoveNote(sourceNote.CardId, _card.Id, sourceNote.Id, insertIndex);
        _refreshParent();
    }
}
