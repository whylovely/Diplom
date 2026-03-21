using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Models;

namespace Client.Services
{
    public interface IDataService
    {
        event Action? DataChanged;

        IReadOnlyList<CurrencyRate> CurrencyRates { get; }
        IReadOnlyList<Account> Accounts { get; }
        IReadOnlyList<Category> Categories { get; }
        IReadOnlyList<Transaction> Transactions { get; }
        IReadOnlyList<Obligation> Obligations { get; }
        IReadOnlyList<TransactionTemplate> Templates { get; }
        IReadOnlyList<AccountGroup> AccountGroups { get; }

        void AddAccount(Account account);   // Добавление счета
        void RenameAccount(Guid id, string newName);    // Переименовывание счета
        void RemoveAccount(Guid id);    // soft-delete счета
        bool IsAccountUsed(Guid id);    // Использовался ли аккаунт
        void SetAccountGroup(Guid accountId, Guid? groupId); // Привязка счета к группе

        void AddCategory(Category category);    // Добавление категории
        void RemoveCategory(Category category); // Удаление категории

        Task AddObligationAsync(Obligation obligation); // Добавление обязательства
        Task UpdateObligationAsync(Obligation obligation);  // Изменение обязательства
        Task DeleteObligationAsync(Guid id);    // Удаление обязательства
        Task MarkObligationPaidAsync(Guid id, bool isPaid); // Статус обязательства

        Task AddTemplateAsync(TransactionTemplate template); // Добавление шаблона
        Task DeleteTemplateAsync(Guid id);                   // Удаление шаблона

        Task AddAccountGroupAsync(AccountGroup group);       // Добавление группы счетов
        Task UpdateAccountGroupAsync(AccountGroup group);    // Обновление группы счетов
        Task DeleteAccountGroupAsync(Guid id);              // Удаление группы счетов

        Account GetExpenseAccountForCategory(Guid categoryId);  // Возвращение категории расходов
        Account GetIncomeAccountForCategory(Guid categoryId);   // Возвращение категории доходов

        Task PostTransactionAsync(Transaction tx);  // Проводка транзакции
        Task StornoTransactionAsync(Guid transactionId);    // Сторнирование транзакции

        decimal GetRate(string fromCurrency, string toCurrency = "RUB");    // Получение курса валют
        void SetCurrencyRate(string code, decimal rate);    // Обновление курса валют

        DateTimeOffset? GetLocalLastChangeDate();   // Дата последнего изменения
        int GetLocalTransactionCount();             // Количество транзакций

        void ClearDatabase();                       // Очистка БД (выход из аккаунта)
    }
}