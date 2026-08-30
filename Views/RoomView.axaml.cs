using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Linq;
using System.Collections.Generic;
using Tarea.Helpers;
using Tarea.Models;
using Tarea.Services;
using Tarea.ViewModels;
using static System.Net.Mime.MediaTypeNames;

namespace Tarea.Views;

public partial class RoomView : UserControl
{
    private DispatcherTimer? _longPressTimer;
    private CardNoteViewModel? _longPressTarget;
    private DragDropHelper? _dragHelper;
    private bool _cardDragInitiated;
    private bool _longPressFired;
    private bool _isNoteDragging;

    private readonly DataService _dataService;

    public RoomView() : this(null!) { }

    public RoomView(DataService dataService)
    {
        _dataService = dataService;
        InitializeComponent();

        // Tunneling handler for click-outside-to-deselect
        AddHandler(PointerPressedEvent, RoomGrid_PointerPressed,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // Watch for stat changes to update ratio bar
        DataContextChanged += (s, e) =>
        {
            _dragHelper = null; // reset on room change
            if (DataContext is RoomViewModel vm)
            {
                _dragHelper = new DragDropHelper(
                    CardItemsControl,
                    "TareaCard",
                    (from, to) => vm.ReorderCard(from, to)
                );

                vm.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName is nameof(vm.TodoCardCount)
                        or nameof(vm.WipCardCount)
                        or nameof(vm.DoneCardCount))
                    {
                        UpdateRatioBar(vm);
                    }
                };
                UpdateRatioBar(vm);
            }
        };

