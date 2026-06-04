# TESTS.md — описание тестов проекта

Документ описывает наборы автоматических тестов клиента и сервера: что именно проверяется, за что отвечает каждый тест, какие инструменты используются.

---

## 1. Общие определения

- **Unit-тест** — проверка одного класса/метода в изоляции, без внешних зависимостей (БД, сеть). Зависимости заменяются заглушками (fake / stub).
- **Интеграционный тест** — проверка нескольких слоёв приложения вместе (контроллер + EF + middleware JWT + валидация), с in-memory реализацией БД.
- **Fact** — одиночный тест xUnit без параметров.
- **Theory + InlineData** — параметризованный тест: один метод прогоняется с несколькими наборами входных данных.
- **Skip** — тест помечен как пропускаемый с указанием причины (обычно — требует реальной PostgreSQL вместо InMemory EF).
- **IClassFixture\<AppFactory\>** — общий инстанс `WebApplicationFactory` на класс тестов, чтобы не поднимать сервер на каждый `Fact`.
- **FakeDataService** — тестовая реализация `IDataService` для клиента, где все методы, не нужные тесту, бросают `NotImplementedException`.
- **AppFactory** — серверная `WebApplicationFactory<Program>`, подменяющая PostgreSQL на EF InMemory и JWT-ключ на тестовый.

**Итоговое покрытие:**

| Проект          | Тестов | Пройдено | Пропущено | Назначение |
|-----------------|:------:|:--------:|:---------:|------------|
| `Client.Tests`  | 44     | 44       | 0         | Unit-тесты чистых сервисов клиента |
| `Server.Tests`  | 117    | 109      | 8         | Интеграционные тесты Web API |

---

## 2. Client.Tests — тесты клиента

**Стек:** xUnit 2.6, .NET 8, без Avalonia/UI.
**Принцип:** тестируются только «чистые» сервисы — без SQLite, без HttpClient, без View.

### 2.1. `Helpers/FakeDataService.cs`

Заглушка `IDataService` для тестов. Позволяет конфигурировать списки аккаунтов, словари технических счетов категорий, счётчик локальных транзакций и дату последнего изменения. Методы, не используемые в текущих тестах, бросают `NotImplementedException` — это защищает от случайных вызовов.

### 2.2. `Services/TransactionValidatorTests.cs` — 13 тестов

Проверяет `TransactionValidator.Validate(...)` — возвращает `string? error` или `null`.

| Тест | Что проверяет |
|------|---------------|
| `Expense_AllFieldsValid_ReturnsNull` | Корректный расход → ошибок нет |
| `Transfer_DifferentAccounts_ReturnsNull` | Перевод между разными счетами валиден |
| `DebtRepayment_WithObligation_ReturnsNull` | Погашение долга с привязанным обязательством валидно |
| `NullFromAccount_ReturnsError` | Пустой счёт-источник → «Не выбран счет» |
| `ZeroOrNegativeAmount_ReturnsError` (Theory: 0, -5) | Сумма ≤ 0 → «Сумма должна быть больше нуля» |
| `ExpenseWithoutCategory_ReturnsError` | Расход без категории → «Не выбрана категория» |
| `IncomeWithoutCategory_ReturnsError` | Доход без категории → та же ошибка |
| `TransferWithoutToAccount_ReturnsError` | Перевод без счёта-назначения |
| `TransferSameAccount_ReturnsError` | Перевод на тот же счёт запрещён |
| `DebtRepaymentWithoutObligation_ReturnsError` | Погашение без долга запрещено |
| `DebtReceiveWithoutObligation_ReturnsError` | Получение без долга запрещено |
| `TransferDoesNotRequireCategory` | Перевод не требует категории (regression) |

### 2.3. `Services/TransactionBuilderTests.cs` — 12 тестов

Проверяет `TransactionBuilder.Build(...)` — построение списка `Entry` для всех видов транзакций.

