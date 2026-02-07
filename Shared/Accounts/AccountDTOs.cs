namespace Shared.Accounts;

public enum AccountKind
{
    Assets = 0,
    Income = 1,
    Expenses = 2
}

public sealed record AccountDto(Guid Id, string Name, AccountKind Kind, string Currency);
public sealed record CreateAccountRequest(string Name, AccountKind Kind, string Currency);
public sealed record UpdateAccountRequest(string Name, AccountKind Kind, string Currency);