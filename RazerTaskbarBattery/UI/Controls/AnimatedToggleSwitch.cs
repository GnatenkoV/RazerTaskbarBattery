using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RazerTaskbar.UI.Controls;

[TemplatePart(Name = ThumbPartName, Type = typeof(FrameworkElement))]
public sealed class AnimatedToggleSwitch : ToggleButton
{
    private const string ThumbPartName = "PART_Thumb";
    private FrameworkElement? _thumb;
    private TranslateTransform? _thumbTransform;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _thumb = GetTemplateChild(ThumbPartName) as FrameworkElement;
        _thumbTransform = null;

        if (_thumb is not null)
        {
            // Template resources may be sealed/frozen by WPF. Always assign an
            // instance owned by this control before changing or animating it.
            _thumbTransform = _thumb.RenderTransform is TranslateTransform transform
                ? transform.CloneCurrentValue()
                : new TranslateTransform();
            _thumb.RenderTransform = _thumbTransform;
        }

        ApplyThumbPosition(animate: false);
    }

    protected override void OnChecked(RoutedEventArgs eventArgs)
    {
        base.OnChecked(eventArgs);
        ApplyThumbPosition(animate: IsLoaded);
    }

    protected override void OnUnchecked(RoutedEventArgs eventArgs)
    {
        base.OnUnchecked(eventArgs);
        ApplyThumbPosition(animate: IsLoaded);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ApplyThumbPosition(animate: false);
    }

    private void ApplyThumbPosition(bool animate)
    {
        if (_thumb is null || _thumbTransform is null)
        {
            return;
        }

        double availableWidth = Math.Max(0.0, ActualWidth - _thumb.ActualWidth - 6.0);
        double target = IsChecked == true ? availableWidth : 0.0;

        if (!animate)
        {
            _thumbTransform.BeginAnimation(TranslateTransform.XProperty, null);
            _thumbTransform.X = target;
            return;
        }

        DoubleAnimation animation = new()
        {
            From = _thumbTransform.X,
            To = target,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new QuadraticEase
            {
                EasingMode = EasingMode.EaseOut
            },
            FillBehavior = FillBehavior.HoldEnd
        };

        _thumbTransform.BeginAnimation(TranslateTransform.XProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
}
