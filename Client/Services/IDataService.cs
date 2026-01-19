using System.Collections.Generic;
using Client.Models;

namespace Client.Services
{
    public interface IDataService
    {
        IReadOnlyList<Account> Accounts { get; }
        IReadOnlyList<Category> Categories { get; }
        IReadOnlyList<Transaction> Transactions { get; }

        void AddAccount(Account account);
        void AddCategory(Category category);

        void PostTransaction(Transaction tx); // Пока заглушка
    }
}