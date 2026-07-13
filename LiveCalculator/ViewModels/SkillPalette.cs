using System.Windows.Media;

namespace LiveCalculator.ViewModels;

public static class SkillPalette
{
    private static readonly Color[] colours =
    {
        fromHex(0x66CCFF),
        fromHex(0x88B300),
        fromHex(0xED1121),
        fromHex(0xFFCC22),
        fromHex(0xFF66AA),
        fromHex(0x05F4FD),
    };

    public static Color ForIndex(int index) => colours[index % colours.Length];

    public static Brush BrushForIndex(int index)
    {
        var brush = new SolidColorBrush(ForIndex(index));
        brush.Freeze();
        return brush;
    }

    private static Color fromHex(int rgb) =>
        Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
}
