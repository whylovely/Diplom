using System.Collections.Generic;
using Shared.Accounts;
using Shared.Categories;
using Shared.Obligations;
using Shared.Transactions;

namespace Shared.Sync;

public sealed record SyncPushRequest(
    IReadOnlyList<AccountDto> Accounts,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<ObligationDto> Obligations,
    IReadOnlyList<TransactionDto> Transactions
);
