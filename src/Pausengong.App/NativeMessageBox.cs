using System.Runtime.InteropServices;

namespace Pausengong.App;

/// <summary>
/// Plain Win32 message box, usable before any WinUI window/XamlRoot exists (e.g. during the
/// single-instance check in the App constructor, where a ContentDialog isn't an option yet).
/// </summary>
internal static partial class NativeMessageBox
{
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(nint hWnd, string text, string caption, uint type);

    private const uint MB_OK = 0x0;
    private const uint MB_ICONINFORMATION = 0x40;

    public static void Show(string caption, string text) =>
        MessageBoxW(0, text, caption, MB_OK | MB_ICONINFORMATION);
}
