# Описание архитектуры Shared-библиотеки (подробно)

Проект **Shared** — это **общая библиотека**, которую используют и клиент, и сервер одновременно. Она содержит **контракты** (DTO и перечисления), которые описывают формат данных, передаваемых между клиентом и сервером по сети.

---

## Часть 1. Зачем нужен Shared?

### Проблема без Shared

Представьте, что клиент и сервер — это два человека, которые общаются по почте. Клиент отправляет серверу письмо: «Создай мне счёт с полями: Name, Currency, Kind». Сервер должен **точно знать**, какие поля ожидать, какие типы данных у каждого поля, и в каком формате отвечать.

**Без общей библиотеки** произошло бы следующее:
- В клиенте вы написали бы класс `CreateAccountRequest` с полем `Name` (строка).
- В сервере вы написали бы **такой же** класс `CreateAccountRequest` с полем `Name` (строка).
- Если вы добавите поле `Currency` в серверном классе, но забудете добавить в клиентском — всё **молча сломается**. Клиент будет отправлять запрос без `Currency`, а сервер будет ждать его.

### Решение: Shared

Общие классы (DTO, перечисления) описываются **один раз** в проекте Shared. И клиент, и сервер **ссылаются** на этот проект:

```
┌──────────┐         ┌──────────┐
│  Client  │         │  Server  │
│  (.csproj)│         │  (.csproj)│
└────┬─────┘         └────┬─────┘
     │                     │
     │  ProjectReference   │  ProjectReference
     │                     │
     └────────┬────────────┘
              │
              ▼
        ┌──────────┐
        │  Shared  │
        │ (.csproj) │
        │ DTO-классы│
        │ Enums     │
        └──────────┘
```

Теперь, если вы добавите поле в `CreateAccountRequest` в Shared — оно **автоматически** появится и в клиенте, и в сервере. Компилятор не даст вам забыть что-то обновить.

**Аналогия из жизни:** Shared — это как **стандартный бланк заказа** в ресторане. И официант (клиент), и повар (сервер) используют один и тот же бланк. Если в бланк добавляют новую графу «Аллергии», оба сразу видят её.

---

## Часть 2. Что такое DTO?

**DTO** (Data Transfer Object) — это «контейнер для передачи данных». Это класс, который:
- **Не содержит логики** (никаких методов, вычислений).
- **Содержит только данные** (свойства/поля).
- Используется **исключительно** для передачи данных между клиентом и сервером.

В C# для DTO используется конструкция `record` — это специальный тип, который автоматически сравнивает объекты **по значениям** (а не по ссылке), генерирует `ToString()` и является неизменяемым (immutable).

**Пример:**
```csharp
public sealed record CategoryDto(Guid Id, string Name);
```

Это одна строка, но компилятор автоматически создаёт:
- Конструктор `new CategoryDto(id, name)`.
- Свойства `Id` и `Name` (только для чтения).
- Метод `ToString()` → `CategoryDto { Id = ..., Name = ... }`.
- Сравнение по значениям: `cat1 == cat2` → `true`, если `Id` и `Name` совпадают.

---

## Часть 3. Технологии

| Компонент               | Значение                            |
|--------------------------|-------------------------------------|
| Язык                     | C# 12 (.NET 8)                     |
| Тип проекта              | Библиотека классов (Class Library) |
| Внешние зависимости      | **Никаких** (0 NuGet-пакетов)     |

Shared — это **чистая** библиотека без единой внешней зависимости. Это делает её максимально лёгкой и переносимой.

---

## Часть 4. Структура папок

```
Shared/
├── Accounts/
│   └── AccountDTOs.cs        ← DTO для счетов + перечисления
├── Auth/
│   └── AuthDTOs.cs           ← DTO для регистрации и входа
├── Categories/
│   └── CategoryDTOs.cs       ← DTO для категорий
├── Exchange/
│   └── ExchangeRateDto.cs    ← DTO для курсов валют
├── Obligations/
│   └── ObligationDto.cs      ← DTO для долгов + перечисления
├── Reports/
│   └── ReportDtos.cs         ← DTO для отчётов
├── Sync/
│   └── SyncPushRequest.cs    ← DTO для двусторонней синхронизации (отправка с клиента на сервер)
├── Transactions/
│   └── TransactionDTO.cs     ← DTO для транзакций и проводок
└── Shared.csproj             ← Файл проекта
```

Каждая папка соответствует одному **домену** (области) приложения. Внутри каждой папки — один файл со всеми DTO этого домена.

---

## Часть 5. Подробный разбор каждого файла

### 5.1. `Auth/AuthDTOs.cs` — Аутентификация

```csharp
namespace Shared.Auth;

// Запрос на регистрацию нового пользователя
public sealed record RegisterRequest(string Email, string Password);

// Запрос на вход (логин)
public sealed record LoginRequest(string Email, string Password);

// Ответ сервера после успешной регистрации/входа
public sealed record AuthResponse(string AccessToken);
```

