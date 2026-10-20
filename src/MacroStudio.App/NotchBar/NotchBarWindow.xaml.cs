using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MacroStudio.App.Services;
using MacroStudio.Core.Geometry;
using MacroStudio.Core.Settings;
using MacroStudio.Native.Displays;
using MacroStudio.Native.Windowing;
using Microsoft.Win32;

namespace MacroStudio.App.NotchBar;

/// <summary>
/// The bar on the top edge of the screen. It never takes the keyboard focus, opens when the pointer rests on it
/// (or stays open while pinned), and hides during full-screen apps and while the user drags a window
/// (Windows 11 shows Snap Layouts there).
/// </summary>
public partial class NotchBarWindow : Window
{
    // Sizes in device-independent pixels (1/96 inch). The window always has the expanded size;
    // its transparent parts let clicks through to the windows below.
    private const double ExpandedWidth = 640;
    private const double ExpandedHeight = 48;
    private const double CollapsedWidth = 160;
    private const double CollapsedHeight = 6;
    private const double HoverMargin = 40;

    private static readonly TimeSpan HoverCheckInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan FullscreenCheckInterval = TimeSpan.FromMilliseconds(500);
    private static readonly Duration AnimationDuration = new(TimeSpan.FromMilliseconds(150));

    private readonly NotchBarViewModel _viewModel;
    private readonly SettingsService _settings;
    private readonly HoverTracker _hover = new();
    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _fullscreenTimer;
    private readonly WindowDragWatcher _dragWatcher = new();

    private nint _hwnd;
    private MonitorInfo? _monitor;
    private bool _isFullscreenAppActive;
    private bool _isWindowDragActive;
    private bool _wasBusy;

    public NotchBarWindow(NotchBarViewModel viewModel, SettingsService settings)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _settings = settings;
        ContextMenu = (ContextMenu)FindResource("AppContextMenu");

