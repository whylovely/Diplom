using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
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

        private readonly List<Obligation> _obligations = new();
        public IReadOnlyList<Obligation> Obligations => _obligations;

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

            _obligations.AddRange([
                new Obligation { Counterparty = "Иван Иванов", Amount = 5000, Type = Shared.Obligations.ObligationType.Debt, DueDate = DateTimeOffset.Now.AddDays(5), Note = "Вернуть до пятницы" },
                new Obligation { Counterparty = "Петр Петров", Amount = 2000, Type = Shared.Obligations.ObligationType.Credit, DueDate = DateTimeOffset.Now.AddDays(-2), Note = "Обещал вернуть вчера" }, // Просрочено
                new Obligation { Counterparty = "Банк", Amount = 10000, Type = Shared.Obligations.ObligationType.Debt, IsPaid = true, PaidAt = DateTimeOffset.Now.AddDays(-10) }
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

        public void RemoveCategory(Category category)
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

        public Account GetExpenseAccountForCategory(Guid categoryId)
        {
            var accId = _expenseAccountByCategoryId[categoryId];
            return _accounts.Single(a => a.Id == accId);
        }

        public Account GetIncomeAccountForCategory(Guid categoryId)
        {
            var accId = _incomeAccountByCategoryId[categoryId];
            return _accounts.Single(a => a.Id == accId);
        }

        public Task PostTransactionAsync(Transaction tx)
        {
            if (tx.Entries.Count < 2)
                throw new InvalidOperationException("Транзакция не содержит двух проводок");

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
            return Task.CompletedTask;
        }

        public Task AddObligationAsync(Obligation obligation)
        {
            _obligations.Add(obligation);
            RaiseChanged();
            return Task.CompletedTask;
        }

        public Task UpdateObligationAsync(Obligation obligation)
        {
            var existing = _obligations.FirstOrDefault(o => o.Id == obligation.Id);
            if (existing != null)
            {
                var index = _obligations.IndexOf(existing);
                _obligations[index] = obligation;
                RaiseChanged();
            }
            return Task.CompletedTask;
        }

        public Task DeleteObligationAsync(Guid id)
        {
            var existing = _obligations.FirstOrDefault(o => o.Id == id);
            if (existing != null)
            {
                _obligations.Remove(existing);
                RaiseChanged();
            }
            return Task.CompletedTask;
        }

        public Task MarkObligationPaidAsync(Guid id, bool isPaid)
        {
            var existing = _obligations.FirstOrDefault(o => o.Id == id);
            if (existing != null)
            {
                existing.IsPaid = isPaid;
                existing.PaidAt = isPaid ? DateTimeOffset.Now : null;
                RaiseChanged();
            }
            return Task.CompletedTask;
        }
    }
}