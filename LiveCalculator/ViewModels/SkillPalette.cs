using System.Windows.Media;

namespace LiveCalculator.ViewModels;

public static class SkillPalette
{
    private static readonly Color[] colours =
    {
        fromHex(0xFF7EB6),
        fromHex(0x7AA2FF),
        fromHex(0x63D4B0),
        fromHex(0xFFD166),
        fromHex(0xB692FF),
        fromHex(0xFF9F68),
        fromHex(0x8AE86A),
        fromHex(0xFF6B6B),
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
