using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Models;

namespace Client.Services
{
    public interface IDataService
    {
        event Action? DataChanged;

        IReadOnlyList<Account> Accounts { get; }
        IReadOnlyList<Category> Categories { get; }
        IReadOnlyList<Transaction> Transactions { get; }
        IReadOnlyList<Obligation> Obligations { get; }

        void AddAccount(Account account);
        void AddCategory(Category category);
        void RemoveCategory(Category category);
        void RenameAccount(Guid id, string newName);
        void RemoveAccount(Guid id);
        bool IsAccountUsed(Guid id);

        Task AddObligationAsync(Obligation obligation);
        Task UpdateObligationAsync(Obligation obligation);
        Task DeleteObligationAsync(Guid id);
        Task MarkObligationPaidAsync(Guid id, bool isPaid);

        Account GetExpenseAccountForCategory(Guid categoryId);
        Account GetIncomeAccountForCategory(Guid categoryId);

        Task PostTransactionAsync(Transaction tx);
    }
}