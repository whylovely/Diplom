using CommunityToolkit.Mvvm.ComponentModel;
using Shared.Obligations;
using System;

namespace Client.Models;

/// <summary>
/// Долговое обязательство: либо я кому-то должен (Debt), либо мне должны (Credit).
/// При погашении формируется обычная транзакция через <c>TransactionBuilder</c>,
/// затем долг помечается как оплаченный.
/// Свойство <see cref="StatusLabel"/> вычисляется на лету: показывает «Просрочено» /
/// «Подходит срок» (≤ 3 дней до даты) / «Активно» / «Погашено».
/// </summary>
public partial class Obligation : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty] private string _counterparty = string.Empty;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private string _currency = "RUB";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TypeLabel))]
    private ObligationType _type;
    [ObservableProperty] private DateTimeOffset _createdAt = DateTimeOffset.Now;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    private DateTimeOffset? _dueDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    private bool _isPaid;
    
    [ObservableProperty] private DateTimeOffset? _paidAt;
    [ObservableProperty] private string? _note;

    public string TypeLabel => Type == ObligationType.Debt ? "Я должен" : "Мне должны";
    
    public string StatusLabel 
    {
        get
        {
            if (IsPaid) return "Погашено";
            if (DueDate.HasValue)
            {
                var now = DateTimeOffset.Now.Date;
                var due = DueDate.Value.Date;
                if (due < now) return "Просрочено";
                if ((due - now).TotalDays <= 3) return "Подходит срок";
            }
            return "Активно";
        }
    }
}

public enum ObligationType
{
    Debt = 0,
    Credit = 1
}