        _hoverTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = HoverCheckInterval };
        _hoverTimer.Tick += OnHoverCheck;
        _fullscreenTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = FullscreenCheckInterval };
        _fullscreenTimer.Tick += OnFullscreenCheck;
    }

    public void Start()
    {
        _hover.OpenDelay = TimeSpan.FromMilliseconds(_settings.Current.NotchBarOpenDelayMs);
        UpdatePinning();
        _settings.Changed += OnSettingsChanged;
        _viewModel.PropertyChanged += OnViewModelChanged;
        _dragWatcher.DragStarted += (_, _) => SetWindowDragActive(true);
        _dragWatcher.DragEnded += (_, _) => SetWindowDragActive(false);
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _fullscreenTimer.Start();
        UpdateVisibility();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowInterop.MakeNonActivatingToolWindow(_hwnd);
        WindowInterop.SetClickThrough(_hwnd, true);
        Reposition();
    }

    // Moving to a monitor with another scale resizes the window; center it again afterwards.
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.BeginInvoke(Reposition);
    }

    protected override void OnClosed(EventArgs e)
    {
        _hoverTimer.Stop();
        _fullscreenTimer.Stop();
        _dragWatcher.Dispose();
        _settings.Changed -= OnSettingsChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        base.OnClosed(e);
    }

    private bool IsMenuOpen =>
        ContextMenu.IsOpen || MacroBox.IsDropDownOpen || LoopBox.IsDropDownOpen || SpeedBox.IsDropDownOpen;

    private void UpdateVisibility()
    {
        bool shouldShow = _viewModel.IsBarAvailable && !_isFullscreenAppActive && !_isWindowDragActive;
        if (shouldShow == IsVisible)
        {
            return;
        }

        if (shouldShow)
        {
            Reposition();
            Show();
            _hoverTimer.Start();
        }
        else
        {
            _hoverTimer.Stop();
            _hover.Reset();
            ContextMenu.IsOpen = false;
            SetExpanded(false, animate: false);
            Hide();
        }
    }

    /// <summary>Places the window on the top edge of the chosen monitor, in physical pixels.</summary>
    private void Reposition()
    {
        if (_hwnd == 0)
        {
            return;
        }

        AppSettings settings = _settings.Current;
        _monitor = Monitors.FindOrPrimary(settings.NotchBarMonitor);
        ScreenRect bounds = _monitor.Bounds;
        int widthPx = (int)Math.Round(ExpandedWidth * _monitor.Scale);
        int anchorX = settings.NotchBarPosition switch
        {
            NotchBarPosition.Left => bounds.Left + (bounds.Width / 4),
            NotchBarPosition.Right => bounds.Left + (bounds.Width * 3 / 4),
            _ => bounds.Left + (bounds.Width / 2),
        };

        // WorkArea.Top keeps the bar below a taskbar that sits at the top of the screen.
        WindowInterop.MoveTo(_hwnd, anchorX - (widthPx / 2), _monitor.WorkArea.Top);
    }

    private void OnHoverCheck(object? sender, EventArgs e)
    {
        // While a macro moves the pointer, the bar must not open under it.
        if (_viewModel.IsPlaybackRunning || !MouseCursor.TryGetPosition(out ScreenPoint pointer))
        {
            return;
        }

        ScreenRect window = WindowInterop.GetBounds(_hwnd);
        bool isOverBar = _hover.IsExpanded
            ? IsMenuOpen || window.Contains(pointer)
            : GetClosedHoverZone(window).Contains(pointer);

        switch (_hover.Update(isOverBar, Environment.TickCount64))
        {
            case HoverDecision.Expand:
                SetExpanded(true, animate: true);
                break;
            case HoverDecision.Collapse:
                SetExpanded(false, animate: true);
                break;
        }
    }

    /// <summary>The thin strip plus a margin on both sides, so the pointer does not have to hit it exactly.</summary>
    private static ScreenRect GetClosedHoverZone(ScreenRect window)
    {
        double scale = window.Width / ExpandedWidth;
        int centerX = window.Left + (window.Width / 2);
        int halfWidth = (int)Math.Round(((CollapsedWidth / 2) + HoverMargin) * scale);
        int height = (int)Math.Ceiling(CollapsedHeight * scale);
        return new ScreenRect(centerX - halfWidth, window.Top, centerX + halfWidth, window.Top + height);
    }

    private void SetExpanded(bool expanded, bool animate)
    {
        // Follows the Windows setting "Animation effects".
        Duration duration = animate && SystemParameters.ClientAreaAnimation ? AnimationDuration : new Duration(TimeSpan.Zero);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        Animate(NotchOutline, WidthProperty, expanded ? ExpandedWidth : CollapsedWidth);
        Animate(NotchOutline, HeightProperty, expanded ? ExpandedHeight : CollapsedHeight);
        Animate(BarContent, OpacityProperty, expanded ? 1 : 0);
        Animate(StateIndicator, OpacityProperty, expanded ? 0 : 1);
        BarContent.IsHitTestVisible = expanded;

        // The closed bar lets everything through, also Windows' "which window is under this point" that the
        // recorder uses; otherwise clicks under the invisible part of the bar would be recorded as clicks on it.
        if (_hwnd != 0)
        {
            WindowInterop.SetClickThrough(_hwnd, !expanded);
        }

        void Animate(UIElement element, DependencyProperty property, double to) =>
            element.BeginAnimation(property, new DoubleAnimation(to, duration) { EasingFunction = easing });
    }

    private void OnFullscreenCheck(object? sender, EventArgs e)
    {
        bool isFullscreen = _monitor is not null && WindowInterop.IsFullscreenWindowOn(_monitor.Bounds);
        if (isFullscreen != _isFullscreenAppActive)
        {
            _isFullscreenAppActive = isFullscreen;
            UpdateVisibility();
        }
    }

    private void SetWindowDragActive(bool isActive)
    {
        _isWindowDragActive = isActive;
        UpdateVisibility();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NotchBarViewModel.IsBarAvailable):
                UpdateVisibility();
                break;

            // Recording and playback start closed, even when pinned: clicks under an open bar would land on
            // the bar. While recording or paused, the pointer resting on it still opens it.
            case nameof(NotchBarViewModel.IsBusy) when _viewModel.IsBusy != _wasBusy:
                _wasBusy = _viewModel.IsBusy;
                UpdatePinning();
                if (_wasBusy)
                {
                    _hover.Reset();
                    SetExpanded(false, animate: true);
                }

                break;

            // Resuming a paused macro closes the bar again, so it does not sit under the macro's clicks.
            case nameof(NotchBarViewModel.IsPlaybackRunning) when _viewModel.IsPlaybackRunning && _hover.IsExpanded:
                _hover.Reset();
                SetExpanded(false, animate: true);
                break;
        }
    }

    private void UpdatePinning() =>
        _hover.IsPinned = _settings.Current.NotchBarPinned && !_viewModel.IsBusy;

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        _hover.OpenDelay = TimeSpan.FromMilliseconds(settings.NotchBarOpenDelayMs);
        UpdatePinning();
        Reposition();
    }

    // Windows raises this on its own thread.
    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Reposition);
}
