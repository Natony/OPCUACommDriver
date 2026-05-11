using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Base class cho tất cả Models, hỗ trợ INotifyPropertyChanged cho WPF Binding
/// Tự động dispatch PropertyChanged events lên UI thread để đảm bảo thread-safety
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Thông báo property đã thay đổi
    /// Tự động dispatch lên UI thread nếu cần
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        var handler = PropertyChanged;
        if (handler == null) return;

        // Check if we need to dispatch to UI thread
        if (Application.Current?.Dispatcher != null &&
            !Application.Current.Dispatcher.CheckAccess())
        {
            // We're on a background thread, dispatch to UI thread
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                handler.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }
        else
        {
            // We're already on UI thread or no dispatcher available
            handler.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
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
}
