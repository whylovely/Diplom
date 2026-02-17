using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using Client.ViewModels;

namespace Client.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainWindowViewModel vm)
        {
            await vm.OnWindowLoaded();
        }
    }
}