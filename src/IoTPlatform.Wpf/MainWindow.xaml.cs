using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using IoTPlatform.Wpf.ViewModels;

namespace IoTPlatform.Wpf;

public partial class MainWindow : Window
{
    private bool _isPasswordVisible;
    private bool _isSyncingPassword;
    private readonly DispatcherTimer _notificationCloseTimer;

    public MainWindow()
    {
        InitializeComponent();
        _notificationCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _notificationCloseTimer.Tick += (_, _) =>
        {
            _notificationCloseTimer.Stop();
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.IsNotificationFlyoutOpen = false;
            }
        };
        var viewModel = new MainViewModel();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(MainViewModel.Password) ||
                AuthPasswordBox.Password == viewModel.Password)
            {
                return;
            }

            _isSyncingPassword = true;
            AuthPasswordBox.Password = viewModel.Password;
            AuthPasswordTextBox.Text = viewModel.Password;
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(viewModel.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
            _isSyncingPassword = false;
        };
        DataContext = viewModel;
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

    private void NotificationButton_OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _notificationCloseTimer.Stop();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsNotificationFlyoutOpen = true;
        }
    }

    private void NotificationButton_OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _notificationCloseTimer.Stop();
        _notificationCloseTimer.Start();
    }

    private void NotificationFlyout_OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _notificationCloseTimer.Stop();
    }

    private void NotificationFlyout_OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _notificationCloseTimer.Stop();
        _notificationCloseTimer.Start();
    }

    private void HomeDeviceCard_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null)
        {
            return;
        }

        if (DataContext is not MainViewModel viewModel ||
            sender is not FrameworkElement { Tag: HomeDeviceCard card } ||
            !viewModel.OpenHomeDeviceCommand.CanExecute(card))
        {
            return;
        }

        viewModel.OpenHomeDeviceCommand.Execute(card);
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
