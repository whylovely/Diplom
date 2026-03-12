using Client.Models;
using Client.Services;
using Client.ViewModels;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels
{
    public sealed partial class NewTransactionViewModel : ViewModelBase
    {
        private readonly IDataService dataServ;
        private readonly INotificationService _notify;
        private readonly Action _onPosted;

        public ObservableCollection<Account> Accounts { get; }
        public ObservableCollection<Category> Categories { get; }
        public ObservableCollection<Category> FilteredCategories { get; } = new();
        public ObservableCollection<Obligation> ActiveObligations { get; } = new();

        public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;
        public string Description { get; set; } = "";

        [ObservableProperty] private Account? _fromAccount;
        [ObservableProperty] private Account? _toAccount;
        [ObservableProperty] private Category? _category;
        [ObservableProperty] private Obligation? _selectedObligation;

        public decimal Amount { get; set; }

        [ObservableProperty] TxKindChoice _choice = TxKindChoice.None;

        public bool IsExpense => Choice == TxKindChoice.Expense;
        public bool IsIncome => Choice == TxKindChoice.Income;
        public bool IsTransfer => Choice == TxKindChoice.Transfer;
        public bool IsDebtRepayment => Choice == TxKindChoice.DebtRepayment;
        public bool IsDebtReceive => Choice == TxKindChoice.DebtReceive;

        public bool IsSingleAccount => IsExpense || IsIncome || IsDebtRepayment || IsDebtReceive;
        public bool IsCategoryRequire => IsExpense || IsIncome;
        public bool IsObligationRequire => IsDebtRepayment || IsDebtReceive;

        public NewTransactionViewModel(IDataService data, INotificationService notify, Action onPosted)
        {
            dataServ = data;
            _notify = notify;
            _onPosted = onPosted;

            Accounts = new ObservableCollection<Account>(dataServ.Accounts.Where(a => a.Type == AccountType.Assets));
            Categories = new ObservableCollection<Category>(dataServ.Categories);

            _fromAccount = Accounts.FirstOrDefault();
            _toAccount = Accounts.Skip(1).FirstOrDefault();
            _category = Categories.FirstOrDefault();

            dataServ.DataChanged += OnDataChanged;

        }

        partial void OnChoiceChanged(TxKindChoice value)
        {
            OnPropertyChanged(nameof(IsExpense));
            OnPropertyChanged(nameof(IsIncome));
            OnPropertyChanged(nameof(IsTransfer));
            OnPropertyChanged(nameof(IsDebtRepayment));
            OnPropertyChanged(nameof(IsDebtReceive));

            OnPropertyChanged(nameof(IsSingleAccount));
            OnPropertyChanged(nameof(IsCategoryRequire));
            OnPropertyChanged(nameof(IsObligationRequire));

            ResetIrrelevantFields();
            ReloadCategories();
        }

        private void OnDataChanged()
        {
            ReloadCategories();
            ReloadAccounts();
            ReloadObligations();
        }

        private void ResetIrrelevantFields()
        {
            Category = null;

            if (Choice != TxKindChoice.Transfer)
                ToAccount = null;

            if (Choice == TxKindChoice.Transfer)
                Category = null;
        }

        public void PresetForQuickTx(Account account, TxKindChoice choice)
        {
            Choice = choice;

            FromAccount = Accounts.FirstOrDefault(a => a.Id == account.Id) ?? Accounts.FirstOrDefault();

            Amount = 0;
            Description = "";

            ResetIrrelevantFields();
            ReloadCategories();
            OnPropertyChanged(nameof(IsExpense));
            OnPropertyChanged(nameof(IsIncome));
            OnPropertyChanged(nameof(IsTransfer));
            OnPropertyChanged(nameof(IsDebtRepayment));
            OnPropertyChanged(nameof(IsDebtReceive));
            OnPropertyChanged(nameof(IsSingleAccount));
            OnPropertyChanged(nameof(IsCategoryRequire));
            OnPropertyChanged(nameof(IsObligationRequire));
        }

        public void PresetForDebtTx(Obligation obligation)
        {
            Choice = obligation.Type == ObligationType.Debt ? TxKindChoice.DebtRepayment : TxKindChoice.DebtReceive;
            
            Amount = obligation.Amount;
            Description = $"Погашение долга: {obligation.Counterparty}";

            SelectedObligation = ActiveObligations.FirstOrDefault(o => o.Id == obligation.Id) ?? ActiveObligations.FirstOrDefault();
            
            ResetIrrelevantFields();
            ReloadCategories();
            OnPropertyChanged(nameof(IsExpense));
            OnPropertyChanged(nameof(IsIncome));
            OnPropertyChanged(nameof(IsTransfer));
            OnPropertyChanged(nameof(IsDebtRepayment));
            OnPropertyChanged(nameof(IsDebtReceive));
            OnPropertyChanged(nameof(IsSingleAccount));
            OnPropertyChanged(nameof(IsCategoryRequire));
            OnPropertyChanged(nameof(IsObligationRequire));
        }

        [RelayCommand]
        private async Task PostAsync()
        {
            if (FromAccount == null)
            {
                await _notify.ShowErrorAsync("Не выбран счет");
                return;
            }

            if (Amount <= 0)
            {
                await _notify.ShowErrorAsync("Сумма должна быть больше нуля");
                return;
            }

            if (Choice != TxKindChoice.Transfer && Category is null)
            {
                await _notify.ShowErrorAsync("Не выбрана категория");
                return;
            }

            if (Choice == TxKindChoice.Transfer && ToAccount is null)
            {
                await _notify.ShowErrorAsync("Не выбран счет назначения");
                return;
            }

            if (Choice == TxKindChoice.Transfer && ToAccount!.Id == FromAccount!.Id)
            {
                await _notify.ShowErrorAsync("Счета должны отличаться");
                return;
            }
            
            if (IsObligationRequire && SelectedObligation is null)
            {
                await _notify.ShowErrorAsync("Не выбран долг");
                return;
            }

            var tx = new Transaction
            {
                Date = Date,
                Description = string.IsNullOrWhiteSpace(Description) ? Choice.ToString() : Description
            };

            var money = new Money(Amount, FromAccount!.CurrencyCode);

            switch (Choice)
            {
                case TxKindChoice.Expense:
                {
                    var expAcc = dataServ.GetExpenseAccountForCategory(Category!.Id);

                    tx.Entries.Add(new Entry
                    {
                        AccountId = FromAccount!.Id,
                        CategoryId = Category.Id,
                        Direction = EntryDirection.Credit,
                        Amount = money
                    });

                    tx.Entries.Add(new Entry
                    {
                        AccountId = expAcc.Id,
                        CategoryId = Category.Id,
                        Direction = EntryDirection.Debit,
                        Amount = money
                    });

                    break;
                }


                case TxKindChoice.Income:
                    {
                        var incAcc = dataServ.GetIncomeAccountForCategory(Category!.Id);

                        tx.Entries.Add(new Entry
                        {
                            AccountId = FromAccount!.Id,
                            CategoryId = Category.Id,
                            Direction = EntryDirection.Debit,
                            Amount = money
                        });

                        tx.Entries.Add(new Entry
                        {
                            AccountId = incAcc.Id,
                            CategoryId = Category.Id,
                            Direction = EntryDirection.Credit,
                            Amount = money
                        });

                        break;
                    }
     

                case TxKindChoice.Transfer:
                    {
                        if (ToAccount!.CurrencyCode != FromAccount!.CurrencyCode)
                        {
                            await _notify.ShowErrorAsync("Счета должны быть в одной валюте");
                            return;
                        }

                        tx.Entries.Add(new Entry
                        {
                            AccountId = FromAccount.Id,
                            CategoryId = null,
                            Direction = EntryDirection.Credit,
                            Amount = money
                        });

                        tx.Entries.Add(new Entry
                        {
                            AccountId = ToAccount.Id,
                            CategoryId = null,
                            Direction = EntryDirection.Debit,
                            Amount = money
                        });

                        break;
                    }


                case TxKindChoice.DebtRepayment:
                    {
                        if (SelectedObligation!.Currency != FromAccount!.CurrencyCode)
                        {
                            await _notify.ShowErrorAsync($"Валюта долга ({SelectedObligation.Currency}) не совпадает с валютой счета ({FromAccount.CurrencyCode})");
                            return;
                        }
                        
                        var expAcc = dataServ.Accounts.FirstOrDefault(a => a.Type == AccountType.Expense);
                        if (expAcc == null)
                        {
                            await _notify.ShowErrorAsync("Не найден технический счет расходов для списания долга.");
                            return;
                        }

                        tx.Entries.Add(new Entry
                        {
                            AccountId = FromAccount.Id,
                            CategoryId = null,
                            Direction = EntryDirection.Credit,
                            Amount = money
                        });

                        tx.Entries.Add(new Entry
                        {
                            AccountId = expAcc.Id,
                            CategoryId = null,
                            Direction = EntryDirection.Debit,
                            Amount = money
                        });

                        break;
                    }
                case TxKindChoice.DebtReceive:
                    {
                        if (SelectedObligation!.Currency != FromAccount!.CurrencyCode)
                        {
                            await _notify.ShowErrorAsync($"Валюта долга ({SelectedObligation.Currency}) не совпадает с валютой счета ({FromAccount.CurrencyCode})");
                            return;
                        }

                        var incAcc = dataServ.Accounts.FirstOrDefault(a => a.Type == AccountType.Income);
                        if (incAcc == null)
                        {
                            await _notify.ShowErrorAsync("Не найден технический счет доходов для зачисления долга.");
                            return;
                        }

                        tx.Entries.Add(new Entry
                        {
                            AccountId = FromAccount.Id,
                            CategoryId = null,
                            Direction = EntryDirection.Debit,
                            Amount = money
                        });

                        tx.Entries.Add(new Entry
                        {
                            AccountId = incAcc.Id,
                            CategoryId = null,
                            Direction = EntryDirection.Credit,
                            Amount = money
                        });

                        break;
                    }

                default:
                    await _notify.ShowErrorAsync("Неизвестный тип операции");
                    return;
            }

            try
            {
                await dataServ.PostTransactionAsync(tx);
            }
            catch (Exception ex)
            {
                await _notify.ShowErrorAsync(ex.Message);
                return;
            }

            if (IsObligationRequire && SelectedObligation != null)
            {
                try
                {
                    if (Amount >= SelectedObligation.Amount)
                        await dataServ.MarkObligationPaidAsync(SelectedObligation.Id, true);
                    else
                    {
                        SelectedObligation.Amount -= Amount;
                        await dataServ.UpdateObligationAsync(SelectedObligation);
                    }
                }
                catch (Exception ex)
                {
                    await _notify.ShowErrorAsync($"Операция проведена, но не удалось обновить обязательство: {ex.Message}");
                }
            }

            Amount = 0;
            Description = "";

            _onPosted();
        }

        public void ReloadObligations()
        {
            ActiveObligations.Clear();
            foreach (var o in dataServ.Obligations.Where(x => !x.IsPaid).OrderBy(x => x.Counterparty))
                ActiveObligations.Add(o);
                
            SelectedObligation ??= ActiveObligations.FirstOrDefault();
        }

        public void ReloadAccounts()
        {
            Accounts.Clear();
            foreach (var a in dataServ.Accounts.Where(x => x.Type == AccountType.Assets))
                Accounts.Add(a);

            FromAccount ??= Accounts.FirstOrDefault();
            ToAccount ??= Accounts.Skip(1).FirstOrDefault();
        }

        public void ReloadCategories()
        {
            FilteredCategories.Clear();

            if (Choice == TxKindChoice.Transfer)
            {
                Category = null;
                return;
            }

            var needKind = Choice == TxKindChoice.Income ? CategoryKind.Income : CategoryKind.Expense;

            foreach (var c in dataServ.Categories.Where(x => x.Kind == needKind).OrderBy(x => x.Name))
                FilteredCategories.Add(c);

            Category = FilteredCategories.FirstOrDefault();
        }

        [RelayCommand] private void SetExpense() => Choice = TxKindChoice.Expense;
        [RelayCommand] private void SetIncome() => Choice = TxKindChoice.Income;
        [RelayCommand] private void SetTransfer() => Choice = TxKindChoice.Transfer;
        [RelayCommand] private void SetDebtRepayment() => Choice = TxKindChoice.DebtRepayment;
        [RelayCommand] private void SetDebtReceive() => Choice = TxKindChoice.DebtReceive;
    }
}