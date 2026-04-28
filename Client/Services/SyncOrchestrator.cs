using System;
using System.Threading.Tasks;
using Client.Models;

namespace Client.Services;

public sealed class SyncAnalysis
{
    private const int ConflictThreshold = 10;

    public int LocalCount { get; init; }
    public int ServerCount { get; init; }
    public DateTimeOffset? LocalLastChange { get; init; }

    public bool ServerReachable => ServerCount >= 0;

    public bool NeedsConflictDialog => ServerReachable && Math.Abs(LocalCount - ServerCount) > ConflictThreshold;
}


public enum SyncAction
{
    SmartSync,  // автоматически: push если есть изменения, затем pull
    PushOnly,   // только отправить локальные данные на сервер
    PullOnly,   // только загрузить данные с сервера
    Cancel,     // пользователь выбрал «оставить локальные данные»
    Dismiss     // пользователь закрыл диалог без выбора
}

public sealed class SyncOutcome
{
    public bool Success { get; init; }
    public bool WasCancelled { get; init; }
    public bool WasDismissed { get; init; }

    public bool DataReplaced { get; init; }

    public string? ErrorMessage { get; init; }

    public static SyncOutcome Ok(bool dataReplaced = false) =>
        new() { Success = true, DataReplaced = dataReplaced };

    public static SyncOutcome Fail(string? msg) =>
        new() { Success = false, ErrorMessage = msg ?? "Неизвестная ошибка" };

    public static SyncOutcome Cancelled() => new() { WasCancelled = true };
    public static SyncOutcome Dismissed() => new() { WasDismissed = true };
}

// Принимает решения о синхронизации и выполняет их
public sealed class SyncOrchestrator
{
    private readonly SyncService _sync;
    private readonly IDataService _data;

    public SyncOrchestrator(SyncService sync, IDataService data)
    {
        _sync = sync;
        _data = data;
    }

    public async Task<SyncAnalysis> AnalyzeAsync()
    {
        var localCount = _data.GetLocalTransactionCount();
        var serverCount = await _sync.GetServerTransactionCountAsync();
        var localLastChange = _data.GetLocalLastChangeDate();

        return new SyncAnalysis
        {
            LocalCount = localCount,
            ServerCount = serverCount,
            LocalLastChange = localLastChange
        };
    }

    public Task<SyncOutcome> ExecuteAsync(SyncAction action) => action switch
    {
        SyncAction.SmartSync => RunSmartSyncAsync(),
        SyncAction.PushOnly => RunPushAsync(),
        SyncAction.PullOnly => RunPullAsync(),
        SyncAction.Cancel => Task.FromResult(SyncOutcome.Cancelled()),
        SyncAction.Dismiss => Task.FromResult(SyncOutcome.Dismissed()),
        _ => Task.FromResult(SyncOutcome.Fail("Неизвестное действие"))
    };

    private async Task<SyncOutcome> RunSmartSyncAsync()
    {
        try
        {
            var result = await _sync.SmartSyncAsync();
            return result.Success ? SyncOutcome.Ok(dataReplaced: true) : SyncOutcome.Fail(result.ErrorMessage);
        }
        catch (Exception ex) { return SyncOutcome.Fail(ex.Message); }
    }

    private async Task<SyncOutcome> RunPushAsync()
    {
        try
        {
            var result = await _sync.PushAllDataToServerAsync();
            return result.Success ? SyncOutcome.Ok(dataReplaced: false) : SyncOutcome.Fail(result.ErrorMessage);
        }
        catch (Exception ex) { return SyncOutcome.Fail(ex.Message); }
    }

    private async Task<SyncOutcome> RunPullAsync()
    {
        try
        {
            var result = await _sync.SyncAsync();
            return result.Success ? SyncOutcome.Ok(dataReplaced: true) : SyncOutcome.Fail(result.ErrorMessage);
        }
        catch (Exception ex) { return SyncOutcome.Fail(ex.Message); }
    }
}