**Как используется:**
1. Клиент формирует `LoginRequest` с email и паролем → отправляет POST на `/api/auth/login`.
2. Сервер принимает `LoginRequest`, проверяет пароль.
3. Если ок — возвращает `AuthResponse` с JWT-токеном внутри.
4. Клиент сохраняет `AccessToken` и прикладывает его ко всем последующим запросам.

---

### 5.2. `Accounts/AccountDTOs.cs` — Счета

```csharp
namespace Shared.Accounts;

// Перечисление: тип счёта
public enum AccountKind
{
    Assets = 0,     // Активы (банковская карта, наличные)
    Income = 1,     // Доходный счёт (зарплата, подработка)
    Expenses = 2    // Расходный счёт (продукты, транспорт)
}

// Перечисление: одновалютный или многовалютный
public enum MultiCurrencyType
{
    Standard = 0,       // Обычный (одна валюта)
    MultiCurrency = 1   // Многовалютный (две валюты)
}

// DTO: данные счёта (ответ сервера)
public sealed record AccountDto(
    Guid Id, string Name, AccountKind Kind, string Currency,
    MultiCurrencyType AccountType, string? SecondaryCurrency, decimal? ExchangeRate);

// DTO: запрос на создание счёта (от клиента)
public sealed record CreateAccountRequest(
    string Name, AccountKind Kind, string Currency,
    MultiCurrencyType AccountType = MultiCurrencyType.Standard,
    string? SecondaryCurrency = null, decimal? ExchangeRate = null);

// DTO: запрос на обновление счёта (от клиента)
public sealed record UpdateAccountRequest(
    string Name, AccountKind Kind, string Currency,
    MultiCurrencyType AccountType = MultiCurrencyType.Standard,
    string? SecondaryCurrency = null, decimal? ExchangeRate = null);
```

**Три типа DTO для одной сущности — зачем?**
- `AccountDto` — **ответ** сервера (содержит `Id`, который генерируется на сервере).
- `CreateAccountRequest` — **запрос** на создание (без `Id`, потому что его ещё нет).
- `UpdateAccountRequest` — **запрос** на обновление (без `Id`, потому что он передаётся в URL: `PUT /api/accounts/{id}`).

Это разделение называется **CQRS-lite**: разные модели для чтения (Dto) и записи (Request).

---

### 5.3. `Categories/CategoryDTOs.cs` — Категории

```csharp
namespace Shared.Categories;

public sealed record CategoryDto(Guid Id, string Name);
public sealed record CreateCategoryRequest(string Name);
public sealed record UpdateCategoryRequest(string Name);
```

Самый простой набор: категория — это просто название (например, «Продукты», «Транспорт», «Зарплата»).

---

### 5.4. `Transactions/TransactionDTO.cs` — Транзакции и проводки

```csharp
namespace Shared.Transactions;

// Денежная сумма с валютой
public sealed record MoneyDto(decimal Amount, string Currency);

// Направление проводки
public enum EntryDirection
{
    Debit = 0,   // Дебет (приход на счёт)
    Credit = 1   // Кредит (уход со счёта)
}

// Запрос на создание одной проводки
public sealed record CreateEntryRequest(
    Guid AccountId,            // На какой счёт
    Guid? CategoryId,          // В какой категории (может быть null для переводов)
    EntryDirection Direction,  // Дебет или Кредит
    MoneyDto Money             // Сумма + валюта
);

// Запрос на создание транзакции (содержит несколько проводок)
public sealed record CreateTransactionRequest(
    DateTimeOffset Date,
    string? Description,
    IReadOnlyList<CreateEntryRequest> Entries  // Список проводок
);

// DTO: одна проводка (ответ сервера)
public sealed record EntryDto(
    Guid Id, Guid AccountId, Guid? CategoryId,
    EntryDirection Direction, MoneyDto Money);

// DTO: транзакция (ответ сервера)
public sealed record TransactionDto(
    Guid Id, DateTimeOffset Date, string? Description,
    IReadOnlyList<EntryDto> Entries);
```

**Ключевая идея:** Одна транзакция = несколько проводок. Например, «Перевод 5000₽ с карты на наличные»:
- Проводка 1: Кредит 5000₽ со счёта «Карта» (деньги уходят).
- Проводка 2: Дебет 5000₽ на счёт «Наличные» (деньги приходят).

---

### 5.5. `Obligations/ObligationDto.cs` — Долги

```csharp
namespace Shared.Obligations;

public enum ObligationType
{
    Debt = 0,    // Я должен кому-то
    Credit = 1   // Мне должны
}

// DTO: данные долга (ответ сервера)
public sealed record ObligationDto(
    Guid Id, string Counterparty, decimal Amount, string Currency,
    ObligationType Type, DateTimeOffset CreatedAt, DateTimeOffset? DueDate,
    bool IsPaid, DateTimeOffset? PaidAt, string? Note);

// Запрос на создание долга
public sealed record CreateObligationRequest(
    string Counterparty, decimal Amount, string Currency,
    ObligationType Type, DateTimeOffset? DueDate, string? Note);

// Запрос на обновление долга
public sealed record UpdateObligationRequest(
    string Counterparty, decimal Amount, string Currency,
    ObligationType Type, DateTimeOffset? DueDate, bool IsPaid, string? Note);
```

