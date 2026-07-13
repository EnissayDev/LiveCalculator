using System.Windows;
using LiveCalculator.ViewModels;

namespace LiveCalculator;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel = new();

    private double savedWidth;
    private double savedHeight;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += (_, _) => viewModel.Start();
        Closed += (_, _) => viewModel.Stop();
    }

    // Toggles a small always-on-top "overlay" that only shows official SR, the pre-rework delta,
    // the new rework SR and the live gameplay SR. In this mode the view model skips the pricier
    // per-frame PP + strain-graph work (see MainViewModel.CompactMode).
    private void CompactToggle_Click(object sender, RoutedEventArgs e)
    {
        viewModel.CompactMode = !viewModel.CompactMode;

        if (viewModel.CompactMode)
        {
            savedWidth = Width;
            savedHeight = Height;

            MinWidth = 0;
            MinHeight = 0;
            ResizeMode = ResizeMode.CanMinimize;
            SizeToContent = SizeToContent.Height;
            Width = 340;
            Topmost = true;
        }
        else
        {
            SizeToContent = SizeToContent.Manual;
            Topmost = false;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 420;
            MinHeight = 620;
            Width = savedWidth;
            Height = savedHeight;
        }
    }
}
