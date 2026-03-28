using Avalonia.Controls;
using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection.Metadata;
using System.Text;

namespace Client.ViewModels.DialogWindow;

public partial class AddObligationDialogViewModel : ViewModelBase
{
    private readonly Window _window;

    [ObservableProperty] private string _counterparty = string.Empty;
    [ObservableProperty] private bool _hasCounterpartyError;

    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private bool _hasAmountError;

    [ObservableProperty] private string _currencyCode = "RUB";
    public string[] Currencies { get; }
 
    private ObligationType _type = ObligationType.Debt;
    public ObligationType Type
    {
        get => _type;
        set
        {
            if (SetProperty(ref _type, value))
            {
                OnPropertyChanged(nameof(IsDebt));
                OnPropertyChanged(nameof(IsCredit));
            }
        }
    }

    public bool IsDebt
    {
        get => Type == ObligationType.Debt;
        set { if (value) Type = ObligationType.Debt; }
    }

    public bool IsCredit
    {
        get => Type == ObligationType.Credit;
        set { if (value) Type = ObligationType.Credit; }
    }

    [ObservableProperty] private DateTimeOffset? _dueDate;  // Дата возврата
    [ObservableProperty] private bool _hasDueDateError;
    [ObservableProperty] private string? _note;
    [ObservableProperty] private string _title = "Новое обязательство";

    // public List<string> Currencies { get; } = new() { "RUB", "USD", "EUR" };
    //public List<ObligationType> Types { get; } = new() { ObligationType.Debt, ObligationType.Credit };

    public Obligation? Result { get; private set; }

    public AddObligationDialogViewModel(Window window, SettingsService? settings = null, Obligation? existing = null)
    {
        _window = window;
        Currencies = CurrencyHelper.GetFilteredCurrencies(
            settings?.Settings.FavoriteCurrencies);

        if (existing != null)
        {
            Title = "Редактирование обязательства";
            Counterparty = existing.Counterparty;
            Amount = existing.Amount;
            CurrencyCode = existing.Currency;
            Type = existing.Type;
            DueDate = existing.DueDate;
            Note = existing.Note;
            Result = existing; 
        }
    }

    [RelayCommand]
    private void Save()
    {
        HasCounterpartyError = string.IsNullOrWhiteSpace(Counterparty);
        HasAmountError = Amount <= 0;

        if (HasCounterpartyError || HasAmountError) return;

        if (Result == null) Result = new Obligation();

        Result.Counterparty = Counterparty;
        Result.Amount = Amount;
        Result.Currency = CurrencyCode;
        Result.Type = Type;
        Result.DueDate = DueDate;
        Result.Note = Note;
        Result.CreatedAt = DateTimeOffset.Now; 
        _window.Close(Result);
    }

    [RelayCommand]
    private void Cancel() => _window.Close(null);
}