---

### 5.6. `Exchange/ExchangeRateDto.cs` — Курсы валют

```csharp
namespace Shared.Exchange;

public sealed record ExchangeRateDto(
    string Currency,       // Код валюты (USD, EUR, BTC...)
    decimal Rate,          // Курс к рублю (например, 95.5 для USD)
    DateTimeOffset Date    // Дата актуальности курса
);
```

Самый простой DTO — курс одной валюты. Сервер возвращает массив/список таких объектов.

---

### 5.7. `Reports/ReportDtos.cs` — Отчёты

```csharp
namespace Shared.Reports;

// Итого по одной категории
public sealed record CategoryTotalDto(Guid? CategoryId, string CategoryName, decimal Total);

// Итого за один месяц
public sealed record MonthlyTotalDto(int Year, int Month, decimal Income, decimal Expense);

// Обороты по одному счёту
public sealed record AccountTurnoverDto(
    Guid AccountId, string AccountName, string Currency,
    decimal Debit, decimal Credit);

// Сводка (общий отчёт)
public sealed record SummaryDto(
    decimal TotalIncome, decimal TotalExpense, decimal Net,
    IReadOnlyList<CategoryTotalDto> ExpenseByCategory,
    IReadOnlyList<CategoryTotalDto> IncomeByCategory);
```

---

### 5.8. `Sync/SyncPushRequest.cs` — Синхронизация (Push)

```csharp
namespace Shared.Sync;

// Комплексный запрос на полную перезапись серверной БД из локальной БД клиента
public sealed record SyncPushRequest(
    IReadOnlyList<AccountDto> Accounts,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<ObligationDto> Obligations,
    IReadOnlyList<TransactionDto> Transactions
);
```

Этот DTO используется для **разрешения конфликтов синхронизации**, когда пользователь решает принудительно отправить свои локальные данные на сервер, полностью затерев серверные.

---

## Часть 6. Типы DTO и их роли

В Shared используются **три вида** DTO. Каждый вид решает свою задачу:

```
┌──────────────────────────────────────────────────────────────────┐
│                                                                  │
│   Клиент                    Сервер                               │
│                                                                  │
│   ┌───────────────┐   POST /api/accounts   ┌────────────────┐   │
│   │ Create...     │ ──────────────────────► │ Контроллер     │   │
│   │ Request       │   (тело запроса JSON)   │ принимает      │   │
│   └───────────────┘                         │ Request        │   │
│                                             │                │   │
│   ┌───────────────┐   200 OK + JSON         │ возвращает     │   │
│   │ ...Dto        │ ◄────────────────────── │ Dto            │   │
│   │ (ответ)       │   (тело ответа)         └────────────────┘   │
│   └───────────────┘                                              │
│                                                                  │
│   ┌───────────────┐   PUT /api/accounts/id                       │
│   │ Update...     │ ──────────────────────► (обновление)         │
│   │ Request       │                                              │
│   └───────────────┘                                              │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

| Суффикс в имени | Направление              | Пример                   | Когда используется       |
|------------------|--------------------------|--------------------------|--------------------------|
| `...Dto`         | Сервер → Клиент (ответ)  | `AccountDto`             | GET-запросы (чтение)     |
| `Create...Request` | Клиент → Сервер (запрос) | `CreateAccountRequest` | POST-запросы (создание)  |
| `Update...Request` | Клиент → Сервер (запрос) | `UpdateAccountRequest` | PUT-запросы (обновление) |

---

## Часть 7. Перечисления (Enums)

Shared также содержит **перечисления** — фиксированные списки допустимых значений:

| Enum               | Где определён            | Значения                                      | Для чего                |
|--------------------|--------------------------|-----------------------------------------------|-------------------------|
| `AccountKind`      | `Accounts/AccountDTOs.cs`| Assets (0), Income (1), Expenses (2)          | Тип счёта               |
| `MultiCurrencyType`| `Accounts/AccountDTOs.cs`| Standard (0), MultiCurrency (1)               | Одно/многовалютный      |
| `EntryDirection`   | `Transactions/TransactionDTO.cs` | Debit (0), Credit (1)                | Направление проводки    |
| `ObligationType`   | `Obligations/ObligationDto.cs`   | Debt (0), Credit (1)                 | Тип долга               |

Перечисления хранятся в базе данных как **числа** (int), но в коде используются как **понятные имена** (`AccountKind.Assets` вместо `0`).

---

## Итого

| Свойство                     | Значение                                                    |
|------------------------------|-------------------------------------------------------------|
| Назначение                   | Общие классы данных для клиента и сервера                   |
| Количество файлов с кодом    | 7 файлов                                                    |
| Внешние зависимости           | 0 (только стандартная библиотека .NET)                      |
| Типы содержимого             | DTO (record), Enums, Request/Response модели                |
| Кто использует               | И `Client.csproj`, и `Server.csproj` через `ProjectReference` |
| Ключевая польза              | Единый источник правды — изменяешь в одном месте, обновляется везде |
