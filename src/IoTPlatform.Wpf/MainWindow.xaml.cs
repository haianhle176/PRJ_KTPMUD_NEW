using System.Windows;
using System.Windows.Controls;
using IoTPlatform.Wpf.ViewModels;

namespace IoTPlatform.Wpf;

public partial class MainWindow : Window
{
    private bool _isPasswordVisible;
    private bool _isSyncingPassword;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void AuthPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncingPassword)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.Password = passwordBox.Password;
            _isSyncingPassword = true;
            AuthPasswordTextBox.Text = passwordBox.Password;
            _isSyncingPassword = false;
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(passwordBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void AuthPasswordTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncingPassword)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.Password = AuthPasswordTextBox.Text;
            _isSyncingPassword = true;
            AuthPasswordBox.Password = AuthPasswordTextBox.Text;
            _isSyncingPassword = false;
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(AuthPasswordTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void PasswordVisibilityButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        if (_isPasswordVisible)
        {
            AuthPasswordTextBox.Visibility = Visibility.Visible;
            AuthPasswordBox.Visibility = Visibility.Collapsed;
            PasswordVisibilitySlash.Visibility = Visibility.Collapsed;
            AuthPasswordTextBox.Focus();
            AuthPasswordTextBox.CaretIndex = AuthPasswordTextBox.Text.Length;
            return;
        }

        AuthPasswordTextBox.Visibility = Visibility.Collapsed;
        AuthPasswordBox.Visibility = Visibility.Visible;
        PasswordVisibilitySlash.Visibility = Visibility.Visible;
        AuthPasswordBox.Focus();
    }

    private void AuthUsernameTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        UsernamePlaceholder.Visibility = string.IsNullOrEmpty(AuthUsernameTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void AuthEmailTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        EmailPlaceholder.Visibility = string.IsNullOrEmpty(AuthEmailTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
