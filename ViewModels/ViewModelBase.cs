using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace OpcUaCommunicationEngine.ViewModels;

/// <summary>
/// Base class cho tất cả ViewModels
/// Implement INotifyPropertyChanged và các helper methods
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isBusy;
    private string _busyMessage = string.Empty;

    /// <summary>
    /// Đang busy (loading, processing...)
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>
    /// Thông báo khi busy
    /// </summary>
    public string BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    /// <summary>
    /// Thông báo property đã thay đổi
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Set giá trị và thông báo nếu thay đổi
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Set busy state
    /// </summary>
    protected void SetBusy(bool isBusy, string message = "")
    {
        IsBusy = isBusy;
        BusyMessage = message;
    }

    /// <summary>
    /// Execute action với busy indicator
    /// </summary>
    protected async Task ExecuteWithBusyAsync(Func<Task> action, string busyMessage = "Processing...")
    {
        try
        {
            SetBusy(true, busyMessage);
            await action();
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Execute action với busy indicator và return value
    /// </summary>
    protected async Task<T?> ExecuteWithBusyAsync<T>(Func<Task<T>> action, string busyMessage = "Processing...")
    {
        try
        {
            SetBusy(true, busyMessage);
            return await action();
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Run action trên UI thread
    /// </summary>
    protected void RunOnUIThread(Action action)
    {
        if (Application.Current?.Dispatcher != null)
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(action);
            }
        }
        else
        {
            action();
        }
    }

    /// <summary>
    /// Run async action trên UI thread
    /// </summary>
    protected async Task RunOnUIThreadAsync(Action action)
    {
        if (Application.Current?.Dispatcher != null)
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(action);
            }
        }
        else
        {
            action();
        }
    }

    /// <summary>
    /// Show message box
    /// </summary>
    protected void ShowMessage(string message, string title = "Information")
    {
        RunOnUIThread(() => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information));
    }

    /// <summary>
    /// Show error message
    /// </summary>
    protected void ShowError(string message, string title = "Error")
    {
        RunOnUIThread(() => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error));
    }

    /// <summary>
    /// Show confirmation dialog
    /// </summary>
    protected bool ShowConfirmation(string message, string title = "Confirm")
    {
        var result = MessageBoxResult.No;
        RunOnUIThread(() =>
        {
            result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        });
        return result == MessageBoxResult.Yes;
    }
}
