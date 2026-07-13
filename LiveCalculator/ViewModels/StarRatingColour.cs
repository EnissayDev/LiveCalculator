using System;
using System.Windows.Media;

namespace LiveCalculator.ViewModels;

/// <summary>
/// Approximates osu!'s star-rating difficulty colour spectrum.
/// </summary>
public static class StarRatingColour
{
    private static readonly (double stars, Color colour)[] spectrum =
    {
        (0.1, fromHex(0x4290FB)),
        (1.25, fromHex(0x4FC0FF)),
        (2.0, fromHex(0x4FFFD5)),
        (2.5, fromHex(0x7CFF4F)),
        (3.3, fromHex(0xF6F05C)),
        (4.2, fromHex(0xFF8068)),
        (4.9, fromHex(0xFF4E6F)),
        (5.8, fromHex(0xC645B8)),
        (6.7, fromHex(0x6563DE)),
        (7.7, fromHex(0x18158E)),
        (9.0, fromHex(0x000000)),
    };

    public static Color ForStars(double stars)
    {
        if (stars <= spectrum[0].stars)
            return spectrum[0].colour;

        for (int i = 1; i < spectrum.Length; i++)
        {
            if (stars > spectrum[i].stars)
                continue;

            var (loStars, loColour) = spectrum[i - 1];
            var (hiStars, hiColour) = spectrum[i];
            double t = (stars - loStars) / (hiStars - loStars);
            return lerp(loColour, hiColour, t);
        }

        return spectrum[^1].colour;
    }

    private static Color lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static Color fromHex(int rgb) =>
        Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
}
