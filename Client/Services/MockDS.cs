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
            DemoData(); // изменить позже на дефолтные при регистрации аккаунта
        }

        private void DemoData()
        {
            var accCard = new Account { Name = "Карта", CurrencyCode = "RUB", Balance = 15450, InitialBalance = 15450, Type = AccountType.Assets };
            var accCash = new Account { Name = "Наличные", CurrencyCode = "RUB", Balance = 3500, InitialBalance = 3500, Type = AccountType.Assets };
            var accSavings = new Account { Name = "Копилка", CurrencyCode = "RUB", Balance = 100000, InitialBalance = 100000, Type = AccountType.Assets };
            var accCrypto = new Account 
            { 
                Name = "USDT Wallet", 
                CurrencyCode = "USD", 
                Balance = 250, 
                InitialBalance = 250, 
                Type = AccountType.Assets,
                IsMultiCurrency = true,
                SecondaryCurrencyCode = "RUB",
                SecondaryBalance = 23000
            };

            _accounts.AddRange([accCard, accCash, accSavings, accCrypto]);

            var catSalary = new Category { Name = "Зарплата", Kind = CategoryKind.Income };
            var catFreelance = new Category { Name = "Фриланс", Kind = CategoryKind.Income };
            var catCashback = new Category { Name = "Кешбэк", Kind = CategoryKind.Income };

            var catFood = new Category { Name = "Продукты", Kind = CategoryKind.Expense };
            var catCafe = new Category { Name = "Кафе и рестораны", Kind = CategoryKind.Expense };
            var catTransport = new Category { Name = "Транспорт", Kind = CategoryKind.Expense };
            var catHouse = new Category { Name = "Дом и ремонт", Kind = CategoryKind.Expense };
            var catMobile = new Category { Name = "Связь и интернет", Kind = CategoryKind.Expense };
            var catHealth = new Category { Name = "Здоровье", Kind = CategoryKind.Expense };
            var catEntertainment = new Category { Name = "Развлечения", Kind = CategoryKind.Expense };

            _categories.AddRange([
                catSalary, catFreelance, catCashback,
                catFood, catCafe, catTransport, catHouse, catMobile, catHealth, catEntertainment
            ]);

            CreateTechnicalAccounts();

            var today = DateTimeOffset.Now;

            AddTx(today.AddDays(-25), catSalary, accCard, 85000, "Зарплата (аванс)");
            AddTx(today.AddDays(-10), catSalary, accCard, 70000, "Зарплата (остаток)");
            AddTx(today.AddDays(-5), catFreelance, accCrypto, 150, "Оплата за проект", "USD");
            AddTx(today.AddDays(-20), catCashback, accCard, 1250, "Кешбэк за прошлый месяц");

            AddTx(today.AddDays(-28), catFood, accCard, 3500, "Ашан");
            AddTx(today.AddDays(-25), catFood, accCard, 1200, "Пятерочка");
            AddTx(today.AddDays(-21), catFood, accCash, 500, "Фрукты на рынке");
            AddTx(today.AddDays(-18), catFood, accCard, 4100, "Лента");
            AddTx(today.AddDays(-14), catFood, accCard, 800, "Молоко и хлеб");
            AddTx(today.AddDays(-7), catFood, accCard, 5600, "Закупка на неделю");
            AddTx(today.AddDays(-2), catFood, accCash, 350, "Мороженое");

            AddTx(today.AddDays(-26), catCafe, accCard, 1500, "Бизнес-ланчи");
            AddTx(today.AddDays(-15), catCafe, accCard, 3200, "Ужин с друзьями");
            AddTx(today.AddDays(-3), catCafe, accCash, 450, "Кофе с собой");

            AddTx(today.AddDays(-29), catTransport, accCard, 2500, "Проездной");
            AddTx(today.AddDays(-12), catTransport, accCard, 800, "Такси");
            AddTx(today.AddDays(-1), catTransport, accCard, 600, "Такси");

            AddTx(today.AddDays(-10), catHouse, accCard, 4500, "Коммуналка");
            AddTx(today.AddDays(-10), catMobile, accCard, 900, "Интернет + ТВ");
            AddTx(today.AddDays(-5), catMobile, accCard, 500, "Мобильная связь");

            AddTx(today.AddDays(-8), catEntertainment, accCard, 1200, "Кино");
            AddTx(today.AddDays(-2), catEntertainment, accCard, 2500, "Боулинг");

            AddTransfer(today.AddDays(-9), accCard, accSavings, 30000, "В копилку");


            _obligations.AddRange([
                new Obligation { Counterparty = "Максим", Amount = 5000, Type = ObligationType.Debt, DueDate = today.AddDays(7), Note = "Одолжил до зарплаты" },
                new Obligation { Counterparty = "Альфа-Банк (Кредитка)", Amount = 12400, Type = ObligationType.Credit, DueDate = today.AddDays(15), Note = "Льготный период" },
                new Obligation { Counterparty = "Ипотека", Amount = 25000, Type = ObligationType.Credit, IsPaid = true, PaidAt = today.AddDays(-10), Note = "Платеж за текущий месяц" }
            ]);
        }

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

        private void AddTx(DateTimeOffset date, Category cat, Account acc, decimal amount, string note, string currency = "RUB")
        {
            var accExp = GetExpenseAccountForCategory(cat.Id);
            var accInc = GetIncomeAccountForCategory(cat.Id);
            
            if (cat.Kind == CategoryKind.Income)
            {
                var tx = new Transaction
                {
                    Date = date,
                    Description = note,
                    Entries = [
                        new Entry { AccountId = accInc.Id, Amount = new Money(amount, currency), Direction = EntryDirection.Credit, CategoryId = cat.Id }, 
                        new Entry { AccountId = acc.Id, Amount = new Money(amount, currency), Direction = EntryDirection.Debit, CategoryId = cat.Id }      
                    ] 
                };
                 _tx.Add(tx);
                 acc.Balance += amount;
            }
            else
            {
                var tx = new Transaction
                {
                    Date = date,
                    Description = note,
                    Entries = [
                        new Entry { AccountId = acc.Id, Amount = new Money(amount, currency), Direction = EntryDirection.Credit, CategoryId = cat.Id },  
                        new Entry { AccountId = accExp.Id, Amount = new Money(amount, currency), Direction = EntryDirection.Debit, CategoryId = cat.Id }  
                    ]
                };
                _tx.Add(tx);
                acc.Balance -= amount;
            }
        }

        private void AddTransfer(DateTimeOffset date, Account from, Account to, decimal amount, string note, string currency = "RUB")
        {
             var tx = new Transaction
            {
                Date = date,
                Description = note,
                Entries = [
                    new Entry { AccountId = from.Id, Amount = new Money(amount, currency), Direction = EntryDirection.Credit }, 
                    new Entry { AccountId = to.Id, Amount = new Money(amount, currency), Direction = EntryDirection.Debit }   
                ]
            };
            _tx.Add(tx);
            from.Balance -= amount;
            to.Balance += amount;
        }

        public void AddAccount(Account account) 
        { 
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