| Тест | Что проверяет |
|------|---------------|
| `Build_Expense_CreatesCreditFromAndDebitToExpenseAccount` | Расход: Credit по источнику, Debit по техническому счёту расходов, обе проводки несут CategoryId |
| `Build_Income_CreatesDebitToAndCreditFromIncomeAccount` | Доход: Debit по счёту получателю, Credit по техническому счёту доходов |
| `Build_Transfer_SameCurrency_Succeeds` | Перевод в одной валюте: Credit/Debit, без категории |
| `Build_Transfer_DifferentCurrency_Throws` | Перевод в разных валютах → `InvalidOperationException` («одной валюте») |
| `Build_DebtRepayment_ValidCurrency_Succeeds` | Погашение долга в валюте обязательства → 2 проводки, Credit по источнику |
| `Build_DebtRepayment_CurrencyMismatch_Throws` | Разные валюты долга и источника → ошибка «Валюта долга» |
| `Build_DebtRepayment_NoExpenseAccount_Throws` | Отсутствует технический счёт расходов → ошибка «технический счет расходов» |
| `Build_DebtReceive_ValidCurrency_Succeeds` | Приход по займу: Debit по источнику |
| `Build_DebtReceive_NoIncomeAccount_Throws` | Нет технического счёта доходов → ошибка «технический счет доходов» |
| `Build_None_Throws` | `TxKindChoice.None` → «Неизвестный» вид |
| `Build_EntriesHaveUniqueIds` | Все `Entry.Id` уникальны и не `Guid.Empty` |
| `Build_MoneyIsPassedThroughToAllEntries` | `Money` без изменений копируется во все проводки |

### 2.4. `Services/TemplateServiceTests.cs` — 4 теста

Проверяет фабричный метод `TemplateService.Create(...)`.

| Тест | Что проверяет |
|------|---------------|
| `Create_CopiesAllFields` | Все 7 полей шаблона совпадают с переданными аргументами |
| `Create_AcceptsNullableIds` | Допускаются `null` для `FromAccountId`, `ToAccountId`, `CategoryId` |
| `Create_GeneratesNonEmptyId` | Сгенерированный `Id` не `Guid.Empty` |
| `Create_TwoTemplatesHaveDifferentIds` | Два шаблона подряд имеют разные `Id` |

### 2.5. `Services/SyncOrchestratorTests.cs` — 15 тестов

Проверяет «чистые» типы оркестратора синхронизации — `SyncAnalysis` и `SyncOutcome`.
Сам `SyncOrchestrator.ExecuteAsync` зависит от конкретного `SyncService` с `HttpClient`, поэтому тестируется на уровне выше.

**SyncAnalysis:**

| Тест | Что проверяет |
|------|---------------|
| `Analysis_ServerUnreachable_WhenServerCountNegative` | `ServerCount = -1` → `ServerReachable = false`, диалог не показывается |
| `Analysis_ServerReachable_WhenServerCountZeroOrMore` | `ServerCount ≥ 0` → `ServerReachable = true` |
| `Analysis_NeedsConflictDialog_RespectsThreshold` (Theory: 6 кейсов) | Порог конфликта = 10: равенство/разница ≤ 10 → false, разница > 10 → true |
| `Analysis_NoConflictDialog_WhenServerUnreachable_EvenIfDifferenceHuge` | При недоступном сервере диалог не запускается |

**SyncOutcome (фабричные методы):**

| Тест | Что проверяет |
|------|---------------|
| `Ok_WithoutArgs_SuccessNoReplace` | `Ok()` → Success=true, DataReplaced=false |
| `Ok_WithDataReplaced_SetsFlag` | `Ok(true)` → DataReplaced=true |
| `Fail_WithMessage_SetsError` | `Fail("x")` → Success=false, сохраняет текст |
| `Fail_WithNull_UsesDefaultMessage` | `Fail(null)` → «Неизвестная ошибка» |
| `Cancelled_SetsFlag` | `Cancelled()` → WasCancelled=true, остальные флаги false |
| `Dismissed_SetsFlag` | `Dismissed()` → WasDismissed=true, остальные флаги false |

---

## 3. Server.Tests — тесты сервера

