using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.ViewModels;

public sealed partial class AddAccountDialogViewModel : ViewModelBase   // Сделать чтобы была закрпелена валюта базовая, а если многовалютный тогда можно указать другую валюту, но вторая оязательно должна быть базовой
{
    public static string[] Currencies { get; } =
    {
        "RUB", "USD", "EUR", "GBP", "CNY",
        "BTC", "ETH", "USDT"
    };

    public AddAccountDialogViewModel() : this("RUB") { }

    public AddAccountDialogViewModel(string defaultCurrency)
    {
        SelectedCurrency = defaultCurrency;
    } 

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isMultiCurrency;
    [ObservableProperty] private string _selectedCurrency = "RUB";
    [ObservableProperty] private string? _selectedSecondaryCurrency = "USD";
    [ObservableProperty] private decimal _initialBalance;

    public bool CanOk => !string.IsNullOrWhiteSpace(Name)
                         && SelectedCurrency is not null
                         && (!IsMultiCurrency || SelectedSecondaryCurrency is not null);

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(CanOk));
    partial void OnSelectedCurrencyChanged(string value) => OnPropertyChanged(nameof(CanOk));
    partial void OnSelectedSecondaryCurrencyChanged(string? value) => OnPropertyChanged(nameof(CanOk));
    partial void OnIsMultiCurrencyChanged(bool value) => OnPropertyChanged(nameof(CanOk));
}
