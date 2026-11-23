using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace CrosshairStudio;

/// <summary>
/// Глобальные горячие клавиши: срабатывают, даже когда окно программы свёрнуто или открыта игра.
/// Используется системная функция Windows RegisterHotKey — она ничего не «перехватывает»
/// и не лезет в игру, просто Windows присылает нашему окну сообщение WM_HOTKEY.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    // Модификаторы (можно складывать через |): Ctrl | Alt
    public const uint Alt = 0x1;
    public const uint Ctrl = 0x2;
    public const uint Shift = 0x4;

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000; // не повторять, пока клавиша зажата

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    private readonly HwndSource _source;
    private readonly nint _hwnd;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    public HotkeyService(Window window)
    {
        // EnsureHandle создаёт системный дескриптор окна, если его ещё нет.
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source.AddHook(WndProc);
    }

    /// <summary>Возвращает false, если сочетание уже занято другой программой.</summary>
    public bool Register(uint modifiers, Key key, Action action)
    {
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        var id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, modifiers | MOD_NOREPEAT, vk)) return false;
        _actions[id] = action;
        return true;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return nint.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_hwnd, id);
        _actions.Clear();
        _source.RemoveHook(WndProc);
    }
}
