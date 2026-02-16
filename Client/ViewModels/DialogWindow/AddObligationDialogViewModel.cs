using Avalonia.Controls;
using Client.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;

namespace Client.ViewModels.DialogWindow;

public partial class AddObligationDialogViewModel : ViewModelBase
{
    private readonly Window _window;

    [ObservableProperty] private string _counterparty = string.Empty;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private string _currencyCode = "RUB";
    [ObservableProperty] private ObligationType _type = ObligationType.Debt;
    [ObservableProperty] private DateTimeOffset? _dueDate;
    [ObservableProperty] private string? _note;

    [ObservableProperty] private string _title = "Новое обязательство";

    public List<string> Currencies { get; } = new() { "RUB", "USD", "EUR" };
    public List<ObligationType> Types { get; } = new() { ObligationType.Debt, ObligationType.Credit };

    public Obligation? Result { get; private set; }

    public AddObligationDialogViewModel(Window window, Obligation? existing = null)
    {
        _window = window;

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
        if (string.IsNullOrWhiteSpace(Counterparty)) return; 
        if (Amount <= 0) return;

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
