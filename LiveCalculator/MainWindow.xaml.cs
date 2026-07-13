using System.Windows;
using LiveCalculator.ViewModels;

namespace LiveCalculator;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    private double _savedWidth;
    private double _savedHeight;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += (_, _) => _viewModel.Start();
        Closed += (_, _) => _viewModel.Stop();
    }

    // Toggles a small always-on-top "overlay" that only shows official SR, the pre-rework delta,
    // the new rework SR and the live gameplay SR. In this mode the view model skips the pricier
    // per-frame PP + strain-graph work (see MainViewModel.CompactMode).
    private void CompactToggle_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CompactMode = !_viewModel.CompactMode;

        if (_viewModel.CompactMode)
        {
            _savedWidth = Width;
            _savedHeight = Height;

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
            Width = _savedWidth;
            Height = _savedHeight;
        }
    }
}
