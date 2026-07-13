using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LiveCalculator.Osu;
using LiveCalculator.ViewModels;

namespace LiveCalculator.Controls;

public class SkillGraph : Canvas
{
    private const int MaxPoints = 600;

    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        nameof(Series), typeof(IReadOnlyList<SkillSeries>), typeof(SkillGraph),
        new PropertyMetadata(null, (d, _) => ((SkillGraph)d).Redraw()));

    public IReadOnlyList<SkillSeries>? Series
    {
        get => (IReadOnlyList<SkillSeries>?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public SkillGraph()
    {
        ClipToBounds = true;
        SizeChanged += (_, _) => Redraw();
    }

    private void Redraw()
    {
        Children.Clear();

        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 1 || height <= 1)
            return;

        var series = Series?.Where(s => s.Difficulties.Count > 1).ToList();
        if (series == null || series.Count == 0)
            return;

        double globalMax = series.SelectMany(s => s.Difficulties).DefaultIfEmpty(0).Max();
        if (globalMax <= 0)
            return;

        double fillAlpha = Math.Min(1.5 / series.Count, 0.9);
        double strokeAlpha = Math.Min(fillAlpha * 2.4, 0.95);

        const double pad = 3;
        double plotHeight = height - pad * 2;
        double baseline = pad + plotHeight;

        for (int i = 0; i < series.Count; i++)
        {
            var difficulties = series[i].Difficulties;
            int count = difficulties.Count;
            int step = Math.Max(1, count / MaxPoints);

            var line = new PointCollection();
            for (int x = 0; x < count; x += step)
            {
                double fx = count > 1 ? (double)x / (count - 1) : 0;
                double fy = difficulties[x] / globalMax;
                line.Add(new Point(fx * width, pad + (1 - fy) * plotHeight));
            }

            var colour = SkillPalette.ForIndex(i);

            var area = new PointCollection(line) { new Point(width, baseline), new Point(0, baseline) };
            Children.Add(new Polygon
            {
                Points = area,
                Fill = FrozenBrush(colour, fillAlpha)
            });

            Children.Add(new Polyline
            {
                Points = line,
                Stroke = FrozenBrush(colour, strokeAlpha),
                StrokeThickness = 1.4,
                StrokeLineJoin = PenLineJoin.Round
            });
        }
    }

    private static Brush FrozenBrush(Color colour, double alpha)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), colour.R, colour.G, colour.B));
        brush.Freeze();
        return brush;
    }
}
