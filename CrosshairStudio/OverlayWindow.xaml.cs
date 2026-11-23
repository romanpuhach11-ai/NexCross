using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace CrosshairStudio;

/// <summary>
/// Прозрачное окно поверх игры: на весь выбранный монитор, пропускает клики мыши,
/// не забирает фокус и периодически «напоминает» Windows, что оно должно быть сверху.
/// </summary>
public partial class OverlayWindow : Window
{
    // --- Win32: стили окна ---
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;     // клики проходят сквозь окно
    private const int WS_EX_TOOLWINDOW = 0x80;      // не показывать в Alt+Tab
    private const int WS_EX_LAYERED = 0x80000;      // нужно для прозрачности
    private const int WS_EX_NOACTIVATE = 0x08000000; // окно никогда не получает фокус

    // --- Win32: SetWindowPos ---
    private static readonly nint HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private readonly DispatcherTimer _topmostTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private CrosshairPreset? _current;
    private int _monitorIndex;
    private double _animationPhase;

    public OverlayWindow()
    {
        InitializeComponent();

        // Окно уже имеет системный дескриптор, но ещё не показано — самое время поменять стили.
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            style |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new nint(style));
        };

        // Размер окна изменился (например, переехали на другой монитор) — перерисовываем.
        SizeChanged += (_, _) => Draw(null);

        // Игры (особенно в полноэкранном режиме) могут «перебить» Topmost.
        // Раз в 2 секунды просим Windows снова поднять нас наверх — без кражи фокуса.
        _topmostTimer.Tick += (_, _) => ReassertTopmost();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) _topmostTimer.Start(); else _topmostTimer.Stop();
        };
    }

    /// <summary>Показать оверлей на мониторе с номером monitorIndex (0 = первый).</summary>
    public void ShowOn(int monitorIndex)
    {
        _monitorIndex = monitorIndex;
        if (!IsVisible) Show();   // ShowActivated="False" в XAML => без фокуса
        PlaceOnMonitor();
    }

    private void PlaceOnMonitor()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screens.Length == 0) return;
        var index = Math.Clamp(_monitorIndex, 0, screens.Length - 1);
        var b = screens[index].Bounds;   // границы монитора в ПИКСЕЛЯХ

        var hwnd = new WindowInteropHelper(this).Handle;
        // Окно двигаем в пикселях напрямую через Win32 — так нет путаницы с масштабом DPI.
        // Вызываем два раза: если монитор с другим масштабом, WPF после первого вызова
        // может подправить размер, а второй вызов ставит его точно.
        SetWindowPos(hwnd, HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, SWP_NOACTIVATE);
        SetWindowPos(hwnd, HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, SWP_NOACTIVATE);
    }

    private void ReassertTopmost()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public void SetPreset(CrosshairPreset preset) => Draw(preset);

    public void SetAnimationPhase(double phase)
    {
        _animationPhase = phase;
    }

    private void Draw(CrosshairPreset? preset)
    {
        if (preset != null) _current = preset;
        if (_current == null || OverlayCanvas == null) return;
        if (ActualWidth < 1 || ActualHeight < 1) return;   // окно ещё не имеет размера

        OverlayCanvas.Children.Clear();
        OverlayCanvas.Opacity = Math.Clamp(_current.Opacity, 0.05, 1.0);

        // Центр монитора + сдвиг из настроек.
        var pulse = _current.PulseEnabled
            ? 1 + Math.Sin(_animationPhase * _current.PulseSpeed * Math.PI * 2) * _current.PulseStrength
            : 1;
        var recoil = _current.RecoilEnabled
            ? Math.Max(0, Math.Sin(_animationPhase * _current.RecoilSpeed * Math.PI * 2)) * _current.RecoilAmount
            : 0;
        var cx = ActualWidth / 2 + _current.OffsetX;
        var cy = ActualHeight / 2 + _current.OffsetY - recoil;

        try
        {
            var imagePath = AppPaths.ResolveImage(_current.ImagePath);
            var bitmap = (imagePath != null && System.IO.File.Exists(imagePath))
                ? AppPaths.LoadBitmap(imagePath) : null;

            if (bitmap != null)
            {
                var image = new Image { Source = bitmap, Width = 64, Height = 64, Stretch = Stretch.Uniform };
                Canvas.SetLeft(image, cx - 32);
                Canvas.SetTop(image, cy - 32);
                OverlayCanvas.Children.Add(image);
            }
            else
            {
                var color = (Color)ColorConverter.ConvertFromString(_current.Color)!;
                var secondary = color;
                if (_current.GradientEnabled)
                {
                    try { secondary = (Color)ColorConverter.ConvertFromString(_current.SecondaryColor)!; }
                    catch { }
                }
                Brush brush = _current.GradientEnabled
                    ? new LinearGradientBrush(color, secondary, 45)
                    : new SolidColorBrush(color);
                MainWindow.AddCrosshair(OverlayCanvas, cx, cy, _current.Size * pulse,
                    _current.Thickness * pulse, brush, _current.CenterDot, _current.Shape,
                    _current.OutlineEnabled, _current.OutlineThickness);
            }
        }
        catch { /* неверный цвет и т.п. — просто ничего не рисуем */ }
    }
}