**Стек:** xUnit 2.6, `WebApplicationFactory<Program>`, EF Core InMemory, .NET 8.
**Принцип:** каждый тест поднимает HTTP-клиент к реальному pipeline ASP.NET Core (middleware, авторизация, валидация, контроллеры, EF), но с изолированной InMemory БД на каждый `AppFactory`.

### 3.1. Инфраструктура

**`Helpers/AppFactory.cs`** — `WebApplicationFactory<Program>`:
- Заменяет PostgreSQL на EF InMemory (`UseInMemoryDatabase`).
- Подавляет предупреждение `TransactionIgnoredWarning` (InMemory не поддерживает транзакции).
- Подменяет `IExchangeRateService` → `FakeExchangeRateService`.
- Через `PostConfigure<JwtBearerOptions>` перезаписывает issuer/audience/ключ на тестовые.

**`Helpers/JwtHelper.cs`** — генерация валидного JWT для теста (регистрация + логин, либо прямая эмиссия).

**`Helpers/FakeExchangeRateService.cs`** — стабильные курсы валют: RUB=1, USD=90, EUR=100, USDT=90, BTC=8 000 000.

### 3.2. `AuthTests.cs` — 10 тестов

Регистрация, логин, админ-логин, блокировки.

| Тест | Что проверяет |
|------|---------------|
| `Register_ValidData_Returns200AndToken` | Регистрация с валидными данными возвращает JWT |
| `Register_DuplicateEmail_Returns409` | Повторная регистрация email → конфликт |
| `Register_EmptyEmail_Returns400` | Пустой email → 400 |
| `Register_EmptyPassword_Returns400` | Пустой пароль → 400 |
| `Login_ValidCredentials_Returns200AndToken` | Корректный логин возвращает токен |
| `Login_WrongPassword_Returns401` | Неверный пароль → 401 |
| `Login_UnknownEmail_Returns401` | Неизвестный email → 401 |
| `Login_BlockedUser_Returns401` | Заблокированный пользователь не может войти |
| `AdminLogin_RegularUser_Returns403` | `/api/auth/admin-login` обычному пользователю → 403 |
| `AdminLogin_AdminUser_Returns200AndToken` | Админский логин возвращает токен с ролью admin |

### 3.3. `AccountsTests.cs` — 22 теста

CRUD счетов, изоляция пользователей, мультивалютность, запрет удаления с оборотами.

| Тест | Что проверяет |
|------|---------------|
| `GetAll_WithoutToken_Returns401` | Без JWT — 401 |
| `GetAll_NewUser_ReturnsEmptyList` | Новый пользователь видит пустой список |
| `GetAll_ReturnsOnlyOwnAccounts` | Данные разных пользователей изолированы |
| `Create_ValidAccount_Returns201` | Валидное создание — 201 + объект |
| `Create_DuplicateName_Returns409` | Дубликат имени у одного пользователя — 409 |
| `Create_SameNameDifferentUsers_BothSucceed` | Одинаковые имена у разных пользователей допустимы |
| `Create_EmptyName_Returns400` | Пустое имя — 400 |
| `Create_CurrencyTooLong_Returns400` / `TooShort_Returns400` | Код валюты не 3 символа — 400 |
| `Create_MultiCurrency_Returns201WithFields` | Мультивалютный счёт создаётся со вторичной валютой и курсом |
| `Create_MultiCurrency_SamePrimarySecondary_Returns400` | Одинаковые primary/secondary валюты запрещены |
| `Create_MultiCurrency_ZeroExchangeRate_Returns400` | Нулевой курс — 400 |
| `GetById_Existing_Returns200` / `OtherUser_Returns404` / `NotFound_Returns404` | Получение по id + изоляция |
| `Update_ValidData_Returns200` / `DuplicateName_Returns409` | Обновление + проверка уникальности |
| `Delete_AccountWithNoEntries_Returns204` | Удаление неиспользуемого счёта |
| `Delete_AccountWithNonZeroBalance_Returns400` | Счёт с ненулевым балансом удалить нельзя |
| `Delete_AccountWithZeroBalance_ButHasEntries_Returns400` | Есть проводки → удалить нельзя (нужен force) |
| `Delete_WithForce_Returns204` | `?force=true` удаляет даже с оборотами |
| `Delete_NotFound_Returns404` | Несуществующий id |

