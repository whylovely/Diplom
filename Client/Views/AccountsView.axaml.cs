using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Client.Views;

public partial class AccountsView : UserControl
{
    public AccountsView()
    {
        InitializeComponent();
        AccountsGrid.PointerPressed += AccountsGrid_PointerPressed;
    }

    private void AccountsGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(AccountsGrid).Properties.IsRightButtonPressed)
            return;

        var pos = e.GetPosition(AccountsGrid);
        var hit = AccountsGrid.InputHitTest(pos) as Control;
        var row = hit?.FindAncestorOfType<DataGridRow>();

        if (row?.DataContext is not null)
            AccountsGrid.SelectedItem = row.DataContext;
    }
}