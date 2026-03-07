using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;

namespace Client.ViewModels;

public sealed partial class AddAccountDialogViewModel : ViewModelBase   // Добавление счета
{
    public static string[] Currencies => Models.CurrencyHelper.AvailableCurrencies;

    public AddAccountDialogViewModel() {}

    [ObservableProperty] private string _name = "";
    
    [ObservableProperty] private string _selectedCurrency = "RUB";
    
    [ObservableProperty] private decimal _initialBalance;

    public bool CanOk => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(SelectedCurrency) && InitialBalance >= 0;   // Кнопочки серые

    public bool IsBalanceNegative => InitialBalance < 0;

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(CanOk));
    partial void OnSelectedCurrencyChanged(string value) => OnPropertyChanged(nameof(CanOk));
    partial void OnInitialBalanceChanged(decimal value)
    {
        OnPropertyChanged(nameof(CanOk));
        OnPropertyChanged(nameof(IsBalanceNegative));
    }
}


/*
    public string BaseCurrency { get; }
    public string[] SecondaryCurrencies { get; }

    public AddAccountDialogViewModel() : this("RUB") { }

    public AddAccountDialogViewModel(string baseCurrency)
    {
        BaseCurrency = baseCurrency;
        SelectedCurrency = baseCurrency;
        SecondaryCurrencies = Currencies.Where(c => c != baseCurrency).ToArray();
        SelectedSecondaryCurrency = SecondaryCurrencies.FirstOrDefault();
    } 

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isMultiCurrency;
    [ObservableProperty] private string _selectedCurrency = "RUB";
    [ObservableProperty] private string? _selectedSecondaryCurrency;
    [ObservableProperty] private decimal _initialBalance;

    public bool CanOk => !string.IsNullOrWhiteSpace(Name)
                         && SelectedCurrency is not null
                         && (!IsMultiCurrency || SelectedSecondaryCurrency is not null);

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(CanOk));
    partial void OnSelectedCurrencyChanged(string value) => OnPropertyChanged(nameof(CanOk));
    partial void OnSelectedSecondaryCurrencyChanged(string? value) => OnPropertyChanged(nameof(CanOk));
    partial void OnIsMultiCurrencyChanged(bool value) => OnPropertyChanged(nameof(CanOk));
*/