### 3.4. `CategoriesTests.cs` — 14 тестов

CRUD категорий, изоляция, запрет удаления используемой категории.

| Тест | Что проверяет |
|------|---------------|
| `GetAll_WithoutToken_Returns401` | Без токена — 401 |
| `GetAll_NewUser_ReturnsEmpty` | Пустой список у нового пользователя |
| `GetAll_ReturnsOnlyOwnCategories` | Изоляция между пользователями |
| `Create_ValidName_Returns201` / `DuplicateName_Returns409` / `EmptyName_Returns400` | Создание + валидация |
| `Create_SameNameDifferentUsers_BothSucceed` | Одинаковые имена у разных пользователей разрешены |
| `Update_ValidName_Returns200` / `DuplicateName_Returns409` / `OtherUsersCategory_Returns404` | Обновление + изоляция |
| `Delete_Unused_Returns204` | Неиспользуемая категория удаляется |
| `Delete_UsedInTransaction_Returns400` | Категория с транзакциями — 400 |
| `Delete_UsedInTransaction_WithForce_Returns204` | `?force=true` снимает ограничение |
| `Delete_NotFound_Returns404` | Несуществующий id |

### 3.5. `TransactionsTests.cs` — 17 тестов

Двойная запись (Debit = Credit), валидация ссылок, изоляция.

| Тест | Что проверяет |
|------|---------------|
| `GetAll_WithoutToken_Returns401` / `NewUser_ReturnsEmpty` / `ReturnsOnlyOwnTransactions` | Токен + изоляция |
| `Create_OneEntry_Returns400` | Одна проводка запрещена (минимум 2) |
| `Create_MoreThan50Entries_Returns400` | Верхний лимит — 50 |
| `Create_UnbalancedEntries_Returns400` | Сумма Debit ≠ сумма Credit — 400 |
| `Create_ZeroAmount_Returns400` | Нулевая сумма проводки — 400 |
| `Create_InvalidAccountId_Returns400` | Несуществующий счёт — 400 |
| `Create_OtherUsersAccount_Returns400` | Чужой счёт — 400 (не утечка) |
| `Create_DifferentCurrencyAccounts_Returns400` | Разные валюты при прямой проводке — 400 |
| `Create_InvalidCategoryId_Returns400` | Несуществующая категория — 400 |
| `Create_BalancedTwoEntries_Returns201` | Корректная транзакция принимается |
| `Create_WithCategory_Returns201` | Транзакция с категорией |
| `Create_AppendedToList` | После создания транзакция появляется в `GetAll` |
| `GetById_Existing_Returns200` / `OtherUser_Returns404` / `NotFound_Returns404` | Получение + изоляция |

### 3.6. `ObligationsTests.cs` — 16 тестов

CRUD долгов/займов, валидация сумм и валют, пометка как оплаченного.

| Тест | Что проверяет |
|------|---------------|
| `GetAll_WithoutToken_Returns401` / `NewUser_ReturnsEmpty` / `ReturnsOnlyOwn` | Токен + изоляция |
| `Create_Debt_Returns201` / `Create_Credit_Returns201` | Оба направления (я должен / мне должны) |
| `Create_EmptyCounterparty_Returns400` | Контрагент обязателен |
| `Create_InvalidCurrency_Returns400` | Некорректный код валюты |
| `Create_ZeroAmount_Returns400` / `NegativeAmount_Returns400` | Сумма > 0 |
| `Update_ValidData_Returns200` / `OtherUsersObligation_Returns404` | Обновление + изоляция |
| `MarkAsPaid_Unpaid_Returns204` | Пометить непогашенный долг как оплаченный |
| `MarkAsPaid_AlreadyPaid_Returns400` | Повторная пометка — 400 |
| `MarkAsPaid_NotFound_Returns404` | Неизвестный id |
| `Delete_Obligation_Returns204` / `NotFound_Returns404` | Удаление |

