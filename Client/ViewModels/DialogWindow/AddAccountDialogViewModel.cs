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

    public bool HasNameError => Name.Length > 0 ? false : _nameTouched;
    private bool _nameTouched;

    public bool IsBalanceNegative => InitialBalance < 0;

    partial void OnNameChanged(string value)
    {
        _nameTouched = true;
        OnPropertyChanged(nameof(CanOk));
        OnPropertyChanged(nameof(HasNameError));
    }
    partial void OnSelectedCurrencyChanged(string value) => OnPropertyChanged(nameof(CanOk));
    partial void OnInitialBalanceChanged(decimal value)
    {
        OnPropertyChanged(nameof(CanOk));
        OnPropertyChanged(nameof(IsBalanceNegative));
    }
}