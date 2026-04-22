using System;
using Client.Models;

namespace Client.Services;

/// <summary>
/// Создаёт <see cref="TransactionTemplate"/> из полей формы.
/// Сохранение и удаление шаблонов остаются за <see cref="IDataService"/>.
/// </summary>
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
            Name          = name,
            Choice        = choice,
            FromAccountId = fromAccountId,
            ToAccountId   = toAccountId,
            CategoryId    = categoryId,
            Amount        = amount,
            Description   = description
        };
}
