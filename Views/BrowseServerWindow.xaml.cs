using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Opc.Ua.Client;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.ViewModels;

namespace OpcUaCommunicationEngine.Views;

public partial class BrowseServerWindow : Window
{
    private readonly BrowseServerViewModel _viewModel;

    public BrowseServerWindow(Session session, PlcDevice device)
    {
        InitializeComponent();

        _viewModel = new BrowseServerViewModel(session, device);
        DataContext = _viewModel;
    }

    private void Head_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    /// <summary>
    /// Tags that were added during this browse session
    /// </summary>
    public IReadOnlyList<TagItem> AddedTags => _viewModel.SelectedTags.ToList();

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is BrowseNodeItem node)
        {
            _viewModel.SelectedNode = node;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = _viewModel.SelectedTags.Any();
        Close();
    }

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        // Double-click on a variable node adds it as a tag
        if (_viewModel.SelectedNode?.CanAddAsTag == true)
        {
            _viewModel.AddSelectedTagCommand.Execute(null);
        }
    }
}
