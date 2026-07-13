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
    private const int max_points = 600;

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

        const double pad = 4;
        double plotHeight = height - pad * 2;

        for (int i = 0; i < series.Count; i++)
        {
            var difficulties = series[i].Difficulties;
            var points = new PointCollection();

            int count = difficulties.Count;
            int step = Math.Max(1, count / max_points);

            for (int x = 0; x < count; x += step)
            {
                double fx = count > 1 ? (double)x / (count - 1) : 0;
                double fy = difficulties[x] / globalMax;
                points.Add(new Point(fx * width, pad + (1 - fy) * plotHeight));
            }

            var polyline = new Polyline
            {
                Points = points,
                Stroke = SkillPalette.BrushForIndex(i),
                StrokeThickness = 1.6,
                StrokeLineJoin = PenLineJoin.Round
            };

            Children.Add(polyline);
        }
    }
}
