using CommunityToolkit.Mvvm.ComponentModel;
using System;
using static System.Net.Mime.MediaTypeNames;

namespace Client.ViewModels;

public sealed partial class InputDialogViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _text = "";

    public bool CanOk => !string.IsNullOrWhiteSpace(Text);

    public Action<bool>? Close { get; set; }

    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(CanOk));
}