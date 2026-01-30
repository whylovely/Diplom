using Avalonia.Controls;
using Client.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Xml.Linq;

namespace Client.ViewModels;

public sealed partial class AddCategoryDialogViewModel : ObservableObject
{
    private readonly Window _wnd;

    [ObservableProperty]
    private string _name = "";

    public AddCategoryDialogViewModel(Window wnd, string? initialName)
    {
        _wnd = wnd;
        Name = initialName ?? "";
    }

    private bool CanOk() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanOk))]
    private void Ok()
    {
        var clean = Name.Trim();

        _wnd.Close(new Category { Name = clean });
    }

    [RelayCommand]
    private void Cancel() => _wnd.Close(null);

    partial void OnNameChanged(string value) => OkCommand.NotifyCanExecuteChanged();
}