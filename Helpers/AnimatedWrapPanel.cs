using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Tarea.Helpers
{
    public class AnimatedWrapPanel : WrapPanel
    {
        private readonly Dictionary<Control, Point> _childPositions = new();
        private bool _hasArrangedOnce;
        private int _lastVisibleChildCount;

        // ── Styled Properties ──────────────────────────────

        public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
            AvaloniaProperty.Register<AnimatedWrapPanel, bool>(nameof(IsAnimationEnabled), true);

        public bool IsAnimationEnabled
        {
            get => GetValue(IsAnimationEnabledProperty);
            set => SetValue(IsAnimationEnabledProperty, value);
        }

        public static readonly StyledProperty<int> AnimationMillisecondsProperty =
            AvaloniaProperty.Register<AnimatedWrapPanel, int>(nameof(AnimationMilliseconds), 200);

        public int AnimationMilliseconds
        {
            get => GetValue(AnimationMillisecondsProperty);
            set => SetValue(AnimationMillisecondsProperty, value);
        }

        // ── Layout ─────────────────────────────────────────

        protected override Size ArrangeOverride(Size finalSize)
        {
            double x = 0, y = 0, rowHeight = 0;

            // Count visible children BEFORE arranging
            int visibleCount = 0;
            foreach (var child in Children)
            {
                if (child.IsVisible)
                {
                    var desired = child.DesiredSize;
                    if (desired.Width >= 1 || desired.Height >= 1)
                        visibleCount++;
                }
            }

            // If child count changed, this is a bulk add/remove (room navigation,
            // card hide, card add) — skip animation this pass, just record positions
            bool suppressThisPass = !_hasArrangedOnce
                || visibleCount != _lastVisibleChildCount;

            foreach (var child in Children)
            {
                if (!child.IsVisible)
                    continue;

                var desired = child.DesiredSize;

                if (desired.Width < 1 && desired.Height < 1)
                {
                    child.Arrange(new Rect(0, 0, 0, 0));
                    _childPositions.Remove(child);
                    continue;
                }

                // Wrap to next row
                if (x > 0 && x + desired.Width > finalSize.Width)
                {
                    x = 0;
                    y += rowHeight;
                    rowHeight = 0;
                }

                child.Arrange(new Rect(x, y, desired.Width, desired.Height));

                var newPos = new Point(x, y);

                // Only animate when this is a reorder (same child count)
                if (!suppressThisPass && IsAnimationEnabled
                    && _childPositions.TryGetValue(child, out var oldPos))
                {
                    var dx = oldPos.X - newPos.X;
                    var dy = oldPos.Y - newPos.Y;

                    if (Math.Abs(dx) > 0.5 || Math.Abs(dy) > 0.5)
                    {
                        SlideChild(child, dx, dy);
                    }
                }

                _childPositions[child] = newPos;

                x += desired.Width;
                rowHeight = Math.Max(rowHeight, desired.Height);
            }

            // Clean up entries for children that were removed
            var liveChildren = new HashSet<Control>(Children);
            foreach (var key in _childPositions.Keys.Where(k => !liveChildren.Contains(k)).ToList())
                _childPositions.Remove(key);

            _lastVisibleChildCount = visibleCount;
            _hasArrangedOnce = true;

            return finalSize;
        }

        // ── Animation ──────────────────────────────────────

        private void SlideChild(Control child, double fromDx, double fromDy)
        {
            TranslateTransform tt;

            if (child.RenderTransform is TranslateTransform existing)
            {
                // Read current animated offset BEFORE resetting — prevents jump
                double currentX = existing.X;
                double currentY = existing.Y;

                // Combine: remaining animation offset + new layout shift
                fromDx += currentX;
                fromDy += currentY;

                tt = existing;
            }
            else
            {
                tt = new TranslateTransform();
                child.RenderTransform = tt;
            }

            tt.X = fromDx;
            tt.Y = fromDy;

            var startX = fromDx;
            var startY = fromDy;
            var startTime = DateTime.UtcNow;
            var durationMs = (double)AnimationMilliseconds;

            // ~60fps timer-based lerp with quadratic ease-out
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) =>
            {
                var t = Math.Min(1.0, (DateTime.UtcNow - startTime).TotalMilliseconds / durationMs);

                // Quadratic ease-out: t * (2 - t)
                var eased = t * (2.0 - t);

                tt.X = startX * (1.0 - eased);
                tt.Y = startY * (1.0 - eased);

                if (t >= 1.0)
                {
                    tt.X = 0;
                    tt.Y = 0;
                    timer.Stop();
                }
            };
            timer.Start();
        }
    }
}
