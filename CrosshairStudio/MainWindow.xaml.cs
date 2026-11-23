using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
// Алиасы вместо "using System.Windows.Shapes": иначе Path путается с System.IO.Path.
using Ellipse = System.Windows.Shapes.Ellipse;
using Line = System.Windows.Shapes.Line;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace CrosshairStudio;

public partial class MainWindow : Window
{
    private string? _imagePath;          // полный путь к выбранной картинке (или null)
    private OverlayWindow? _overlay;     // окно-оверлей поверх игры
    private bool _loadingPreset;         // true, пока загружаем пресет (чтобы не рисовать 10 раз подряд)
    private readonly System.Windows.Threading.DispatcherTimer _animationTimer =
        new() { Interval = TimeSpan.FromMilliseconds(33) };
    private double _animationTime;

    private HotkeyService? _hotkeys;
    private System.Windows.Forms.NotifyIcon? _tray;
    private bool _reallyExit;            // false: крестик окна прячет программу в трей

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RootGrid.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        };
        _animationTimer.Tick += (_, _) =>
        {
            _animationTime += _animationTimer.Interval.TotalSeconds;
            DrawPreview();
        };
        AppPaths.EnsureFolders();
        FillMonitors();
        RefreshPresets();
        DrawPreview();
        SetupTray();
    }

    private void Navigate(string page)
    {
        LauncherPanel.Visibility = page == "Launcher" ? Visibility.Visible : Visibility.Collapsed;
        StudioPanel.Visibility = page == "Studio" ? Visibility.Visible : Visibility.Collapsed;
        PresetsPanel.Visibility = page == "Presets" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        if (page == "Studio")
            DrawPreview();
    }

    private void Launcher_Click(object sender, RoutedEventArgs e) => Navigate("Launcher");
    private void Studio_Click(object sender, RoutedEventArgs e) => Navigate("Studio");
    private void Presets_Click(object sender, RoutedEventArgs e) => Navigate("Presets");
    private void Settings_Click(object sender, RoutedEventArgs e) => Navigate("Settings");

    // ───────────────────────── Горячие клавиши ─────────────────────────

    // Вызывается, когда у окна уже есть системный дескриптор — тут можно регистрировать hotkeys.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hotkeys = new HotkeyService(this);

        // Чтобы поменять сочетания — меняй здесь. Ctrl+Alt редко конфликтует с играми.
        var ok1 = _hotkeys.Register(HotkeyService.Ctrl | HotkeyService.Alt, Key.X, ToggleOverlay);
        var ok2 = _hotkeys.Register(HotkeyService.Ctrl | HotkeyService.Alt, Key.Z, NextPreset);

        FooterText.Text = ok1 && ok2
            ? "Crosshair Studio 1.0 • Ctrl+Alt+X — показать/скрыть • Ctrl+Alt+Z — следующий пресет"
            : "Crosshair Studio 1.0 • Не удалось занять часть горячих клавиш.";

        _ = CheckForUpdateAsync(false);
    }

    private async Task CheckForUpdateAsync(bool userInitiated)
    {
        var update = await UpdateService.CheckAsync();
        if (update.Error is not null)
        {
            if (userInitiated)
                MessageBox.Show(update.Error, "Оновлення",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!update.UpdateAvailable)
        {
            if (userInitiated)
                MessageBox.Show("Встановлено актуальну версію.", "Оновлення",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            $"Доступна нова версія {update.Version}. Завантажити та встановити?",
            "Оновлення",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
            return;

        try
        {
            var zipPath = await UpdateService.DownloadAsync(update);
            if (zipPath is null)
                throw new InvalidOperationException("Архів оновлення не завантажено.");
            UpdateService.ApplyPortableUpdate(zipPath);
            ExitApp();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Оновлення",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        _ = CheckForUpdateAsync(true);
    }

    private void NextPreset()
    {
        if (PresetList.Items.Count == 0) return;
        // Смена выбранного элемента сама вызовет PresetList_SelectionChanged и загрузит пресет.
        PresetList.SelectedIndex = (PresetList.SelectedIndex + 1) % PresetList.Items.Count;
    }

    // ───────────────────────── Трей ─────────────────────────

    private void SetupTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,   // позже можно поставить свою .ico
            Text = "Crosshair Studio",
            Visible = true
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Открыть окно", null, (_, _) => ShowFromTray());
        menu.Items.Add("Прицел вкл/выкл", null, (_, _) => ToggleOverlay());
        menu.Items.Add("Выход", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowFromTray();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _reallyExit = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    // Нажали крестик: вместо выхода прячем окно в трей (прицел продолжает работать).
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_reallyExit)
        {
            e.Cancel = true;
            Hide();
            _tray?.ShowBalloonTip(2000, "Crosshair Studio",
                "Программа работает в трее. Выход — правой кнопкой по значку.",
                System.Windows.Forms.ToolTipIcon.Info);
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _animationTimer.Stop();
        _hotkeys?.Dispose();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _overlay?.Close();
        base.OnClosed(e);
    }

    // ───────────────────────── Мониторы ─────────────────────────

    private void FillMonitors()
    {
        MonitorCombo.Items.Clear();
        var screens = System.Windows.Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            var main = s.Primary ? " — основной" : "";
            MonitorCombo.Items.Add($"Монитор {i + 1} ({s.Bounds.Width}×{s.Bounds.Height}){main}");
        }
        var primary = Array.FindIndex(screens, s => s.Primary);
        MonitorCombo.SelectedIndex = primary >= 0 ? primary : 0;
    }

    private void MonitorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Если оверлей уже виден — переносим его на выбранный монитор.
        if (_overlay is { IsVisible: true } && MonitorCombo.SelectedIndex >= 0)
            _overlay.ShowOn(MonitorCombo.SelectedIndex);
    }

    // ───────────────────────── Текущие настройки ─────────────────────────

    private CrosshairPreset CurrentPreset() => new()
    {
        Name = "My crosshair",
        Color = ColorBox.Text.Trim(),
        Size = SizeSlider.Value,
        Thickness = ThicknessSlider.Value,
        CenterDot = DotCheck.IsChecked == true,
        Shape = (ShapeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cross",
        OutlineEnabled = OutlineCheck.IsChecked == true,
        OutlineThickness = OutlineThicknessSlider.Value,
        PulseEnabled = PulseCheck.IsChecked == true,
        PulseSpeed = PulseSpeedSlider.Value,
        PulseStrength = PulseStrengthSlider.Value,
        RecoilEnabled = RecoilCheck.IsChecked == true,
        RecoilAmount = RecoilAmountSlider.Value,
        RecoilSpeed = RecoilSpeedSlider.Value,
        GradientEnabled = GradientCheck.IsChecked == true,
        SecondaryColor = SecondaryColorBox.Text.Trim(),
        Opacity = OpacitySlider.Value / 100.0,
        OffsetX = OffsetXSlider.Value,
        OffsetY = OffsetYSlider.Value,
        ImagePath = _imagePath
    };

    private bool TryGetColor(out Color color)
    {
        try
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(ColorBox.Text.Trim())!;
            color = brush.Color;
            return true;
        }
        catch
        {
            color = Colors.Lime;
            MessageBox.Show("Введи цвет в формате HEX, например #58D68D.", "Неверный цвет",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private bool TryGetColorSilently(out Color color)
    {
        try { color = (Color)ColorConverter.ConvertFromString(ColorBox.Text.Trim())!; return true; }
        catch { color = Colors.Lime; return false; }
    }

    // ───────────────────────── Обработчики настроек ─────────────────────────

    private void ApplyColor_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetColor(out _)) DrawPreview();
    }

    private void Setting_Changed(object sender, RoutedEventArgs e) => UpdatePreviewFromSettings();

    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdatePreviewFromSettings();

    private void SecondaryColorBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreviewFromSettings();

    private void PreviewArea_SizeChanged(object sender, SizeChangedEventArgs e) => DrawPreview();

    private void UpdatePreviewFromSettings()
    {
        if (_loadingPreset || !IsInitialized) return;
        SizeValue.Text = $"{SizeSlider.Value:0} px";
        ThicknessValue.Text = $"{ThicknessSlider.Value:0} px";
        OutlineThicknessValue.Text = $"{OutlineThicknessSlider.Value:0} px";
        PulseSpeedValue.Text = $"{PulseSpeedSlider.Value:0.0}";
        PulseStrengthValue.Text = $"{PulseStrengthSlider.Value * 100:0} %";
        RecoilAmountValue.Text = $"{RecoilAmountSlider.Value:0} px";
        RecoilSpeedValue.Text = $"{RecoilSpeedSlider.Value:0.0}";
        OpacityValue.Text = $"{OpacitySlider.Value:0} %";
        OffsetValue.Text = $"X: {OffsetXSlider.Value:0}   Y: {OffsetYSlider.Value:0}";
        var preset = CurrentPreset();
        if (preset.PulseEnabled || preset.RecoilEnabled)
        {
            if (!_animationTimer.IsEnabled) _animationTime = 0;
            _animationTimer.Start();
        }
        else
        {
            _animationTimer.Stop();
            _animationTime = 0;
        }
        DrawPreview();
    }

    // ───────────────────────── Рисование ─────────────────────────

    private void DrawPreview()
    {
        if (PreviewCanvas == null || !TryGetColorSilently(out var color)) return;

        var preset = CurrentPreset();

        // 1) Превью в окне программы
        PreviewCanvas.Children.Clear();
        var w = PreviewArea.ActualWidth;
        var h = PreviewArea.ActualHeight;
        if (w >= 10 && h >= 10)
        {
            PreviewCanvas.Opacity = preset.Opacity;
            var pulse = preset.PulseEnabled
                ? 1 + Math.Sin(_animationTime * preset.PulseSpeed * Math.PI * 2) * preset.PulseStrength
                : 1;
            var recoil = preset.RecoilEnabled
                ? Math.Max(0, Math.Sin(_animationTime * preset.RecoilSpeed * Math.PI * 2)) * preset.RecoilAmount
                : 0;
            var cx = w / 2 + preset.OffsetX;
            var cy = h / 2 + preset.OffsetY - recoil;

            var bitmap = (!string.IsNullOrWhiteSpace(_imagePath) && File.Exists(_imagePath))
                ? AppPaths.LoadBitmap(_imagePath) : null;

            if (bitmap != null)
            {
                var img = new Image { Source = bitmap, Width = 64, Height = 64, Stretch = Stretch.Uniform };
                Canvas.SetLeft(img, cx - 32);
                Canvas.SetTop(img, cy - 32);
                PreviewCanvas.Children.Add(img);
            }
            else
            {
                var secondary = color;
                if (preset.GradientEnabled)
                {
                    try { secondary = (Color)ColorConverter.ConvertFromString(preset.SecondaryColor)!; }
                    catch { }
                }
                Brush brush = preset.GradientEnabled
                    ? new LinearGradientBrush(color, secondary, 45)
                    : new SolidColorBrush(color);
                AddCrosshair(PreviewCanvas, cx, cy, preset.Size * pulse, preset.Thickness * pulse,
                    brush, preset.CenterDot, preset.Shape, preset.OutlineEnabled, preset.OutlineThickness);
            }
        }

        // 2) Живое обновление: если прицел уже показан в игре — обновляем и его.
        if (_overlay is { IsVisible: true })
        {
            _overlay.SetAnimationPhase(_animationTime);
            _overlay.SetPreset(preset);
        }
    }

    /// <summary>Рисует выбранную форму прицела. Используется и превью, и оверлеем.</summary>
    internal static void AddCrosshair(Canvas canvas, double cx, double cy, double size,
        double thickness, Brush brush, bool dot, string shape = "Cross",
        bool outline = false, double outlineThickness = 1)
    {
        void DrawLines(Brush stroke, double lineThickness)
        {
            void DrawLine(double x1, double y1, double x2, double y2)
            {
                canvas.Children.Add(new Line
                {
                    X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                    Stroke = stroke, StrokeThickness = lineThickness,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
                });
            }
            var gap = size * 0.22;
            var half = size / 2;
            switch (shape)
            {
                case "T":
                    DrawLine(cx - half, cy - half, cx + half, cy - half);
                    DrawLine(cx, cy - half, cx, cy + half);
                    break;
                case "Cross":
                default:
                    DrawLine(cx - half, cy, cx - gap, cy);
                    DrawLine(cx + gap, cy, cx + half, cy);
                    DrawLine(cx, cy - half, cx, cy - gap);
                    DrawLine(cx, cy + gap, cx, cy + half);
                    break;
            }
        }

        if (shape == "Dot")
        {
            var dotSize = Math.Max(thickness * 1.8, size * 0.45);
            var ellipse = new Ellipse { Width = dotSize, Height = dotSize, Fill = brush,
                Stroke = outline ? Brushes.Black : null, StrokeThickness = outline ? outlineThickness : 0 };
            Canvas.SetLeft(ellipse, cx - dotSize / 2);
            Canvas.SetTop(ellipse, cy - dotSize / 2);
            canvas.Children.Add(ellipse);
        }
        else if (shape == "Circle" || shape == "Square")
        {
            var shapeSize = shape == "Circle" ? size : size * 0.82;
            var half = shapeSize / 2;
            if (shape == "Circle")
            {
                if (outline)
                {
                    var border = new Ellipse
                    {
                        Width = shapeSize, Height = shapeSize, Fill = Brushes.Transparent,
                        Stroke = Brushes.Black, StrokeThickness = thickness + outlineThickness * 2
                    };
                    Canvas.SetLeft(border, cx - half);
                    Canvas.SetTop(border, cy - half);
                    canvas.Children.Add(border);
                }
                var ellipse = new Ellipse { Width = shapeSize, Height = shapeSize, Fill = Brushes.Transparent,
                    Stroke = brush, StrokeThickness = thickness };
                Canvas.SetLeft(ellipse, cx - half);
                Canvas.SetTop(ellipse, cy - half);
                canvas.Children.Add(ellipse);
            }
            else
            {
                if (outline)
                {
                    var border = new Rectangle { Width = shapeSize, Height = shapeSize, Fill = Brushes.Transparent,
                        Stroke = Brushes.Black, StrokeThickness = thickness + outlineThickness * 2 };
                    Canvas.SetLeft(border, cx - half);
                    Canvas.SetTop(border, cy - half);
                    canvas.Children.Add(border);
                }
                var rect = new Rectangle { Width = shapeSize, Height = shapeSize, Fill = Brushes.Transparent,
                    Stroke = brush, StrokeThickness = thickness };
                Canvas.SetLeft(rect, cx - half);
                Canvas.SetTop(rect, cy - half);
                canvas.Children.Add(rect);
            }
        }
        else
        {
            if (outline) DrawLines(Brushes.Black, thickness + outlineThickness * 2);
            DrawLines(brush, thickness);
        }

        if (dot && shape != "Dot")
        {
            var dotSize = thickness * 1.8;
            var ellipse = new Ellipse { Width = dotSize, Height = dotSize, Fill = brush };
            Canvas.SetLeft(ellipse, cx - dotSize / 2);
            Canvas.SetTop(ellipse, cy - dotSize / 2);
            canvas.Children.Add(ellipse);
        }
    }

    // ───────────────────────── Картинка ─────────────────────────

    private void LoadImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dialog.ShowDialog() == true)
        {
            _imagePath = dialog.FileName;
            ImageStatus.Text = Path.GetFileName(_imagePath);
            DrawPreview();
        }
    }

    private void RemoveImage_Click(object sender, RoutedEventArgs e)
    {
        _imagePath = null;
        ImageStatus.Text = "Изображение не выбрано";
        DrawPreview();
    }

    // ───────────────────────── Пресеты ─────────────────────────

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetColor(out _)) return;
        var preset = CurrentPreset();
        var nameWindow = new NameDialog();
        if (nameWindow.ShowDialog() != true) return;

        preset.Name = nameWindow.PresetName;
        var safeName = string.Concat(preset.Name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Preset";

        // Картинку копируем в папку программы, а в JSON пишем только имя файла.
        if (!string.IsNullOrWhiteSpace(_imagePath) && File.Exists(_imagePath))
        {
            var imageFileName = safeName + Path.GetExtension(_imagePath);
            var destination = Path.Combine(AppPaths.Images, imageFileName);
            if (!string.Equals(Path.GetFullPath(_imagePath), Path.GetFullPath(destination),
                    StringComparison.OrdinalIgnoreCase))
                File.Copy(_imagePath, destination, overwrite: true);
            preset.ImagePath = imageFileName;
            _imagePath = destination;
        }

        var path = Path.Combine(AppPaths.Presets, safeName + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true }));
        RefreshPresets();
    }

    private void RefreshPresets()
    {
        if (PresetList == null) return;
        PresetList.Items.Clear();
        foreach (var file in Directory.GetFiles(AppPaths.Presets, "*.json"))
            PresetList.Items.Add(Path.GetFileNameWithoutExtension(file));
    }

    private void RefreshPresets_Click(object sender, RoutedEventArgs e) => RefreshPresets();

    private void PresetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetList.SelectedItem is not string name) return;
        var path = Path.Combine(AppPaths.Presets, name + ".json");
        try
        {
            var preset = JsonSerializer.Deserialize<CrosshairPreset>(File.ReadAllText(path));
            if (preset == null) return;

            _loadingPreset = true;   // пока true — слайдеры не вызывают перерисовку
            ColorBox.Text = preset.Color;
            SizeSlider.Value = preset.Size;
            ThicknessSlider.Value = preset.Thickness;
            ShapeCombo.SelectedIndex = preset.Shape switch
            {
                "Dot" => 0,
                "Circle" => 1,
                "Square" => 2,
                "T" => 4,
                _ => 3
            };
            OutlineCheck.IsChecked = preset.OutlineEnabled;
            OutlineThicknessSlider.Value = preset.OutlineThickness;
            DotCheck.IsChecked = preset.CenterDot;
            PulseCheck.IsChecked = preset.PulseEnabled;
            PulseSpeedSlider.Value = preset.PulseSpeed;
            PulseStrengthSlider.Value = preset.PulseStrength;
            RecoilCheck.IsChecked = preset.RecoilEnabled;
            RecoilAmountSlider.Value = preset.RecoilAmount;
            RecoilSpeedSlider.Value = preset.RecoilSpeed;
            GradientCheck.IsChecked = preset.GradientEnabled;
            SecondaryColorBox.Text = preset.SecondaryColor;
            OpacitySlider.Value = preset.Opacity * 100;
            OffsetXSlider.Value = preset.OffsetX;
            OffsetYSlider.Value = preset.OffsetY;
            _imagePath = AppPaths.ResolveImage(preset.ImagePath);
            ImageStatus.Text = string.IsNullOrEmpty(_imagePath) ? "Изображение не выбрано" : Path.GetFileName(_imagePath);
            _loadingPreset = false;

            UpdatePreviewFromSettings();   // обновит подписи и перерисует всё один раз
        }
        catch (Exception ex)
        {
            _loadingPreset = false;
            MessageBox.Show("Не удалось загрузить пресет: " + ex.Message);
        }
    }

    // Встроенные пресеты
    private void SetPreset(string color, double size, double thickness, bool dot)
    {
        _loadingPreset = true;
        ColorBox.Text = color;
        SizeSlider.Value = size;
        ThicknessSlider.Value = thickness;
        ShapeCombo.SelectedIndex = 3;
        OutlineCheck.IsChecked = false;
        OutlineThicknessSlider.Value = 1;
        DotCheck.IsChecked = dot;
        PulseCheck.IsChecked = false;
        PulseSpeedSlider.Value = 2;
        PulseStrengthSlider.Value = 0.25;
        RecoilCheck.IsChecked = false;
        RecoilAmountSlider.Value = 8;
        RecoilSpeedSlider.Value = 2;
        GradientCheck.IsChecked = false;
        SecondaryColorBox.Text = "#FFFFFF";
        OpacitySlider.Value = 100;
        OffsetXSlider.Value = 0;
        OffsetYSlider.Value = 0;
        _imagePath = null;
        ImageStatus.Text = "Изображение не выбрано";
        _loadingPreset = false;
        UpdatePreviewFromSettings();
    }
    private void ClassicGreen_Click(object sender, RoutedEventArgs e) => SetPreset("#58D68D", 24, 3, false);
    private void SmallDot_Click(object sender, RoutedEventArgs e) => SetPreset("#FFFFFF", 10, 2, true);
    private void CyanCross_Click(object sender, RoutedEventArgs e) => SetPreset("#00D9FF", 30, 2, false);

    // ───────────────────────── Оверлей ─────────────────────────

    private void ShowOverlay()
    {
        if (!TryGetColorSilently(out _)) return;   // без окна с ошибкой — это может вызываться из игры
        _overlay ??= new OverlayWindow();
        _overlay.SetPreset(CurrentPreset());
        _overlay.ShowOn(Math.Max(0, MonitorCombo.SelectedIndex));
    }

    private void ToggleOverlay()
    {
        if (_overlay is { IsVisible: true }) _overlay.Hide();
        else ShowOverlay();
    }

    private void ToggleOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetColor(out _)) return;   // тут можно показать сообщение об ошибке цвета
        ShowOverlay();
    }

    private void HideOverlay_Click(object sender, RoutedEventArgs e) => _overlay?.Hide();
}