### 3.7. `AdminTests.cs` — 13 тестов

Админские эндпоинты управления пользователями.

| Тест | Что проверяет |
|------|---------------|
| `GetUsers_WithoutToken_Returns401` | Без токена — 401 |
| `GetUsers_RegularUser_Returns403` | Обычный пользователь — 403 |
| `GetUsers_Admin_Returns200` | Админ видит список |
| `GetUser_ExistingId_Returns200` / `NotFound_Returns404` | Детали пользователя |
| `Block_RegularUser_Returns204` | Блокировка обычного |
| `Block_AdminUser_Returns400` | Нельзя заблокировать админа |
| `Block_NotFound_Returns404` | Блокировка несуществующего |
| `Unblock_BlockedUser_Returns204` / `NotFound_Returns404` | Разблокировка |
| `Delete_RegularUser_Returns204` | Удаление обычного |
| `Delete_AdminUser_Returns400` | Админа удалить нельзя |
| `Delete_NotFound_Returns404` | Несуществующий id |

### 3.8. `ExchangeTests.cs` — 11 тестов

Курсы валют и конвертация.

| Тест | Что проверяет |
|------|---------------|
| `GetRates_WithoutToken_Returns401` / `Convert_WithoutToken_Returns401` | Требуется JWT |
| `GetRates_Returns200WithList` | Список курсов |
| `GetRates_ContainsExpectedCurrencies` | Содержит RUB, USD, EUR, USDT, BTC |
| `GetRates_AllRatesPositive` | Все курсы > 0 |
| `Convert_UsdToRub_Returns200` / `RubToUsd_Returns200` / `UsdToEur_Returns200` | Базовая конвертация |
| `Convert_SameCurrency_Returns1to1` | USD→USD = 1 |
| `Convert_UnknownFromCurrency_Returns400` / `UnknownToCurrency_Returns400` | Неизвестный код — 400 |

### 3.9. `ReportsTests.cs` — 8 тестов (3 активных + 5 пропущено)

Отчёты: summary, by-category, monthly, turnover.

| Тест | Что проверяет |
|------|---------------|
| `Reports_WithoutToken_Returns401` (Theory) | Все 4 endpoint'а требуют JWT |
| `ByCategory_WithoutToken_Returns401` | Защита авторизации |
| `ByCategory_KindAssets_Returns400` | Нельзя запросить отчёт по `Assets` (только Expense/Income) |
| ⏭ `Summary_NoTransactions_Returns200WithZeroes` | Skip: требует PostgreSQL (JOIN Entry→Account→Kind) |
| ⏭ `ByCategory_KindExpenses_Returns200` | Skip: та же причина |
| ⏭ `ByCategory_KindIncome_Returns200` | Skip |
| ⏭ `Monthly_NoTransactions_Returns200Empty` | Skip: JOIN Entry→Transaction→Date |
| ⏭ `Turnover_NoTransactions_Returns200Empty` | Skip: JOIN Entry→Account→Name |

### 3.10. `SyncTests.cs` — 4 теста (1 активный + 3 пропущено)

Push всего дерева данных клиента на сервер (замещение).

| Тест | Что проверяет |
|------|---------------|
| `Push_WithoutToken_Returns401` | Защита авторизации |
| ⏭ `Push_EmptyPayload_ClearsAllUserData` | Skip: InMemory не поддерживает `ExecuteDeleteAsync` |
| ⏭ `Push_ValidData_ReplacesServerData` | Skip: та же причина |
| ⏭ `Push_IsolatedBetweenUsers` | Skip: та же причина |

---

## 4. Запуск тестов

```bash
# Клиент
dotnet test Client.Tests
# Ожидается: 44 passed, 0 failed, 0 skipped

# Сервер
dotnet test Server.Tests
# Ожидается: 109 passed, 0 failed, 8 skipped
```

Для прогона `[Skip]`-тестов нужно переключить `AppFactory` на реальную PostgreSQL и убрать атрибуты `Skip`.