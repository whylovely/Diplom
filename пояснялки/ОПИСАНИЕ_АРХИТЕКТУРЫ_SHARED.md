# Архитектура Shared-библиотеки

**Shared** — общая библиотека классов, которую подключают и Client, и Server. Содержит DTO (Data Transfer Objects) и перечисления, описывающие формат данных при обмене между клиентом и сервером.

---

## 1. Зачем нужен Shared

Без общей библиотеки одинаковые классы пришлось бы дублировать в клиенте и сервере. При добавлении поля в одном месте и забытии в другом — запросы молча ломаются. Shared — единый источник правды: изменяешь DTO один раз, оба проекта автоматически обновляются через `ProjectReference`.

```
Client.csproj ──► Shared.csproj ◄── Server.csproj
```

---

## 2. Технологии

| Свойство | Значение |
|----------|----------|
| Язык | C# 12 (.NET 8) |
| Тип проекта | Class Library |
| Внешние зависимости | 0 (только стандартная библиотека .NET) |

---

## 3. Структура

```
Shared/
├── Accounts/         AccountDTOs.cs      — DTO и enums для счетов
├── Auth/             AuthDTOs.cs         — Запросы логина/регистрации, ответ с токеном
├── Categories/       CategoryDTOs.cs     — DTO для категорий
├── Transactions/     TransactionDTO.cs   — DTO для транзакций, проводок, MoneyDto
├── Obligations/      ObligationDto.cs    — DTO и enums для обязательств
├── Reports/          ReportDtos.cs       — DTO для отчётов (сводка, по категориям, помесячно)
├── Exchange/         ExchangeRateDto.cs  — DTO курсов валют
├── Sync/             SyncPushRequest.cs  — DTO для Push-синхронизации
└── Shared.csproj
```

---

## 4. Типы DTO

Для каждой сущности используется до трёх DTO:

| Суффикс | Направление | Назначение | Пример |
|---------|-------------|------------|--------|
| `...Dto` | Сервер → Клиент | Ответ (содержит Id) | `AccountDto` |
| `Create...Request` | Клиент → Сервер | Создание (без Id) | `CreateAccountRequest` |
| `Update...Request` | Клиент → Сервер | Обновление (Id в URL) | `UpdateAccountRequest` |

---

## 5. Файлы

### `Auth/AuthDTOs.cs`
- `RegisterRequest(Email, Password)` — регистрация
- `LoginRequest(Email, Password)` — вход
- `AuthResponse(AccessToken)` — ответ с JWT-токеном

### `Accounts/AccountDTOs.cs`
- `AccountKind` — enum: Assets (0), Income (1), Expenses (2)
- `MultiCurrencyType` — enum: Standard (0), MultiCurrency (1)
- `AccountDto(Id, Name, Kind, Currency, AccountType, SecondaryCurrency?, ExchangeRate?)`
- `CreateAccountRequest(Name, Kind, Currency, AccountType?, SecondaryCurrency?, ExchangeRate?)`
- `UpdateAccountRequest(Name, Kind, Currency, AccountType?, SecondaryCurrency?, ExchangeRate?)`

### `Categories/CategoryDTOs.cs`
- `CategoryDto(Id, Name)`
- `CreateCategoryRequest(Name)`
- `UpdateCategoryRequest(Name)`

### `Transactions/TransactionDTO.cs`
- `EntryDirection` — enum: Debit (0), Credit (1)
- `MoneyDto(Amount, Currency)`
- `CreateEntryRequest(AccountId, CategoryId?, Direction, Money)`
- `CreateTransactionRequest(Date, Description?, Entries)`
- `EntryDto(Id, AccountId, CategoryId?, Direction, Money)`
- `TransactionDto(Id, Date, Description?, Entries)`

### `Obligations/ObligationDto.cs`
- `ObligationType` — enum: Debt (0), Credit (1)
- `ObligationDto(Id, Counterparty, Amount, Currency, Type, CreatedAt, DueDate?, IsPaid, PaidAt?, Note?)`
- `CreateObligationRequest(Counterparty, Amount, Currency, Type, DueDate?, Note?)`
- `UpdateObligationRequest(Counterparty, Amount, Currency, Type, DueDate?, IsPaid, Note?)`

### `Reports/ReportDtos.cs`
- `CategoryTotalDto(CategoryId?, CategoryName, Total)`
- `MonthlyTotalDto(Year, Month, Income, Expense)`
- `AccountTurnoverDto(AccountId, AccountName, Currency, Debit, Credit)`
- `SummaryDto(TotalIncome, TotalExpense, Net, ExpenseByCategory, IncomeByCategory)`

### `Exchange/ExchangeRateDto.cs`
- `ExchangeRateDto(Currency, Rate, Date)`

### `Sync/SyncPushRequest.cs`
- `SyncPushRequest(Accounts, Categories, Obligations, Transactions)` — полная перезапись серверных данных из клиента

---

## 6. Перечисления

| Enum | Файл | Значения |
|------|-------|----------|
| `AccountKind` | AccountDTOs.cs | Assets (0), Income (1), Expenses (2) |
| `MultiCurrencyType` | AccountDTOs.cs | Standard (0), MultiCurrency (1) |
| `EntryDirection` | TransactionDTO.cs | Debit (0), Credit (1) |
| `ObligationType` | ObligationDto.cs | Debt (0), Credit (1) |

Хранятся в БД как `int`, в коде используются как именованные значения.
