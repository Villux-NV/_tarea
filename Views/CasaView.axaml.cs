using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tarea.Helpers;
using Tarea.Services;
using Tarea.ViewModels;

namespace Tarea.Views;

public partial class CasaView : UserControl
{
    private bool _cardDragInitiated;
    private readonly DataService _dataService;

    private DragDropHelper? _dragHelper;

    public CasaView() : this(null!) { }

    public CasaView(DataService dataService)
    {
        _dataService = dataService;
        InitializeComponent();

        AddHandler(PointerPressedEvent, CasaGrid_PointerPressed,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);

        Loaded += OnLoaded;
    }


    // ── Card Pointer Events ─────────────────────────────────
    private void Card_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _cardDragInitiated = false;

        var source = e.Source as Visual;

        if (ViewHelpers.IsInsideTaggedArea(source, "QuickAddArea", "DescriptionArea"))
            return;

        // Double-click to open room
        if (e.ClickCount == 2)
        {
            if (sender is Control el && el.DataContext is RoomSummary room
                && DataContext is CasaViewModel vm)
            {
                vm.OpenRoomCommand.Execute(room.Id);
            }

            return;
        }

        if (ViewHelpers.IsInsideTaggedArea(source, "CardDragHandle"))
        {
            _cardDragInitiated = true;
            _dragHelper?.OnItemPointerPressed(sender!, e);
        }

        InitDragHelper();
    }

    private void Card_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_cardDragInitiated) 
            return;

        _dragHelper?.OnItemPointerMoved(sender!, e);
    }

    private void Card_PointerEntered(object? sender, PointerEventArgs e)
    {
        // Hover scale is handled declaratively via the card-hover style class.
        // This handler is kept as a hook point for Phase 5 drag state management.
    }

    private void Card_PointerExited(object? sender, PointerEventArgs e)
    {
        // Hover scale revert handled declaratively.
    }


    // ── Card Flip ──────────────────────────────────────────
    private void FlipCard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn)
            return;

        var (front, back) = ViewHelpers.FindCardFaces(btn);
        if (front == null || back == null)
            return;

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

        // Phase 1: collapse the current face
        AnimateScaleX(hideTransform, 1, 0, 150, () =>
        {
            // Phase 2: expand the other face
            AnimateScaleX(showTransform, 0, 1, 150);
        });
    }

    private static void AnimateScaleX(ScaleTransform transform, double from, double to, int durationMs,
        Action? onComplete = null)
    {
        int steps = Math.Max(1, durationMs / 16);
        double delta = (to - from) / steps;
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
                // Ease-in-out approximation
                double t = (double)currentStep / steps;
                double eased = t < 0.5
                    ? 2 * t * t
                    : 1 - Math.Pow(-2 * t + 2, 2) / 2;
                transform.ScaleX = from + (to - from) * eased;
            }
        };
        timer.Start();
    }

    // ── Card Drag ──────────────────────────────────────────
    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        InitDragHelper();
    }

    private void InitDragHelper()
    {
        if (DataContext is CasaViewModel vm && _dragHelper == null)
        {
            _dragHelper = new DragDropHelper(
                RoomCardsControl,
                "TareaRoom",
                (from, to) => vm.ReorderRoom(from, to)
            );
        }
    }

    // ── Marquee Title Scroll ──────────────────────────────
    private DispatcherTimer? _marqueeTimer;

    private void CardTitle_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Border border)
            return;

        var tb = ViewHelpers.FindVisualChild<TextBlock>(border);
        if (tb == null)
            return;

        // Measure the full text width
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

        // Unconstrain the TextBlock so it can render beyond the border
        tb.Width = desiredWidth;

        var tt = new TranslateTransform(0, 0);
        tb.RenderTransform = tt;

        // Animate the scroll
        double totalDuration = overflow * 30;
        double elapsed = 0;
        bool reversing = false;
        double startDelay = 400;

        _marqueeTimer?.Stop();
        _marqueeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _marqueeTimer.Tick += (s, ev) =>
        {
            if (startDelay > 0)
            {
                startDelay -= 16;
                return;
            }

            elapsed += 16;
            double progress = Math.Min(elapsed / totalDuration, 1.0);

            // Sine ease in-out
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

        if (sender is not Border border)
            return;

        var tb = ViewHelpers.FindVisualChild<TextBlock>(border);
        if (tb == null)
            return;

        tb.TextTrimming = TextTrimming.CharacterEllipsis;
        tb.Width = double.NaN;
        tb.RenderTransform = new TranslateTransform(0, 0);
    }


    // ── Quick Add ────────────────────────────────────────
    private void QuickAddInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not RoomSummary room)
            return;

        if (DataContext is not CasaViewModel vm)
            return;

        if (e.Key == Key.Enter)
        {
            vm.QuickAddCardCommand.Execute(room.Id);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelQuickAddCommand.Execute(room.Id);
            e.Handled = true;
        }
    }

    private void ShowQuickAdd_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control c && c.DataContext is RoomSummary room
            && DataContext is CasaViewModel vm)
        {
            vm.ShowQuickAddCommand.Execute(room.Id);
        }
    }

    private void DeleteRoom_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control c && c.DataContext is RoomSummary room
            && DataContext is CasaViewModel vm)
        {
            vm.DeleteRoomCommand.Execute(room.Id);
        }
    }


    // ── Description Edit ──────────────────────────────────
    private void Description_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control el && el.DataContext is RoomSummary room)
        {
            room.StartEditDescription();
            e.Handled = true;
        }
    }

    private void DescriptionEdit_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control c || c.DataContext is not RoomSummary room)
            return;

        if (e.Key == Key.Enter)
        {
            room.CommitDescriptionCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            room.CancelDescriptionCommand.Execute(null);
            e.Handled = true;
        }
    }


    // ── Add Room ──────────────────────────────────────────
    private void AddRoom_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is CasaViewModel vm)
        {
            vm.AddRoomCommand.Execute(null);
            e.Handled = true;
        }
    }


    // ── Focus-on-visible ──────────────────────────────────
    private void InputField_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox tb)
        {
            // Defer focus to after the layout pass
            Dispatcher.UIThread.Post(() =>
            {
                tb.Focus();
                tb.SelectAll();
            }, DispatcherPriority.Loaded);
        }
    }


    // ── Click-outside-to-cancel ─────────────────────────
    private void CasaGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var source = e.Source as Visual;

        if (ViewHelpers.IsInsideTaggedArea(source, "QuickAddArea", "DescriptionArea"))
            return;

        if (DataContext is CasaViewModel vm)
        {
            vm.CancelAllQuickAdds();
            vm.CancelAllDescriptionEdits();
        }
    }


    // ── Staggered Entrance ───────────────────────────────
    public void AnimateEntrance()
    {
        if (!_dataService.Settings.AnimationsEnabled)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            var panel = ViewHelpers.FindVisualChild<WrapPanel>(RoomCardsControl);
            if (panel == null) return;

            int i = 0;
            foreach (var child in panel.GetVisualChildren())
            {
                if (child is not Control c) continue;
                c.Opacity = 0;

                int delay = i * 80;
                int index = i;

                var fadeTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(delay)
                };
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

    private static void AnimateOpacity(Control target, double from, double to, int durationMs)
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
            }
            else
            {
                target.Opacity = from + (delta * currentStep);
            }
        };
        timer.Start();
    }


    // ── Public API for MainWindow ────────────────────────
    public void StartQuickAdd()
    {
        TxtAddRoom.Focus();
        TxtAddRoom.SelectAll();
    }
}