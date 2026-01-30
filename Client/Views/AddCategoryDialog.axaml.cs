using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Client.ViewModels;

namespace Client.Views;

public partial class AddCategoryDialog : Window
{
    public AddCategoryDialog()
    {
        InitializeComponent();
        DataContext = new AddCategoryDialogViewModel(this);
    }
}