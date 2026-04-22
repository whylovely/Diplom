using System;
using System.Collections.Generic;
using System.Linq;
using Client.Models;

namespace Client.Services;

/// <summary>
/// Строит список проводок (Entry) для транзакции по параметрам формы.
/// Чистая бизнес-логика — не обращается к БД и не знает о UI.
/// Бросает <see cref="InvalidOperationException"/> при невалидных бизнес-условиях.
/// </summary>
public sealed class TransactionBuilder
{
    private readonly IDataService _data;

    public TransactionBuilder(IDataService data) => _data = data;

    public List<Entry> Build(
        TxKindChoice choice,
        Account fromAccount,
        Account? toAccount,
        Category? category,
        Obligation? obligation,
        Money money)
    {
        var entries = new List<Entry>();

        switch (choice)
        {
            case TxKindChoice.Expense:
            {
                var expAcc = _data.GetExpenseAccountForCategory(category!.Id);
                entries.Add(new Entry
                {
                    AccountId  = fromAccount.Id,
                    CategoryId = category.Id,
                    Direction  = EntryDirection.Credit,
                    Amount     = money
                });
                entries.Add(new Entry
                {
                    AccountId  = expAcc.Id,
                    CategoryId = category.Id,
                    Direction  = EntryDirection.Debit,
                    Amount     = money
                });
                break;
            }

            case TxKindChoice.Income:
            {
                var incAcc = _data.GetIncomeAccountForCategory(category!.Id);
                entries.Add(new Entry
                {
                    AccountId  = fromAccount.Id,
                    CategoryId = category.Id,
                    Direction  = EntryDirection.Debit,
                    Amount     = money
                });
                entries.Add(new Entry
                {
                    AccountId  = incAcc.Id,
                    CategoryId = category.Id,
                    Direction  = EntryDirection.Credit,
                    Amount     = money
                });
                break;
            }

            case TxKindChoice.Transfer:
            {
                if (toAccount!.CurrencyCode != fromAccount.CurrencyCode)
                    throw new InvalidOperationException("Счета должны быть в одной валюте");

                entries.Add(new Entry
                {
                    AccountId = fromAccount.Id,
                    Direction = EntryDirection.Credit,
                    Amount    = money
                });
                entries.Add(new Entry
                {
                    AccountId = toAccount.Id,
                    Direction = EntryDirection.Debit,
                    Amount    = money
                });
                break;
            }

            case TxKindChoice.DebtRepayment:
            {
                if (obligation!.Currency != fromAccount.CurrencyCode)
                    throw new InvalidOperationException(
                        $"Валюта долга ({obligation.Currency}) не совпадает с валютой счета ({fromAccount.CurrencyCode})");

                var expAcc = _data.Accounts.FirstOrDefault(a => a.Type == AccountType.Expense)
                    ?? throw new InvalidOperationException(
                        "Не найден технический счет расходов для списания долга.");

                entries.Add(new Entry
                {
                    AccountId = fromAccount.Id,
                    Direction = EntryDirection.Credit,
                    Amount    = money
                });
                entries.Add(new Entry
                {
                    AccountId = expAcc.Id,
                    Direction = EntryDirection.Debit,
                    Amount    = money
                });
                break;
            }

            case TxKindChoice.DebtReceive:
            {
                if (obligation!.Currency != fromAccount.CurrencyCode)
                    throw new InvalidOperationException(
                        $"Валюта долга ({obligation.Currency}) не совпадает с валютой счета ({fromAccount.CurrencyCode})");

                var incAcc = _data.Accounts.FirstOrDefault(a => a.Type == AccountType.Income)
                    ?? throw new InvalidOperationException(
                        "Не найден технический счет доходов для зачисления долга.");

                entries.Add(new Entry
                {
                    AccountId = fromAccount.Id,
                    Direction = EntryDirection.Debit,
                    Amount    = money
                });
                entries.Add(new Entry
                {
                    AccountId = incAcc.Id,
                    Direction = EntryDirection.Credit,
                    Amount    = money
                });
                break;
            }

            default:
                throw new InvalidOperationException("Неизвестный тип операции");
        }

        return entries;
    }
}
