using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Base class cho tất cả Models, hỗ trợ INotifyPropertyChanged cho WPF Binding
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

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
}
