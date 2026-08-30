using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Tarea.Helpers;

public static class ViewHelpers
{
    /// <summary>
    /// Walk up the visual tree from a source element looking for a
    /// Control whose Tag matches one of the given tag names.
    /// </summary>
    public static bool IsInsideTaggedArea(object? source, params string[] tags)
    {
        var current = source as Visual;
        while (current != null)
        {
            if (current is Control c && c.Tag is string tag)
            {
                foreach (var t in tags)
                {
                    if (tag == t)
                        return true;
                }
            }
            current = current.GetVisualParent();
        }
        return false;
    }

    /// <summary>
    /// Walk up the visual tree from a start element, find the parent Grid
    /// that contains the two card face Borders (front + back) identified
    /// by having a ScaleTransform as their RenderTransform.
    /// </summary>
    public static (Border? front, Border? back) FindCardFaces(Visual? startElement)
    {
        var current = startElement;
        while (current != null)
        {
            current = current.GetVisualParent();

            if (current is not Grid grid)
                continue;

            Border? first = null;
            Border? second = null;

            foreach (var child in grid.GetVisualChildren())
            {
                if (child is Border b && b.RenderTransform is ScaleTransform)
                {
                    if (first == null) first = b;
                    else if (second == null) second = b;
                }
            }

            // Only return if we found both faces — otherwise keep walking up
            if (first != null && second != null)
                return (first, second);
        }
        return (null, null);
    }

    /// <summary>Find the first descendant of type T in the visual tree.</summary>
    public static T? FindVisualChild<T>(Visual parent) where T : Visual
    {
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is T found)
                return found;

            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }
        return null;
    }
}