        AddHandler(DragDrop.DropEvent, OnNoteDrop);
        AddHandler(DragDrop.DragOverEvent, OnNoteDragOver);
    }


    // ── Card Events ─────────────────────────────────────
    private void Card_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _cardDragInitiated = false;
        var source = e.Source as Visual;

        if (ViewHelpers.IsInsideTaggedArea(source, "NoteRow", "NoteAddArea", "NoteDropZone"))
            return;

        if (ViewHelpers.IsInsideTaggedArea(source, "CardDragHandle"))
        {
            _cardDragInitiated = true;
            _dragHelper?.OnItemPointerPressed(sender!, e);
        }
    }

    private void Card_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_cardDragInitiated) 
            return;

        _dragHelper?.OnItemPointerMoved(sender!, e);
    }


    // ── Card Flip ──────────────────────────────────────
    private void FlipCard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        var (front, back) = ViewHelpers.FindCardFaces(btn);
        if (front == null || back == null) return;

        var isShowingFront = (btn.Tag as string) == "front";
        AnimateFlip(isShowingFront ? front : back, isShowingFront ? back : front);
    }

    private void AnimateFlip(Border hiding, Border showing)
    {
        var hideTransform = new ScaleTransform(1, 1);
        hiding.RenderTransform = hideTransform;
        hiding.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        var showTransform = new ScaleTransform(0, 1);
        showing.RenderTransform = showTransform;
        showing.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        AnimateScaleX(hideTransform, 1, 0, 150, () =>
        {
            AnimateScaleX(showTransform, 0, 1, 150);
        });
    }

    private static void AnimateScaleX(ScaleTransform transform, double from, double to, int durationMs,
        Action? onComplete = null)
    {
        int steps = Math.Max(1, durationMs / 16);
        int currentStep = 0;
        transform.ScaleX = from;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (s, e) =>
        {
            currentStep++;
            if (currentStep >= steps)
            {
                timer.Stop();
                transform.ScaleX = to;
                onComplete?.Invoke();
            }
            else
            {
                double t = (double)currentStep / steps;
                double eased = t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
                transform.ScaleX = from + (to - from) * eased;
            }
        };
        timer.Start();
    }


    // ── Status Badge Click ──────────────────────────────
    private void StatusBadge_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not CardViewModel cardVm)
            return;

        if (cardVm.Status != CardStatus.Done) return;
        if (_dataService.Settings.HideOnComplete) return;

        // Pulse the card border green → rose
        var cardBorder = FindCardBorder(btn);
        if (cardBorder != null)
            PulseDoneCard(cardBorder);
    }


    // ── Completed Icons ─────────────────────────────────
    private void CompletedIcon_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control el && el.DataContext is CardViewModel cardVm)
        {
            if (cardVm.IsHidden)
            {
                cardVm.IsHidden = false;
                if (DataContext is RoomViewModel vm)
                {
                    var currentIndex = vm.Cards.IndexOf(cardVm);
                    if (currentIndex > 0)
                        vm.Cards.Move(currentIndex, 0);
                }
            }
            else
            {
                cardVm.IsHidden = true;
            }
        }
        e.Handled = true;
    }


    // ── Marquee Title Scroll ─────────────────────────────
    private DispatcherTimer? _marqueeTimer;

    private void CardTitle_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Border border) return;
        var tb = ViewHelpers.FindVisualChild<TextBlock>(border);
        if (tb == null) return;

        tb.TextTrimming = TextTrimming.None;
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desiredWidth = tb.DesiredSize.Width;
        var availableWidth = border.Bounds.Width;

        if (desiredWidth <= availableWidth)
        {
            tb.TextTrimming = TextTrimming.CharacterEllipsis;
            return;
        }

        var overflow = desiredWidth - availableWidth + 20;
        tb.Width = desiredWidth;

        var tt = new TranslateTransform(0, 0);
        tb.RenderTransform = tt;

        double totalDuration = overflow * 30;
        double elapsed = 0;
        bool reversing = false;
        double startDelay = 400;

        _marqueeTimer?.Stop();
        _marqueeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _marqueeTimer.Tick += (s, ev) =>
        {
            if (startDelay > 0) { startDelay -= 16; return; }
            elapsed += 16;
            double progress = Math.Min(elapsed / totalDuration, 1.0);
            double eased = 0.5 * (1 - Math.Cos(progress * Math.PI));

            if (!reversing)
            {
                tt.X = -overflow * eased;
                if (progress >= 1.0) { elapsed = 0; reversing = true; }
            }
            else
            {
                tt.X = -overflow * (1 - eased);
                if (progress >= 1.0) { elapsed = 0; reversing = false; startDelay = 300; }
            }
        };
        _marqueeTimer.Start();
    }

    private void CardTitle_PointerExited(object? sender, PointerEventArgs e)
    {
        _marqueeTimer?.Stop();
        _marqueeTimer = null;
        if (sender is not Border border) return;
        var tb = ViewHelpers.FindVisualChild<TextBlock>(border);
        if (tb == null) return;
        tb.TextTrimming = TextTrimming.CharacterEllipsis;
        tb.Width = double.NaN;
        tb.RenderTransform = new TranslateTransform(0, 0);
    }


    // ── Notes: select, edit, long-press crossout ────────
    private void Note_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control el || el.DataContext is not CardNoteViewModel noteVm)
            return;

        var source = e.Source as Visual;

        // Don't interfere with drag handle or delete button
        if (source is Control c && (c.Cursor?.ToString() == "SizeAll" || c is Button))
            return;
        if (ViewHelpers.IsInsideTaggedArea(source, "NoteDragHandle"))
            return;

        if (e.ClickCount == 2)
        {
            Note_DoubleClick(el, noteVm);
            e.Handled = true;
            return;
        }

        _longPressFired = false;
        _longPressTarget = noteVm;
        _isNoteDragging = false;

        _longPressTimer?.Stop();
        _longPressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _longPressTimer.Tick += (s, args) =>
        {
            _longPressTimer?.Stop();
            _longPressFired = true;
            _longPressTarget?.ToggleCrossedOut();
            _longPressTarget = null;
        };
        _longPressTimer.Start();
        e.Handled = true;
    }

    private void Note_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _longPressTimer?.Stop();
        _longPressTimer = null;

        if (_isNoteDragging)
        {
            _isNoteDragging = false;
            _longPressTarget = null;
            return;
        }

        if (_longPressFired)
        {
            _longPressTarget = null;
            return;
        }

        // Single click → toggle selection
        if (_longPressTarget != null && sender is Control el)
        {
            var noteVm = _longPressTarget;
            _longPressTarget = null;

            if (DataContext is RoomViewModel roomVm)
            {
                if (noteVm.IsSelected)
                {
                    noteVm.Deselect();
                }
                else
                {
                    var cardVm = FindCardViewModel(el);
                    roomVm.DeselectAllNotes(cardVm?.Id, noteVm.Id);
                    noteVm.Select();
                }
            }
        }
    }

    private void Note_DoubleClick(Control el, CardNoteViewModel noteVm)
    {
        _longPressTimer?.Stop();
        _longPressTimer = null;
        _longPressTarget = null;

        if (DataContext is RoomViewModel roomVm)
        {
            var cardVm = FindCardViewModel(el);
            roomVm.DeselectAllNotes(cardVm?.Id, noteVm.Id);
        }
        noteVm.StartEdit();
    }

    private async void NoteDragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control handle || handle.DataContext is not CardNoteViewModel noteVm)
            return;

        _longPressTimer?.Stop();
        _longPressTimer = null;
        _isNoteDragging = true;

        var item = new DataTransferItem();
        item.Set(DataFormat.Text, "TareaNote:" + noteVm.Id + "|" + noteVm.CardId);
        var data = new DataTransfer();
        data.Add(item);

        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);

        _isNoteDragging = false;
        e.Handled = true;
    }

    private void AddNoteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control c && c.DataContext is CardViewModel cardVm)
        {
            cardVm.ShowAddNoteCommand.Execute(null);
        }
    }

    // ── Note Drag-Drop ──────────────────────────────────
    private void OnNoteDragOver(object? sender, DragEventArgs e)
    {
        var text = e.DataTransfer.TryGetText();
        // Only handle note drags — don't interfere with card drags
        if (text != null && text.StartsWith("TareaNote:"))
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private void OnNoteDrop(object? sender, DragEventArgs e)
    {
        var text = e.DataTransfer.TryGetText();
        // Only handle note drags — let card drops pass through to DragDropHelper
        if (text == null || !text.StartsWith("TareaNote:"))
            return;

        var payload = text.Substring("TareaNote:".Length);

        var parts = payload.Split('|');
        if (parts.Length != 2)
            return;

        var draggedNoteId = parts[0];
        var draggedFromCardId = parts[1];

        if (DataContext is not RoomViewModel roomVm)
            return;

        var sourceCard = roomVm.Cards.FirstOrDefault(c => c.Id == draggedFromCardId);
        var draggedNote = sourceCard?.Notes.FirstOrDefault(n => n.Id == draggedNoteId);
        if (sourceCard == null || draggedNote == null)
            return;

        var source = e.Source as Visual;

        CardNoteViewModel? targetNote = null;
        CardViewModel? targetCard = null;

        var current = source;
        while (current != null)
        {
            if (current is Control c)
            {
                if (targetNote == null && c.DataContext is CardNoteViewModel noteVm && c.Tag is string t && t == "NoteRow")
                    targetNote = noteVm;
                if (targetCard == null && c.DataContext is CardViewModel cardVm)
                    targetCard = cardVm;
            }
            if (targetCard != null) break;
            current = current.GetVisualParent();
        }

        if (targetCard == null) return;
        if (draggedNote.Id == targetNote?.Id) return;

        if (draggedFromCardId == targetCard.Id)
        {
            if (targetNote != null)
            {
                int fromIndex = -1, toIndex = -1;
                for (int i = 0; i < targetCard.Notes.Count; i++)
                {
                    if (targetCard.Notes[i].Id == draggedNoteId) fromIndex = i;
                    if (targetCard.Notes[i].Id == targetNote.Id) toIndex = i;
                }
                if (fromIndex >= 0 && toIndex >= 0)
                    targetCard.ReorderNote(fromIndex, toIndex);
            }
        }
        else
        {
            if (targetNote != null)
            {
                int toIndex = 0;
                for (int i = 0; i < targetCard.Notes.Count; i++)
                {
                    if (targetCard.Notes[i].Id == targetNote.Id) { toIndex = i; break; }
                }
                targetCard.AcceptNoteFromOutside(draggedNote, toIndex);
            }
            else
            {
                targetCard.AcceptNoteAtEnd(draggedNote);
            }
        }

        e.Handled = true;
    }

    // ── Note Editing ─────────────────────────────────────
    private void NoteEdit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control c || c.DataContext is not CardNoteViewModel noteVm) return;
        if (e.Key == Key.Enter) { noteVm.CommitEditCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Escape) { noteVm.CancelEditCommand.Execute(null); e.Handled = true; }
    }

    private void NoteAdd_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control c || c.DataContext is not CardViewModel cardVm) return;
        if (e.Key == Key.Enter) { cardVm.AddNoteCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Escape) { cardVm.CancelAddNoteCommand.Execute(null); e.Handled = true; }
    }


    // ── Add Card ─────────────────────────────────────────
    private void AddCard_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is RoomViewModel vm)
        {
            vm.AddCardCommand.Execute(null);
            e.Handled = true;
        }
    }


    // ── Focus-on-visible ─────────────────────────────────
    private void InputField_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox tb)
        {
            Dispatcher.UIThread.Post(() =>
            {
                tb.Focus();
                tb.SelectAll();
            }, DispatcherPriority.Loaded);
        }
    }


    // ── Click-outside-to-deselect ────────────────────────
    private void RoomGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var source = e.Source as Visual;

        if (ViewHelpers.IsInsideTaggedArea(source, "NoteRow", "NoteAddArea", "NoteDropZone"))
            return;

        if (DataContext is RoomViewModel roomVm)
        {
            roomVm.DeselectAllNotes();
            roomVm.CancelAllNoteAdding();
        }
    }


    // ── Animations ──────────────────────────────────────
    public void AnimateEntrance()
    {
        if (!_dataService.Settings.AnimationsEnabled) return;

        Dispatcher.UIThread.Post(() =>
        {
            var panel = ViewHelpers.FindVisualChild<WrapPanel>(CardItemsControl);
            if (panel == null) return;

            int i = 0;
            foreach (var child in panel.GetVisualChildren())
            {
                if (child is not Control c) continue;
                c.Opacity = 0;
                int delay = i * 80;

                var fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
                fadeTimer.Tick += (s, e) =>
                {
                    fadeTimer.Stop();
                    AnimateOpacity(c, 0, 1, 200);
                };
                fadeTimer.Start();
                i++;
            }
        }, DispatcherPriority.Loaded);
    }

    public void AnimateCardHide(CardViewModel card)
    {
        if (!_dataService.Settings.AnimationsEnabled || !_dataService.Settings.AnimSmoothHide)
        {
            card.IsHidden = true;
            return;
        }

        var container = FindCardContainer(card);
        if (container == null) { card.IsHidden = true; return; }

        AnimateOpacity(container, 1, 0, 300, () =>
        {
            card.IsHidden = true;
            container.Opacity = 1;
        });
    }

    public void PulseAndHideCard(CardViewModel card)
    {
        var container = FindCardContainer(card);
        if (container == null) { card.IsHidden = true; return; }

        bool doPulse = _dataService.Settings.AnimationsEnabled && _dataService.Settings.AnimDonePulse;
        bool doFade = _dataService.Settings.AnimationsEnabled && _dataService.Settings.AnimSmoothHide;

        if (!doPulse && !doFade) { card.IsHidden = true; return; }

        var cardBorder = FindCardBorderInContainer(container);

        Action doHide = () =>
        {
            if (doFade)
                AnimateOpacity(container, 1, 0, 300, () => { card.IsHidden = true; container.Opacity = 1; });
            else
                card.IsHidden = true;
        };

        if (doPulse && cardBorder != null)
        {
            PulseDoneCard(cardBorder, doHide);
        }
        else
        {
            doHide();
        }
    }

    private void PulseDoneCard(Border cardBorder, Action? onComplete = null)
    {
        if (!_dataService.Settings.AnimationsEnabled || !_dataService.Settings.AnimDonePulse)
        {
            onComplete?.Invoke();
            return;
        }

        var green = GetBrushColor("GreenBrush");
        var rose = GetBrushColor("RoseBrush");

        var flashBrush = new SolidColorBrush(green);
        var originalBrush = cardBorder.BorderBrush;
        cardBorder.BorderBrush = flashBrush;

        // Animate color from green → rose
        int steps = 600 / 16;
        int currentStep = 0;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (s, e) =>
        {
            currentStep++;
            if (currentStep >= steps)
            {
                timer.Stop();
                cardBorder.BorderBrush = originalBrush;
                onComplete?.Invoke();
            }
            else
            {
                double t = (double)currentStep / steps;
                double eased = 1 - Math.Pow(1 - t, 2); // ease-out

                byte r = (byte)(green.R + (rose.R - green.R) * eased);
                byte g = (byte)(green.G + (rose.G - green.G) * eased);
                byte b = (byte)(green.B + (rose.B - green.B) * eased);

                flashBrush.Color = new Color(255, r, g, b);
            }
        };
        timer.Start();
    }


    // ── Ratio Bar ────────────────────────────────────────
    private void UpdateRatioBar(RoomViewModel vm)
    {
        RatioBarText.Inlines?.Clear();

        var muted = new SolidColorBrush(GetBrushColor("MutedBrush"));
        var green = new SolidColorBrush(GetBrushColor("GreenBrush"));
        var orange = new SolidColorBrush(GetBrushColor("OrangeBrush"));
        var yellow = new SolidColorBrush(GetBrushColor("YellowBrush"));

        const int barWidth = 48;
        var total = vm.DoneCardCount + vm.WipCardCount + vm.TodoCardCount;

        RatioBarText.Inlines!.Add(new Run("[") { Foreground = muted });

        if (total == 0)
        {
            RatioBarText.Inlines.Add(new Run(new string('·', barWidth)) { Foreground = muted });
        }
        else
        {
            var sections = new List<(int count, ISolidColorBrush brush)>();
            if (vm.DoneCardCount > 0) sections.Add((vm.DoneCardCount, green));
            if (vm.WipCardCount > 0) sections.Add((vm.WipCardCount, orange));
            if (vm.TodoCardCount > 0) sections.Add((vm.TodoCardCount, yellow));

            int separatorCount = sections.Count - 1;
            int dashSpace = barWidth - separatorCount;

            var dashCounts = new int[sections.Count];
            int assigned = 0;

            for (int i = 0; i < sections.Count; i++)
            {
                dashCounts[i] = Math.Max(1, (int)Math.Round((double)sections[i].count / total * dashSpace));
                assigned += dashCounts[i];
            }

            int drift = assigned - dashSpace;
            if (drift != 0)
            {
                int largestIdx = 0;
                for (int i = 1; i < dashCounts.Length; i++)
                    if (dashCounts[i] > dashCounts[largestIdx]) largestIdx = i;
                dashCounts[largestIdx] -= drift;
            }

            for (int i = 0; i < sections.Count; i++)
            {
                if (i > 0)
                    RatioBarText.Inlines.Add(new Run("│") { Foreground = muted });
                RatioBarText.Inlines.Add(new Run(new string('─', dashCounts[i]))
                { Foreground = sections[i].brush });
            }
        }

        RatioBarText.Inlines.Add(new Run("]") { Foreground = muted });
    }


    // ── Helpers ──────────────────────────────────────────
    private static void AnimateOpacity(Control target, double from, double to, int durationMs,
        Action? onComplete = null)
    {
        target.Opacity = from;
        int steps = Math.Max(1, durationMs / 16);
        double delta = (to - from) / steps;
        int currentStep = 0;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (s, e) =>
        {
            currentStep++;
            if (currentStep >= steps)
            {
                timer.Stop();
                target.Opacity = to;
                onComplete?.Invoke();
            }
            else
            {
                target.Opacity = from + (delta * currentStep);
            }
        };
        timer.Start();
    }

    private static Color GetBrushColor(string key)
    {
        if (Avalonia.Application.Current!.Resources.TryGetResource(key, null, out var res)
            && res is ISolidColorBrush brush)
            return brush.Color;
        return Colors.Gray;
    }

    private CardViewModel? FindCardViewModel(Visual element)
    {
        var current = element as Visual;
        while (current != null)
        {
            if (current is Control c && c.DataContext is CardViewModel cvm)
                return cvm;
            current = current.GetVisualParent();
        }
        return null;
    }

    private Border? FindCardBorder(Visual startElement)
    {
        var current = startElement as Visual;
        while (current != null)
        {
            if (current is Border b && b.BorderThickness.Left >= 1 && b.Bounds.Width > 100)
                return b;
            current = current.GetVisualParent();
        }
        return null;
    }

    private Border? FindCardBorderInContainer(Control container)
    {
        Border? result = null;
        WalkVisualTree(container, element =>
        {
            if (element is Border b && b.BorderThickness.Left >= 1 && b.Bounds.Width > 100)
            {
                result = b;
                return true;
            }
            return false;
        });
        return result;
    }

    private Control? FindCardContainer(CardViewModel card)
    {
        var panel = ViewHelpers.FindVisualChild<WrapPanel>(CardItemsControl);
        if (panel == null) return null;

        foreach (var child in panel.GetVisualChildren())
        {
            if (child is Control c && c.DataContext == card)
                return c;
        }
        return null;
    }

    private static void WalkVisualTree(Visual parent, Func<Visual, bool> callback)
    {
        foreach (var child in parent.GetVisualChildren())
        {
            if (callback(child)) return;
            WalkVisualTree(child, callback);
        }
    }

    public void StartQuickAdd()
    {
        TxtAddCard.Focus();
        TxtAddCard.SelectAll();
    }
}