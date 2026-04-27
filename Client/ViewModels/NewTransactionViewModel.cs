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
    /// <summary>
    /// Страница «Новая транзакция». Содержит форму с переключателем вида
    /// (расход/доход/перевод/долг) и набор сохранённых шаблонов.
    ///
    /// Логика разделена: <see cref="TransactionValidator"/> проверяет поля,
    /// <see cref="TransactionBuilder"/> строит проводки, <see cref="TemplateService"/>
    /// собирает шаблон. ViewModel занимается только UI и оркестрацией.
    /// </summary>
    public sealed partial class NewTransactionViewModel : ViewModelBase
    {
        private readonly IDataService _data;
        private readonly INotificationService _notify;
        private readonly IInputDialogService _input;
        private readonly Action _onPosted;

        private readonly TransactionBuilder   _builder   = default!;
        private readonly TransactionValidator _validator = new();
        private readonly TemplateService      _templates = new();

        public ObservableCollection<Account> Accounts { get; }
        public ObservableCollection<Category> Categories { get; }
        public ObservableCollection<Category> FilteredCategories { get; } = new();
        public ObservableCollection<Obligation> ActiveObligations { get; } = new();
        public ObservableCollection<TransactionTemplate> Templates { get; } = new();

        public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;
        public string Description { get; set; } = "";

        [ObservableProperty] private Account? _fromAccount;
        [ObservableProperty] private Account? _toAccount;
        [ObservableProperty] private Category? _category;
        [ObservableProperty] private Obligation? _selectedObligation;

        public decimal Amount { get; set; }

        [ObservableProperty] TxKindChoice _choice = TxKindChoice.None;

        public bool IsExpense       => Choice == TxKindChoice.Expense;
        public bool IsIncome        => Choice == TxKindChoice.Income;
        public bool IsTransfer      => Choice == TxKindChoice.Transfer;
        public bool IsDebtRepayment => Choice == TxKindChoice.DebtRepayment;
        public bool IsDebtReceive   => Choice == TxKindChoice.DebtReceive;

        public bool IsSingleAccount    => IsExpense || IsIncome || IsDebtRepayment || IsDebtReceive;
        public bool IsCategoryRequire  => IsExpense || IsIncome;
        public bool IsObligationRequire => IsDebtRepayment || IsDebtReceive;

        public NewTransactionViewModel(
            IDataService data,
            INotificationService notify,
            IInputDialogService input,
            Action onPosted)
        {
            _data     = data;
            _notify   = notify;
            _input    = input;
            _onPosted = onPosted;

            _builder = new TransactionBuilder(_data);

            Accounts   = new ObservableCollection<Account>(_data.Accounts.Where(a => a.Type == AccountType.Assets));
            Categories = new ObservableCollection<Category>(_data.Categories);

            _fromAccount = Accounts.FirstOrDefault();
            _toAccount   = Accounts.Skip(1).FirstOrDefault();
            _category    = Categories.FirstOrDefault();

            _data.DataChanged += OnDataChanged;
            ReloadTemplates();
        }

        // ── Обновление выбора типа операции ──────────────────────────────────

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
            ReloadTemplates();
        }

        // ── Быстрый пресет из внешнего кода ──────────────────────────────────

        public void PresetForQuickTx(Account account, TxKindChoice choice)
        {
            Choice      = choice;
            FromAccount = Accounts.FirstOrDefault(a => a.Id == account.Id) ?? Accounts.FirstOrDefault();
            Amount      = 0;
            Description = "";

            ResetIrrelevantFields();
            ReloadCategories();
            NotifyChoiceProperties();
        }

        public void PresetForDebtTx(Obligation obligation)
        {
            Choice      = obligation.Type == ObligationType.Debt ? TxKindChoice.DebtRepayment : TxKindChoice.DebtReceive;
            Amount      = obligation.Amount;
            Description = $"Погашение долга: {obligation.Counterparty}";

            SelectedObligation = ActiveObligations.FirstOrDefault(o => o.Id == obligation.Id)
                                 ?? ActiveObligations.FirstOrDefault();

            ResetIrrelevantFields();
            ReloadCategories();
            NotifyChoiceProperties();
        }

        private void NotifyChoiceProperties()
        {
            OnPropertyChanged(nameof(IsExpense));
            OnPropertyChanged(nameof(IsIncome));
            OnPropertyChanged(nameof(IsTransfer));
            OnPropertyChanged(nameof(IsDebtRepayment));
            OnPropertyChanged(nameof(IsDebtReceive));
            OnPropertyChanged(nameof(IsSingleAccount));
            OnPropertyChanged(nameof(IsCategoryRequire));
            OnPropertyChanged(nameof(IsObligationRequire));
        }

        // ── Основная команда: провести транзакцию ─────────────────────────────

        [RelayCommand]
        private async Task PostAsync()
        {
            // 1. Валидация формы
            var error = _validator.Validate(Choice, FromAccount, ToAccount, Category, SelectedObligation, Amount);
            if (error != null)
            {
                await _notify.ShowErrorAsync(error);
                return;
            }

            // 2. Построение проводок
            var money = new Money(Amount, FromAccount!.CurrencyCode);
            System.Collections.Generic.List<Entry> entries;
            try
            {
                entries = _builder.Build(Choice, FromAccount!, ToAccount, Category, SelectedObligation, money);
            }
            catch (InvalidOperationException ex)
            {
                await _notify.ShowErrorAsync(ex.Message);
                return;
            }

            // 3. Запись транзакции
            var tx = new Transaction
            {
                Date        = Date,
                Description = string.IsNullOrWhiteSpace(Description) ? Choice.ToString() : Description,
                Entries     = entries
            };

            try
            {
                await _data.PostTransactionAsync(tx);
            }
            catch (Exception ex)
            {
                await _notify.ShowErrorAsync(ex.Message);
                return;
            }

            // 4. Обновление обязательства при необходимости
            if (IsObligationRequire && SelectedObligation != null)
            {
                try
                {
                    if (Amount >= SelectedObligation.Amount)
                        await _data.MarkObligationPaidAsync(SelectedObligation.Id, true);
                    else
                    {
                        SelectedObligation.Amount -= Amount;
                        await _data.UpdateObligationAsync(SelectedObligation);
                    }
                }
                catch (Exception ex)
                {
                    await _notify.ShowErrorAsync(
                        $"Операция проведена, но не удалось обновить обязательство: {ex.Message}");
                }
            }

            // 5. Сброс формы
            Amount      = 0;
            Description = "";
            _onPosted();
        }

        // ── Шаблоны ───────────────────────────────────────────────────────────

        public void ReloadTemplates()
        {
            Templates.Clear();
            foreach (var t in _data.Templates.OrderBy(x => x.Name))
                Templates.Add(t);
        }

        [RelayCommand]
        private async Task SaveAsTemplateAsync()
        {
            var name = await _input.PromptAsync("Новый шаблон", "Введите название шаблона:", "Шаблон " + Choice);
            if (string.IsNullOrWhiteSpace(name)) return;

            var template = _templates.Create(
                name, Choice,
                FromAccount?.Id, ToAccount?.Id, Category?.Id,
                Amount, Description);

            await _data.AddTemplateAsync(template);
            ReloadTemplates();
            await _notify.ShowInfoAsync("Шаблон сохранен");
        }

        [RelayCommand]
        private void ApplyTemplate(TransactionTemplate template)
        {
            if (template == null) return;

            Choice      = template.Choice;
            FromAccount = Accounts.FirstOrDefault(a => a.Id == template.FromAccountId) ?? Accounts.FirstOrDefault();
            ToAccount   = Accounts.FirstOrDefault(a => a.Id == template.ToAccountId)   ?? Accounts.Skip(1).FirstOrDefault();

            ReloadCategories();
            Category    = FilteredCategories.FirstOrDefault(c => c.Id == template.CategoryId) ?? FilteredCategories.FirstOrDefault();
            Amount      = template.Amount;
            Description = template.Description;

            OnPropertyChanged(nameof(Amount));
            OnPropertyChanged(nameof(Description));
        }

        [RelayCommand]
        private async Task DeleteTemplateAsync(TransactionTemplate template)
        {
            if (template == null) return;
            await _data.DeleteTemplateAsync(template.Id);
            ReloadTemplates();
        }

        // ── Перезагрузка коллекций ────────────────────────────────────────────

        public void ReloadAccounts()
        {
            Accounts.Clear();
            foreach (var a in _data.Accounts.Where(x => x.Type == AccountType.Assets))
                Accounts.Add(a);

            FromAccount ??= Accounts.FirstOrDefault();
            ToAccount   ??= Accounts.Skip(1).FirstOrDefault();
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

            foreach (var c in _data.Categories.Where(x => x.Kind == needKind).OrderBy(x => x.Name))
                FilteredCategories.Add(c);

            Category = FilteredCategories.FirstOrDefault();
        }

        public void ReloadObligations()
        {
            ActiveObligations.Clear();
            foreach (var o in _data.Obligations.Where(x => !x.IsPaid).OrderBy(x => x.Counterparty))
                ActiveObligations.Add(o);

            SelectedObligation ??= ActiveObligations.FirstOrDefault();
        }

        // ── Вспомогательные ──────────────────────────────────────────────────

        private void ResetIrrelevantFields()
        {
            Category = null;

            if (Choice != TxKindChoice.Transfer)
                ToAccount = null;

            if (Choice == TxKindChoice.Transfer)
                Category = null;
        }

        [RelayCommand] private void SetExpense()       => Choice = TxKindChoice.Expense;
        [RelayCommand] private void SetIncome()        => Choice = TxKindChoice.Income;
        [RelayCommand] private void SetTransfer()      => Choice = TxKindChoice.Transfer;
        [RelayCommand] private void SetDebtRepayment() => Choice = TxKindChoice.DebtRepayment;
        [RelayCommand] private void SetDebtReceive()   => Choice = TxKindChoice.DebtReceive;
    }
}
