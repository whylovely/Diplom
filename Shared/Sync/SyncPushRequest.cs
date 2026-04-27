using System.Collections.Generic;
using Shared.Accounts;
using Shared.Categories;
using Shared.Obligations;
using Shared.Transactions;

namespace Shared.Sync;

/// <summary>
/// Полный снепшот данных пользователя для Push-синхронизации (POST /api/sync/push).
/// Сервер удаляет всё текущее и записывает то, что в этом запросе.
/// Шаблоны, группы счетов, курсы валют сюда не входят — они локальные.
/// </summary>
public sealed record SyncPushRequest(
    IReadOnlyList<AccountDto> Accounts,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<ObligationDto> Obligations,
    IReadOnlyList<TransactionDto> Transactions
);
