using Avalonia.Controls;
using Avalonia.Input;
using Client.Models;
using Client.ViewModels;

namespace Client.Views;

public partial class AccountsView : UserControl
{
    public AccountsView()
    {
        InitializeComponent();
    }

    private void OnAccountPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control c && c.DataContext is Account acc)
        {
            if (DataContext is AccountsViewModel vm)
            {
                vm.SelectedAccount = acc;
            }
        }
    }
}
