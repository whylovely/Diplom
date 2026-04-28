using Client.Models;

namespace Client.Services;

// Проверяет корректность параметров формы перед построением транзакции
public sealed class TransactionValidator
{
    public string? Validate(
        TxKindChoice choice,
        Account? fromAccount,
        Account? toAccount,
        Category? category,
        Obligation? obligation,
        decimal amount)
    {
        if (fromAccount == null)
            return "Не выбран счет";

        if (amount <= 0)
            return "Сумма должна быть больше нуля";

        bool isCategoryRequired  = choice == TxKindChoice.Expense || choice == TxKindChoice.Income;
        bool isObligationRequired = choice == TxKindChoice.DebtRepayment || choice == TxKindChoice.DebtReceive;

        if (isCategoryRequired && category is null)
            return "Не выбрана категория";

        if (choice == TxKindChoice.Transfer && toAccount is null)
            return "Не выбран счет назначения";

        if (choice == TxKindChoice.Transfer && toAccount!.Id == fromAccount.Id)
            return "Счета должны отличаться";

        if (isObligationRequired && obligation is null)
            return "Не выбран долг";

        return null;
    }
}