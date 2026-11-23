using System.IO;
using System.Windows.Media.Imaging;

namespace CrosshairStudio;

/// <summary>Пути к папкам программы и общие функции для работы с картинками.</summary>
public static class AppPaths
{
    // Всё хранится в %LocalAppData%\CrosshairStudio
    public static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrosshairStudio");
    public static readonly string Presets = Path.Combine(Root, "Presets");
    public static readonly string Images = Path.Combine(Root, "Images");

    public static void EnsureFolders()
    {
        Directory.CreateDirectory(Presets);
        Directory.CreateDirectory(Images);
    }

    /// <summary>
    /// Превращает то, что записано в пресете, в реальный путь к файлу.
    /// Просто имя файла -> ищем в папке Images. Полный путь (старые пресеты) -> оставляем как есть.
    /// </summary>
    public static string? ResolveImage(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;
        return Path.IsPathRooted(stored) ? stored : Path.Combine(Images, stored);
    }

    /// <summary>
    /// Загружает картинку так, чтобы файл не оставался заблокированным
    /// (CacheOption.OnLoad читает всё в память сразу).
    /// Возвращает null, если файл не открылся.
    /// </summary>
    public static BitmapImage? LoadBitmap(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
