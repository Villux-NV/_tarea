using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Tarea.Helpers;

public class DragDropHelper
{
    private readonly ItemsControl _container;
    private readonly string _formatName;
    private readonly Action<int, int> _onReorder;

    private Point _dragStartPoint;
    private PointerPressedEventArgs? _pressedArgs;
    private bool _isDragging;
    private int _dragIndex = -1;
    private int _currentPreviewIndex = -1;
    private Control? _dragElement;
    private DateTime _lastReorderTime = DateTime.MinValue;

    private const double DragThreshold = 10.0;
    private const int ReorderCooldownMs = 250;

    public DragDropHelper(ItemsControl container, string formatName, Action<int, int> onReorder)
    {
        _container = container;
        _formatName = formatName;
        _onReorder = onReorder;

        DragDrop.SetAllowDrop(_container, true);
        _container.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        _container.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    public void OnItemPointerPressed(object sender, PointerPressedEventArgs e)
    {
        _dragStartPoint = e.GetPosition(_container);
        _isDragging = false;
        _pressedArgs = e;
    }

    public async void OnItemPointerMoved(object sender, PointerEventArgs e)
    {
        if (_pressedArgs == null)
            return;

        if (!e.GetCurrentPoint(_container).Properties.IsLeftButtonPressed)
            return;

        var currentPos = e.GetPosition(_container);
        var diff = currentPos - _dragStartPoint;

        if (Math.Abs(diff.X) < DragThreshold && Math.Abs(diff.Y) < DragThreshold)
            return;

        if (_isDragging)
            return;

        var border = FindParentCardRoot(sender as Visual);
        if (border == null)
            return;

        _dragIndex = GetItemIndex(border);
        if (_dragIndex < 0)
            return;

        _isDragging = true;
        _dragElement = border;
        _currentPreviewIndex = _dragIndex;

        _dragElement.Opacity = 0.15;

        var item = new DataTransferItem();
        item.Set(DataFormat.Text, _formatName + ":" + _dragIndex);
        var data = new DataTransfer();
        data.Add(item);

        await DragDrop.DoDragDropAsync(_pressedArgs, data, DragDropEffects.Move);

        if (_dragElement != null)
            _dragElement.Opacity = 1.0;

        if (_currentPreviewIndex != _dragIndex && _currentPreviewIndex >= 0)
            _onReorder(_currentPreviewIndex, _dragIndex);

        _isDragging = false;
        _dragElement = null;
        _dragIndex = -1;
        _currentPreviewIndex = -1;
        _pressedArgs = null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!IsOurDrag(e))
        {
            // Don't set None — let other handlers decide
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;  // Stop bubbling so note handlers don't interfere

        var pos = e.GetPosition(_container);
        var targetBorder = FindCardRootAtPoint(pos);

        if (targetBorder == null || targetBorder == _dragElement)
            return;

        var targetIndex = GetItemIndex(targetBorder);
        if (targetIndex < 0 || targetIndex == _currentPreviewIndex)
            return;

        if ((DateTime.UtcNow - _lastReorderTime).TotalMilliseconds < ReorderCooldownMs)
            return;

        _onReorder(_currentPreviewIndex, targetIndex);
        _lastReorderTime = DateTime.UtcNow;
        _currentPreviewIndex = targetIndex;

        _container.UpdateLayout();
        var newElement = FindCardRootForIndex(targetIndex);
        if (newElement != null)
        {
            _dragElement = newElement;
            _dragElement.Opacity = 0.15;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!IsOurDrag(e))
            return;

        _dragIndex = _currentPreviewIndex;

        if (_dragElement != null)
            _dragElement.Opacity = 1.0;

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;  // Accept the drop so DoDragDropAsync returns Move, not None
    }

    private bool IsOurDrag(DragEventArgs e)
    {
        var text = e.DataTransfer.TryGetText();
        return text != null && text.StartsWith(_formatName + ":");
    }

    
    // ── Visual tree helpers ──────────────────────────────
    private Control? FindParentCardRoot(Visual? element)
    {
        var current = element;
        while (current != null)
        {
            if (current is Control c && c.Tag is string tag && tag == "CardRoot")
                return c;
            current = current.GetVisualParent();
        }
        return null;
    }

    private Control? FindCardRootAtPoint(Point posInContainer)
    {
        var panel = GetItemsPanel();
        if (panel == null) return null;

        foreach (var child in panel.GetVisualChildren())
        {
            if (child is not Visual v) continue;
            var posInChild = _container.TranslatePoint(posInContainer, v);
            if (!posInChild.HasValue) continue;
            if (!new Rect(v.Bounds.Size).Contains(posInChild.Value)) continue;
            return FindCardRootInVisual(v);
        }
        return null;
    }

    private Control? FindCardRootInVisual(Visual parent)
    {
        if (parent is Control c && c.Tag is string tag && tag == "CardRoot")
            return c;
        foreach (var child in parent.GetVisualChildren())
        {
            var result = FindCardRootInVisual(child);
            if (result != null) return result;
        }
        return null;
    }

    private Control? FindCardRootForIndex(int index)
    {
        var container = _container.ContainerFromIndex(index);
        if (container == null) return null;
        return FindCardRootInVisual(container);
    }

    private int GetItemIndex(Visual element)
    {
        var panel = GetItemsPanel();
        if (panel == null) return -1;

        var current = element as Visual;
        while (current != null)
        {
            var parent = current.GetVisualParent();
            if (parent == panel)
            {
                int i = 0;
                foreach (var child in panel.GetVisualChildren())
                {
                    if (child == current) return i;
                    i++;
                }
                return -1;
            }
            current = parent;
        }
        return -1;
    }

    private Panel? GetItemsPanel()
    {
        return ViewHelpers.FindVisualChild<Panel>(_container);
    }
}