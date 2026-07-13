using System;
using System.Windows.Media;

namespace LiveCalculator.ViewModels;

public static class StarRatingColour
{
    private const double DefinedColourCutoff = 6.5;
    private const double TextGradientCutoff = 9.0;

    private static readonly (double pos, Color colour)[] StarSpectrum =
    {
        (0.1, FromHex(0xAAAAAA)),
        (0.1, FromHex(0x4290FB)),
        (1.25, FromHex(0x4FC0FF)),
        (2.0, FromHex(0x4FFFD5)),
        (2.5, FromHex(0x7CFF4F)),
        (3.3, FromHex(0xF6F05C)),
        (4.2, FromHex(0xFF8068)),
        (4.9, FromHex(0xFF4E6F)),
        (5.8, FromHex(0xC645B8)),
        (6.7, FromHex(0x6563DE)),
        (7.7, FromHex(0x18158E)),
        (9.0, FromHex(0x000000)),
        (10.0, FromHex(0x000000)),
    };

    private static readonly (double pos, Color colour)[] TextSpectrum =
    {
        (9.0, FromHex(0xF6F05C)),
        (9.9, FromHex(0xFF8068)),
        (10.6, FromHex(0xFF4E6F)),
        (11.5, FromHex(0xC645B8)),
        (12.4, FromHex(0x6563DE)),
    };

    private static readonly Color Orange1 = FromHex(0xFFD966);

    public static Color ForStars(double stars) => Sample(StarSpectrum, stars);

    public static Brush PillBrush(double stars)
    {
        var brush = new SolidColorBrush(WithAlpha(Darken(Sample(StarSpectrum, stars), 0.1), 0.75));
        brush.Freeze();
        return brush;
    }

    public static Brush TextBrush(double stars)
    {
        var brush = new SolidColorBrush(ForStarsText(stars));
        brush.Freeze();
        return brush;
    }

    public static Color ForStarsText(double stars)
    {
        if (stars < DefinedColourCutoff)
            return WithAlpha(Colors.Black, 0.75);

        if (stars < TextGradientCutoff)
            return Orange1;

        return Sample(TextSpectrum, stars);
    }

    private static Color Sample((double pos, Color colour)[] stops, double value)
    {
        value = Math.Round(value, 2, MidpointRounding.AwayFromZero);

        if (value <= stops[0].pos)
            return stops[0].colour;
        if (value >= stops[^1].pos)
            return stops[^1].colour;

        for (int i = 1; i < stops.Length; i++)
        {
            if (value > stops[i].pos)
                continue;

            double lo = stops[i - 1].pos;
            double hi = stops[i].pos;
            if (hi <= lo)
                return stops[i].colour;

            double t = (value - lo) / (hi - lo);
            return Lerp(stops[i - 1].colour, stops[i].colour, t);
        }

        return stops[^1].colour;
    }

    private static Color Darken(Color c, double amount)
    {
        double factor = 1.0 / (1.0 + amount);
        return Color.FromRgb((byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor));
    }

    private static Color WithAlpha(Color c, double alpha) =>
        Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), c.R, c.G, c.B);

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static Color FromHex(int rgb) =>
        Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
}
