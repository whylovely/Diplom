using Client.Models;
using Client.Services;
using Client.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels
{
    public enum TXKind
    {
        Expense = 1,
        Income = 2,
        Transfer = 3
    }

    public sealed partial class NewTransactionViewModel : ViewModelBase
    {
        private readonly IDataService _data;
        private readonly Action _onPosted;

        public Array KindValues => Enum.GetValues(typeof(TXKind));

        public ObservableCollection<Account> Accounts { get; }
        public ObservableCollection<Category> Categories { get; }

        [ObservableProperty] private TXKind _kind = TXKind.Expense;
        [ObservableProperty] private DateTimeOffset _date = DateTimeOffset.Now;
        [ObservableProperty] private string _description = "";

        [ObservableProperty] private Account? _fromAccount;
        [ObservableProperty] private Account? _toAccount;
        [ObservableProperty] private Category? _category;

        [ObservableProperty] private decimal _amount;

        public NewTransactionViewModel(IDataService data, Action onPosted)
        {
            _data = data;
            _onPosted = onPosted;

            Accounts = new ObservableCollection<Account>(_data.Accounts);
            Categories = new ObservableCollection<Category>(_data.Categories);

            _fromAccount = Accounts.FirstOrDefault();
            _toAccount = Accounts.Skip(1).FirstOrDefault();
            _category = Categories.FirstOrDefault();
        }

        [RelayCommand]
        private void Post()
        {
            if (_fromAccount == null) 
                throw new InvalidOperationException("Не выбран счет");

            if (_amount <= 0) 
                throw new InvalidOperationException("Сумма должна быть больше нуля");

            if (_kind != TXKind.Transfer && _category is null)
                throw new InvalidOperationException("Не выбрана категория");

            if (_kind != TXKind.Transfer && _toAccount is null)
                throw new InvalidOperationException("Не выбран счет назначения");

            if (_kind != TXKind.Transfer && _toAccount.Id == _fromAccount.Id)
                throw new InvalidOperationException("Счета должны отличаться");

            var tx = new Transaction
            {
                Date = _date,
                Description = string.IsNullOrWhiteSpace(_description) ? _kind.ToString() : _description
            };

            var money = new Money(_amount, _fromAccount.CurrencyCode);

            switch (_kind)
            {
                case TXKind.Expense:
                {
                    var expAcc = _data.GetExpenseAccountForCatefory(_category!.Id);

                    tx.Entries.Add(new Entry
                    {
                        AccountId = _fromAccount.Id,
                        CategoryId = _category.Id,
                        Direction = EntryDirection.Credit,
                        Amount = money
                    });

                    tx.Entries.Add(new Entry
                    {
                        AccountId = expAcc.Id,
                        CategoryId = _category.Id,
                        Direction = EntryDirection.Debit,
                        Amount = money
                    });

                    break;
                }


                case TXKind.Income:
                    {
                        var incAcc =  _data.GetIncomeAccountForCatefory(_category!.Id);

                        tx.Entries.Add(new Entry
                        {
                            AccountId = _fromAccount.Id,
                            CategoryId = _category.Id,
                            Direction = EntryDirection.Credit,
                            Amount = money
                        });

                        tx.Entries.Add(new Entry
                        {
                            AccountId = incAcc.Id,
                            CategoryId = _category.Id,
                            Direction = EntryDirection.Debit,
                            Amount = money
                        });

                        break;
                    }
     

                case TXKind.Transfer:
                    {
                        if (_toAccount.CurrencyCode != _fromAccount.CurrencyCode) 
                            throw new InvalidOperationException("Счета должны быть в одной валюте");

                        var catId = _category?.Id ?? _data.Categories.First().Id;

                        tx.Entries.Add(new Entry
                        {
                            AccountId = _fromAccount.Id,
                            CategoryId = catId,
                            Direction = EntryDirection.Credit,
                            Amount = money
                        });

                        tx.Entries.Add(new Entry
                        {
                            AccountId = _toAccount.Id,
                            CategoryId = catId,
                            Direction = EntryDirection.Debit,
                            Amount = money
                        });

                        break;
                    }

                default:
                    throw new InvalidOperationException("Неизвестный тип операции");
            }

            _data.PostTransaction(tx);

            _amount = 0;
            _description = "";

            _onPosted();
        }
    }
}