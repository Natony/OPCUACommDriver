using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Services.OpcUa;
using OpcUaCommunicationEngine.ViewModels;

namespace OpcUaCommunicationEngine.Views;

public partial class BrowseServerWindow : Window
{
    private readonly BrowseServerViewModel _viewModel;

    public BrowseServerWindow(PlcConnection connection)
    {
        InitializeComponent();
        _viewModel = new BrowseServerViewModel(connection);
        DataContext = _viewModel;
    }

    private void Head_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    public IReadOnlyList<TagItem> AddedTags => _viewModel.SelectedTags.ToList();

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is BrowseNodeItem node)
            _viewModel.SelectedNode = node;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = _viewModel.SelectedTags.Any();
        Close();
    }

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (_viewModel.SelectedNode?.CanAddAsTag == true)
            _viewModel.AddSelectedTagCommand.Execute(null);
    }
}
