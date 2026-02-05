using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Client.Views;

public partial class CategoriesView : UserControl
{
    public CategoriesView()
    {
        InitializeComponent();
        CategoriesGrid.PointerPressed += CategoriesGrid_PointerPressed;
    }

    private void CategoriesGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(CategoriesGrid).Properties.IsRightButtonPressed)
            return;

        var pos = e.GetPosition(CategoriesGrid);
        var hit = CategoriesGrid.InputHitTest(pos) as Control;
        var row = hit?.FindAncestorOfType<DataGridRow>();

        if (row?.DataContext is not null)
            CategoriesGrid.SelectedItem = row.DataContext;
    }
}