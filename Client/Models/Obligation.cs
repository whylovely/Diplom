using CommunityToolkit.Mvvm.ComponentModel;
using Shared.Obligations;
using System;

namespace Client.Models;

public partial class Obligation : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty] private string _counterparty = string.Empty;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private string _currency = "RUB";
    [ObservableProperty] private ObligationType _type;
    [ObservableProperty] private DateTimeOffset _createdAt = DateTimeOffset.Now;
    [ObservableProperty] private DateTimeOffset? _dueDate;
    [ObservableProperty] private bool _isPaid;
    [ObservableProperty] private DateTimeOffset? _paidAt;
    [ObservableProperty] private string? _note;

    public string TypeLabel => Type == ObligationType.Debt ? "Я должен" : "Мне должны";
    
    public string StatusLabel 
    {
        get
        {
            if (IsPaid) return "Погашено";
            if (DueDate.HasValue && DueDate.Value < DateTimeOffset.Now) return "Просрочено";
            return "Активно";
        }
    }
}

public enum ObligationType
{
    Debt = 0,
    Credit = 1
}