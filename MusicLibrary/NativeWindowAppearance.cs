using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MusicLibrary;

internal static class NativeWindowAppearance
{
    public static void Apply(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int darkMode = 1;
        _ = DwmSetWindowAttribute(hwnd, DwmWindowAttribute.UseImmersiveDarkMode, ref darkMode, sizeof(int));

        int captionColor = ToColorRef(0x16, 0x16, 0x1F);
        _ = DwmSetWindowAttribute(hwnd, DwmWindowAttribute.CaptionColor, ref captionColor, sizeof(int));

        int textColor = ToColorRef(0xF4, 0xEC, 0xE3);
        _ = DwmSetWindowAttribute(hwnd, DwmWindowAttribute.TextColor, ref textColor, sizeof(int));

        int borderColor = ToColorRef(0x16, 0x16, 0x1F);
        _ = DwmSetWindowAttribute(hwnd, DwmWindowAttribute.BorderColor, ref borderColor, sizeof(int));
    }

    private static int ToColorRef(byte red, byte green, byte blue)
    {
        return red | (green << 8) | (blue << 16);
    }

    private static class DwmWindowAttribute
    {
        public const int UseImmersiveDarkMode = 20;
        public const int BorderColor = 34;
        public const int CaptionColor = 35;
        public const int TextColor = 36;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}
