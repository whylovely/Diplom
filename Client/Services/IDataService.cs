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

        void AddAccount(Account account);   // Добавление счета
        void RenameAccount(Guid id, string newName);    // Переименовывание счета
        void RemoveAccount(Guid id);    // soft-delete счета
        bool IsAccountUsed(Guid id);    // Использовался ли аккаунт

        void AddCategory(Category category);    // Добавление категории
        void RemoveCategory(Category category); // Удаление категории

        Task AddObligationAsync(Obligation obligation); // Добавление обязательства
        Task UpdateObligationAsync(Obligation obligation);  // Изменение обязательства
        Task DeleteObligationAsync(Guid id);    // Удаление обязательства
        Task MarkObligationPaidAsync(Guid id, bool isPaid); // Статус обязательства

        Task AddTemplateAsync(TransactionTemplate template); // Добавление шаблона
        Task DeleteTemplateAsync(Guid id);                   // Удаление шаблона

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