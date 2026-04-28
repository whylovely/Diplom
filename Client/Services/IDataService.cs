using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Models;

namespace Client.Services
{
    // Единый контракт доступа к данным для всех ViewModel
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

        void AddAccount(Account account);
        void RenameAccount(Guid id, string newName);
        void RemoveAccount(Guid id);
        bool IsAccountUsed(Guid id);
        void SetAccountGroup(Guid accountId, Guid? groupId);

        void AddCategory(Category category);
        void RemoveCategory(Category category);

        Task AddObligationAsync(Obligation obligation);
        Task UpdateObligationAsync(Obligation obligation);
        Task DeleteObligationAsync(Guid id);
        Task MarkObligationPaidAsync(Guid id, bool isPaid);

        Task AddTemplateAsync(TransactionTemplate template);
        Task DeleteTemplateAsync(Guid id);

        Task AddAccountGroupAsync(AccountGroup group);
        Task UpdateAccountGroupAsync(AccountGroup group);
        Task DeleteAccountGroupAsync(Guid id);

        void UpdateAccountsBaseCurrency(string newBaseCurrency);

        Account GetExpenseAccountForCategory(Guid categoryId);
        Account GetIncomeAccountForCategory(Guid categoryId);

        Task PostTransactionAsync(Transaction tx);
        Task StornoTransactionAsync(Guid transactionId);

        decimal GetRate(string fromCurrency, string toCurrency = "RUB");
        void SetCurrencyRate(string code, decimal rate);

        DateTimeOffset? GetLocalLastChangeDate();
        int GetLocalTransactionCount();

        void ClearDatabase();
    }
}