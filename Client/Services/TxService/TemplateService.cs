using System;
using Client.Models;

namespace Client.Services;

/// Создаёт TransactionTemplate из полей формы
public sealed class TemplateService
{
    public TransactionTemplate Create(
        string name,
        TxKindChoice choice,
        Guid? fromAccountId,
        Guid? toAccountId,
        Guid? categoryId,
        decimal amount,
        string description)
        => new TransactionTemplate
        {
            Name = name,
            Choice = choice,
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            CategoryId = categoryId,
            Amount = amount,
            Description = description
        };
}