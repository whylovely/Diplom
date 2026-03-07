using Avalonia.Controls;
using Client.Models;
using Client.ViewModels.DialogWindow;

namespace Client.Views;

public partial class AddObligationDialog : Window
{
    public AddObligationDialog()
    {
        InitializeComponent();
        DataContext = new AddObligationDialogViewModel(this);
    }

    public AddObligationDialog(Obligation? existing = null)
    {
        InitializeComponent();
        DataContext = new AddObligationDialogViewModel(this, existing);
    }
}