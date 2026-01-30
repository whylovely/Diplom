using System.Collections.Generic;
using System.Linq;
using System;
using Client.Models;

namespace Client.Services
{
    public sealed class MockDS : IDataService // по сути заглушка
    {
        private readonly List<Account> _accounts = new();
        private readonly List<Category> _categories = new();
        private readonly List<Transaction> _tx = new();
            
        private readonly Dictionary<Guid, Guid> _expenseAccountByCategoryId = new();    // быстрый поиск без перебора
        private readonly Dictionary<Guid, Guid> _incomeAccountByCategoryId = new();

        public IReadOnlyList<Account> Accounts => _accounts;
        public IReadOnlyList<Category> Categories => _categories;
        public IReadOnlyList<Transaction> Transactions => _tx;

        public MockDS()
        {
            var acc1 = new Account { Name = "Наличные", CurrencyCode = "RUB", Balance = 250, InitialBalance = 250 };
            var acc2 = new Account { Name = "Карта", CurrencyCode = "RUB", Balance = 1250, InitialBalance = 1250 };
            _accounts.AddRange([acc1, acc2]);

            _categories.AddRange([
                new Category{ Name = "Еда" },
                new Category{ Name = "Транспорт" },
                new Category{ Name = "Зарплата" },
                new Category{ Name = "Подарки" },
            ]);

            CreateTechnicalAccounts();
        }

        public void AddAccount(Account account) 
        { 
            if (account.Type == AccountType.Assets)
                account.InitialBalance = account.Balance;

            _accounts.Add(account); 
        }

        public void AddCategory(Category category) => _categories.Add(category);

        public void RemoveCatergory(Category category) => _categories.RemoveAll(c => c.Id == category.Id);

        private void CreateTechnicalAccounts()
        {
            foreach (var cat in _categories)
            {
                var expAcc = new Account
                {
                    Name = $"Расходы: {cat.Name}",
                    CurrencyCode = "RUB",
                    Balance = 0,
                    Type = AccountType.Expense
                };
                _accounts.Add(expAcc);
                _expenseAccountByCategoryId[cat.Id] = expAcc.Id;

                var incAcc = new Account
                {
                    Name = $"Доходы: {cat.Name}",
                    CurrencyCode = "RUB",
                    Balance = 0,
                    Type = AccountType.Income
                };
                _accounts.Add(incAcc);
                _incomeAccountByCategoryId[cat.Id] = incAcc.Id;
            }
        }

        public Account GetExpenseAccountForCatefory(Guid categoryId)
        {
            var accId = _expenseAccountByCategoryId[categoryId];
            return _accounts.Single(a => a.Id == accId);
        }

        public Account GetIncomeAccountForCatefory(Guid categoryId)
        {
            var accId = _incomeAccountByCategoryId[categoryId];
            return _accounts.Single(a => a.Id == accId);
        }

        public void PostTransaction(Transaction tx) 
        {
            // пока что минимальная проверка
            if (tx.Entries.Count < 2) throw new InvalidOperationException("Транзакция не содержит двух проводок"); 

            // Обновление счета на основе проводки (+дебит, -кредит)
            foreach (var e in tx.Entries)
            {
                var acc = _accounts.Single(a => a.Id == e.AccountId);

                if (acc.CurrencyCode != e.Amount.CurrencyCode)
                    throw new InvalidOperationException("Валюта проводки не совпадает с валютой счета");

                if (acc.Type == AccountType.Assets)
                {
                    var d = e.Direction == EntryDirection.Debit ? e.Amount.Amount : -e.Amount.Amount;
                    acc.Balance += d;
                }
            }

            _tx.Insert(0, tx);
        }
    }
}