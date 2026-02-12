using System.Collections.Generic;
using System.Linq;
using System;
using Client.Models;

namespace Client.Services
{
    public sealed class MockDS : IDataService // по сути заглушка (пока не написал сервер)
    {
        public event Action? DataChanged;
        private void RaiseChanged() => DataChanged?.Invoke();

        private readonly List<Account> _accounts = new();
        private readonly List<Category> _categories = new();
        private readonly List<Transaction> _tx = new();
            
        private readonly Dictionary<Guid, Guid> _expenseAccountByCategoryId = new();   
        private readonly Dictionary<Guid, Guid> _incomeAccountByCategoryId = new();

        public IReadOnlyList<Account> Accounts => _accounts;
        public IReadOnlyList<Category> Categories => _categories;
        public IReadOnlyList<Transaction> Transactions => _tx;

        public MockDS()
        {
            var acc1 = new Account { Name = "Наличные", CurrencyCode = "RUB", Balance = 250, InitialBalance = 250 };
            var acc2 = new Account { Name = "Карта", CurrencyCode = "RUB", Balance = 1250, InitialBalance = 1250 };
            var acc3 = new Account
            {
                Name = "Крипто-кошелёк",
                CurrencyCode = "USD",
                Balance = 500,
                InitialBalance = 500,
                IsMultiCurrency = true,
                SecondaryCurrencyCode = "BTC",
                SecondaryBalance = 0.015m
            };
            _accounts.AddRange([acc1, acc2, acc3]);

            _categories.AddRange([
                new Category{ Name = "Еда", Kind = CategoryKind.Expense},
                new Category{ Name = "Транспорт", Kind = CategoryKind.Expense },
                new Category{ Name = "Зарплата", Kind = CategoryKind.Income },
                new Category{ Name = "Подарок", Kind = CategoryKind.Income },
            ]);

            CreateTechnicalAccounts();
        }

        public void AddAccount(Account account) 
        { 
            if (account.Type == AccountType.Assets) account.InitialBalance = account.Balance;

            _accounts.Add(account);

            RaiseChanged();
        }

        public void AddCategory(Category category)
        {
            _categories.Add(category);
            RaiseChanged();
        }

        public void RenameAccount(Guid id, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return;

            var acc = Accounts.FirstOrDefault(a => a.Id == id);
            if (acc is null)
                return;

            acc.Name = newName.Trim();

            DataChanged?.Invoke();
        }

        public void RemoveCatergory(Category category)
        {
            _categories.RemoveAll(c => c.Id == category.Id);
            RaiseChanged();
        }

        public void RemoveAccount(Guid id)
        {
            var acc = _accounts.FirstOrDefault(a => a.Id == id);
            if (acc is null) return;
            _accounts.Remove(acc);
            DataChanged?.Invoke();
        }

        public bool IsAccountUsed(Guid Id) => Transactions.Any(tx => tx.Entries.Any(e => e.AccountId == Id));

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

        public void PostTransaction(Transaction tx) // Переписать под сервер
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
            RaiseChanged();
        }
    }
}