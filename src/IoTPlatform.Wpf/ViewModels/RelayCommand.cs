using System.Diagnostics;
using System.Windows.Input;

namespace IoTPlatform.Wpf.ViewModels;

public sealed class RelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _isRunning;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isRunning && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isRunning = true;
            RaiseCanExecuteChanged();
            await execute();
        }
        catch (Exception exception)
        {
            // Keep command failures from terminating the WPF dispatcher.
            Debug.WriteLine(exception);
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T>(
    Func<T?, Task> execute,
    Func<T?, bool>? canExecute = null,
    bool allowConcurrentExecutions = false) : ICommand
{
    private int _runningCount;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        (allowConcurrentExecutions || _runningCount == 0) &&
        (canExecute?.Invoke((T?)parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _runningCount++;
            RaiseCanExecuteChanged();
            await execute((T?)parameter);
        }
        catch (Exception exception)
        {
            // Keep command failures from terminating the WPF dispatcher.
            Debug.WriteLine(exception);
        }
        finally
        {
            _runningCount--;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
