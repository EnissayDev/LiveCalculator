using System.Windows.Media;

namespace LiveCalculator.ViewModels;

public static class SkillPalette
{
    private static readonly Color[] Colours =
    {
        FromHex(0x66CCFF),
        FromHex(0x88B300),
        FromHex(0xED1121),
        FromHex(0xFFCC22),
        FromHex(0xFF66AA),
        FromHex(0x05F4FD),
    };

    public static Color ForIndex(int index) => Colours[index % Colours.Length];

    public static Brush BrushForIndex(int index)
    {
        var brush = new SolidColorBrush(ForIndex(index));
        brush.Freeze();
        return brush;
    }

    private static Color FromHex(int rgb) =>
        Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
}
