using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Client.Views;

namespace Client.ViewModels;

public partial class ObligationsViewModel : ViewModelBase
{
    private readonly IDataService _data;
    private readonly INotificationService _notify;
    private readonly SettingsService _settings;
    private readonly Action<Obligation> _onPayDebt;

    public ObservableCollection<Obligation> Items { get; } = new();
    public ObservableCollection<Obligation> FilteredItems { get; } = new();

    [ObservableProperty] private Obligation? _selectedItem;

    [ObservableProperty] private bool _isDebtTab = true;
    [ObservableProperty] private bool _isCreditTab;

    [ObservableProperty] private bool _showPaid;

    [ObservableProperty] private decimal _totalDebt;
    [ObservableProperty] private decimal _totalCredit;

    public string BaseCurrencyCode => _settings.BaseCurrency;

    public ObligationsViewModel(
        IDataService data, 
        INotificationService notify,
        SettingsService settings, 
        Action<Obligation> onPayDebt)
    {
        _data = data;
        _notify = notify;
        _settings = settings;
        _onPayDebt = onPayDebt;
        _data.DataChanged += Refresh;
        _settings.SettingsChanged += Refresh;
        Refresh();
    }

    partial void OnShowPaidChanged(bool value) => Refresh();
    partial void OnIsDebtTabChanged(bool value) { if(value) Refresh(); }
    partial void OnIsCreditTabChanged(bool value) { if(value) Refresh(); }

    private void Refresh()
    {
        Items.Clear();
        FilteredItems.Clear();
        var all = _data.Obligations;

        var activeOrPaidItems = all.Where(o => ShowPaid || !o.IsPaid).OrderBy(o => o.DueDate).ToList();
        foreach (var item in activeOrPaidItems) Items.Add(item);

        var currentTabType = IsDebtTab ? ObligationType.Debt : ObligationType.Credit;
        var tabItems = activeOrPaidItems.Where(o => o.Type == currentTabType).ToList();
        foreach (var item in tabItems) FilteredItems.Add(item);

        TotalDebt = all.Where(o => o.Type == ObligationType.Debt && !o.IsPaid)
        .Sum(o => o.Amount * _data.GetRate(o.Currency, _settings.BaseCurrency));
        TotalCredit = all.Where(o => o.Type == ObligationType.Credit && !o.IsPaid)
        .Sum(o => o.Amount * _data.GetRate(o.Currency, _settings.BaseCurrency));
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var dialog = new AddObligationDialog(_settings);
        var result = await dialog.ShowDialog<Obligation?>(GetWindow());
        if (result != null)
        {
            try { await _data.AddObligationAsync(result); }
            catch (Exception ex) { await _notify.ShowErrorAsync(ex.Message); }
        }
    }

    [RelayCommand]
    private async Task EditAsync(Obligation? ob)
    {
        var target = ob ?? SelectedItem;
        if (target is null) return;
        
        var dialog = new AddObligationDialog(target, _settings);
        var result = await dialog.ShowDialog<Obligation?>(GetWindow());
        if (result != null)
        {
            try { await _data.UpdateObligationAsync(result); }
            catch (Exception ex) { await _notify.ShowErrorAsync(ex.Message); }
        }
    }

    private static Window GetWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow!;
        }
        throw new InvalidOperationException("Desktop lifetime is null");
    }

    [RelayCommand]
    private async Task DeleteAsync(Obligation? ob)
    {
        var target = ob ?? SelectedItem;
        if (target is null) return;

        var confirmed = await _notify.ShowConfirmAsync($"Удалить обязательство с {target.Counterparty}?");
        if (confirmed)
        {
            try { await _data.DeleteObligationAsync(target.Id); }
            catch (Exception ex) { await _notify.ShowErrorAsync(ex.Message); }
        }
    }

    [RelayCommand]
    private async Task MarkPaidAsync(Obligation? ob = null)
    {
        var target = ob ?? SelectedItem;
        if (target is null) return;
        
        try { await _data.MarkObligationPaidAsync(target.Id, !target.IsPaid); }
        catch (Exception ex) { await _notify.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private void PayDebt(Obligation? ob)  // открытие окна для погашения
    {
        var target = ob ?? SelectedItem;
        if (target == null) return;
        _onPayDebt(target);
    }

    private bool HasSelection => SelectedItem is not null;

    partial void OnSelectedItemChanged(Obligation? value)
    {
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        PayDebtCommand.NotifyCanExecuteChanged();
    }
}