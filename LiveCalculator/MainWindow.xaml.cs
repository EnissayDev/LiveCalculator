using System.Windows;
using LiveCalculator.ViewModels;

namespace LiveCalculator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += (_, _) => viewModel.Start();
        Closed += (_, _) => viewModel.Stop();
    }
}
