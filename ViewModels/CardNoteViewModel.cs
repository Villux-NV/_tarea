using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tarea.Models;
using Tarea.Services;

namespace Tarea.ViewModels;

public partial class CardNoteViewModel : ObservableObject
{
    private readonly CardNote _note;
    private readonly DataService _dataService;
    private readonly string _cardId;
    private readonly Action _refreshParent;

    public string Id => _note.Id;
    public int Order => _note.Order;
    public string CardId => _cardId;

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private bool _isCrossedOut;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editText = string.Empty;

    public string NoteForegroundKey => IsCrossedOut ? "MutedBrush" : "RoseBrush";
    public bool ShowStrikethrough => IsCrossedOut;

    public CardNoteViewModel(CardNote note, DataService dataService, string cardId, Action refreshParent)
    {
        _note = note;
        _dataService = dataService;
        _cardId = cardId;
        _refreshParent = refreshParent;
        _text = note.Text;
        _isCrossedOut = note.IsCrossedOut;
    }

    public void ToggleCrossedOut()
    {
        _dataService.ToggleNoteCrossedOut(_cardId, _note.Id);
        IsCrossedOut = !IsCrossedOut;
        OnPropertyChanged(nameof(NoteForegroundKey));
        OnPropertyChanged(nameof(ShowStrikethrough));
    }

    public void Select()
    {
        IsSelected = true;
    }

    public void Deselect()
    {
        IsSelected = false;
        if (IsEditing)
            CancelEdit();
    }

    public void StartEdit()
    {
        EditText = Text;
        IsEditing = true;
        IsSelected = true;
    }

    [RelayCommand]
    private void CommitEdit()
    {
        var trimmed = EditText.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            Delete();
            return;
        }

        _dataService.UpdateNote(_cardId, _note.Id, trimmed);
        _note.Text = trimmed;
        Text = trimmed;
        IsEditing = false;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        EditText = Text;
        IsEditing = false;
    }

    [RelayCommand]
    private void Delete()
    {
        _dataService.DeleteNote(_cardId, _note.Id);
        _refreshParent();
    }
}
