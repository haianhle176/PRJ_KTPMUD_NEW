using System.Windows;
using IoTPlatform.Wpf.ViewModels;

namespace IoTPlatform.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
