namespace CrosshairStudio;

/// <summary>
/// Все настройки одного прицела. Именно этот класс сохраняется в JSON-файл пресета.
/// Если добавляешь новое свойство — старые пресеты продолжат работать (возьмётся значение по умолчанию).
/// </summary>
public class CrosshairPreset
{
    public string Name { get; set; } = "My crosshair";
    public string Color { get; set; } = "#58D68D";
    public double Size { get; set; } = 24;
    public double Thickness { get; set; } = 3;
    public bool CenterDot { get; set; } = false;
    public string Shape { get; set; } = "Cross";
    public bool OutlineEnabled { get; set; } = false;
    public double OutlineThickness { get; set; } = 1;
    public bool PulseEnabled { get; set; } = false;
    public double PulseSpeed { get; set; } = 2;
    public double PulseStrength { get; set; } = 0.25;
    public bool RecoilEnabled { get; set; } = false;
    public double RecoilAmount { get; set; } = 8;
    public double RecoilSpeed { get; set; } = 2;
    public bool GradientEnabled { get; set; } = false;
    public string SecondaryColor { get; set; } = "#FFFFFF";

    /// <summary>Прозрачность от 0.1 до 1.0.</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>Сдвиг прицела от центра экрана в пикселях (вправо / вниз — положительные).</summary>
    public double OffsetX { get; set; } = 0;
    public double OffsetY { get; set; } = 0;

    /// <summary>
    /// В JSON хранится только ИМЯ файла картинки (она копируется в папку Images),
    /// поэтому пресет не ломается, если исходную картинку переместили.
    /// Старые пресеты с полным путём тоже читаются (см. AppPaths.ResolveImage).
    /// </summary>
    public string? ImagePath { get; set; }
}
