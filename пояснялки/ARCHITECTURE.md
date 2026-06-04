# Архитектура проекта: Личный финансовый менеджер

## Общая схема

```
┌─────────────────────────────┐         ┌──────────────────────────────┐
│      CLIENT (Desktop)       │         │        SERVER (REST API)     │
│   Avalonia UI, .NET 8       │◄─HTTP──►│     ASP.NET Core 8, JWT      │
│                             │  + JWT  │                              │
│  Views (XAML)               │         │  Controllers                 │
│    ↕                        │         │    ↕                         │
│  ViewModels (MVVM)          │         │  EF Core 8                   │
│    ↕                        │         │    ↕                         │
│  Services                   │         │  PostgreSQL 16               │
│    ├── ApiService (HTTP)     │         │                              │
│    ├── LocalDbService (SQLite│         │                              │
│    ├── SyncService          │         │                              │
│    └── AuthService (JWT)    │         │                              │
└─────────────────────────────┘         └──────────────────────────────┘
              │                                        │
              └────────── Shared (DTOs) ───────────────┘
```

Сервер — источник правды. Клиент хранит локальную копию в SQLite и синхронизируется с сервером.

---

## Сервер

### Технологии

| Компонент       | Стек                                    |
|-----------------|-----------------------------------------|
| Framework       | ASP.NET Core 8                          |
| ORM             | Entity Framework Core 8                 |
| База данных     | PostgreSQL 16                           |
| Аутентификация  | JWT (HS256), 7 дней TTL                 |
| Документация    | Swagger / Swashbuckle                   |
| Курсы валют     | ЦБ РФ XML API + CoinGecko JSON API      |

### Запуск сервера (`Server/Program.cs`)

При старте сервер выполняет следующие шаги:

1. **Конфигурация JWT** — считывает ключ из `appsettings.json`, настраивает Bearer-аутентификацию.
2. **Подключение к PostgreSQL** — строка подключения из переменной окружения или `appsettings.json`.
3. **Применение миграций** — `db.Database.Migrate()` автоматически накатывает все миграции EF Core.
4. **Seed-данные** — создаёт двух пользователей, если их нет:
   - `admin@finance.local` / `Admin123` (роль Admin)
   - `demo@finance.local` / `Demo123` (роль User) — с тестовыми счетами, категориями, транзакциями и обязательствами.
5. **Запуск Kestrel** — слушает на порту 5273 (по умолчанию).

### База данных и модели (`Server/Data/AppDbContext.cs`)

Используется **двойная запись (double-entry bookkeeping)**. Каждая транзакция содержит минимум две проводки (Entry): одна дебетует счёт, вторая кредитует.

```
UserEntity
  ├── id, email, password_hash, role (User/Admin), is_blocked, created_at

AccountEntity
  ├── id, user_id, name, kind (Assets/Income/Expenses)
  ├── currency (ISO 4217), account_type (Standard/MultiCurrency)
  ├── secondary_currency, exchange_rate
  └── is_deleted, deleted_at  ← мягкое удаление

CategoryEntity
  ├── id, user_id, name
  └── is_deleted, deleted_at  ← мягкое удаление

TransactionEntity
  ├── id, user_id, date, description
  └── Entries[]  ← 1:N

EntryEntity
  ├── id, user_id, transaction_id, account_id, category_id
  └── direction (Debit=0 / Credit=1), amount, currency

ObligationEntity
  ├── id, user_id, counterparty, amount, currency
  ├── type (OwesToMe=0 / IOwe=1)
  ├── created_at, due_date, is_paid, paid_at, note
  └── is_deleted, deleted_at  ← мягкое удаление
```

### Контроллеры и эндпоинты

Все эндпоинты (кроме `/api/auth/*`) требуют JWT-токен в заголовке:
```
Authorization: Bearer <token>
```

Все запросы фильтруются по `userId`, извлечённому из JWT-клеймов (`Server/Auth/UserContext.cs`).

#### AuthController — `/api/auth`
| Метод | Путь | Описание |
|-------|------|----------|
| POST | `/register` | Регистрация нового пользователя |
| POST | `/login` | Вход, возвращает JWT-токен |
| POST | `/admin/login` | Вход только для Admin |

#### AccountsController — `/api/accounts`
| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/` | Список счетов текущего пользователя |
| GET | `/{id}` | Один счёт |
| POST | `/` | Создать счёт |
| PUT | `/{id}` | Обновить счёт |
| DELETE | `/{id}` | Мягкое удаление (проверяет баланс) |

#### CategoriesController — `/api/categories`
| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/` | Список категорий |
| POST | `/` | Создать категорию |
| PUT | `/{id}` | Обновить |
| DELETE | `/{id}` | Мягкое удаление (проверяет использование) |

#### TransactionsController — `/api/transactions`
| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/` | Все транзакции |
| GET | `/{id}` | Одна транзакция |
| POST | `/` | Создать транзакцию (минимум 2 проводки, сумма дебетов = сумма кредитов) |

#### ObligationsController — `/api/obligations`
| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/` | Список долгов/займов |
| POST | `/` | Создать |
| PUT | `/{id}` | Обновить |
| POST | `/{id}/pay` | Отметить оплаченным |
| DELETE | `/{id}` | Мягкое удаление |

#### ReportController — `/api/reports`
| Метод | Путь | Описание |
|-------|------|----------|
| GET | `/summary` | Итого: доходы / расходы / нетто за период |
| GET | `/by-category` | Разбивка по категориям |
| GET | `/monthly` | Тренд по месяцам |
| GET | `/turnover` | Дебет/кредит по каждому счёту |

#### SyncController — `/api/sync`
| Метод | Путь | Описание |
|-------|------|----------|
| POST | `/push` | Полный push данных клиента на сервер (перезаписывает) |

#### ExchangeController
Получает курсы валют от ЦБ РФ (XML) и CoinGecko (крипта). Кешируется на стороне сервера.

#### AdminController
Управление пользователями: просмотр, блокировка. Доступен только роли Admin.

### Паттерны сервера

- **Мягкое удаление** — записи не удаляются физически, `is_deleted = true`.
- **Изоляция пользователей** — каждый запрос фильтруется по `userId` из токена.
- **Ролевая авторизация** — `[Authorize(Roles = "Admin")]` на административных роутах.
- **Двойная запись** — транзакция валидна, только если сумма дебетов равна сумме кредитов.
- **Авто-миграция** — база приводится к актуальной схеме при каждом старте.

---

## Клиент

### Технологии

| Компонент       | Стек                                    |
|-----------------|-----------------------------------------|
| UI Framework    | Avalonia UI 11.3 (кросс-платформ XAML)  |
| Паттерн         | MVVM (CommunityToolkit.Mvvm 8.2)        |
| Локальная БД    | SQLite (Microsoft.Data.Sqlite)          |
| Графики         | LiveCharts2 + SkiaSharp                 |
| Экспорт         | ClosedXML (Excel), CSV, TXT             |

### Структура клиента

```
Client/
├── Program.cs              — Точка входа, инициализация Avalonia
├── App.axaml / App.axaml.cs — Ресурсы, DI, старт главного окна
├── Models/                 — Локальные модели данных
│   ├── Account.cs, AccountGroup.cs
│   ├── Transaction.cs, Entry.cs
│   ├── Category.cs, Obligation.cs
│   ├── Money.cs, CurrencyRate.cs
│   └── JournalRow.cs, CalendarDay.cs, ...
├── ViewModels/             — Логика и состояние страниц
│   ├── MainWindowViewModel.cs   — Навигация между страницами
│   ├── DashboardViewModel.cs
│   ├── AccountsViewModel.cs
│   ├── NewTransactionViewModel.cs
│   ├── JournalViewModel.cs
│   ├── ReportViewModel.cs
│   ├── CalendarViewModel.cs
│   ├── ObligationsViewModel.cs (через ObligationsView)
│   ├── CategoriesViewModel.cs
│   ├── SettingsViewModel.cs
│   ├── CurrenciesViewModel.cs
│   └── DialogWindow/       — ViewModel диалогов
├── Views/                  — XAML-страницы и диалоги
└── Services/               — Бизнес-логика
```

### Сервисы клиента

#### ApiService (`Client/Services/ApiService.cs`)
HTTP-клиент для общения с сервером. Разделён на секции:
- **Ping** — проверка доступности сервера
- **Accounts** — CRUD счетов
- **Categories** — CRUD категорий
- **Transactions** — получение и создание транзакций
- **Obligations** — долги и займы
- **Sync** — отправка данных на сервер

Каждый запрос добавляет заголовок `Authorization: Bearer <token>`.

#### LocalDbService (`Client/Services/LocalDbService.cs`)
Управляет локальной базой SQLite по пути `%AppData%\Diplom\finance.db`.
- Хранит все данные локально для работы без сети
- `ReplaceAllData()` — полностью заменяет локальные данные после синхронизации
- Пересчитывает балансы счетов из проводок (Debit=`+`, Credit=`-`)

#### SyncService (`Client/Services/SyncService.cs`)
Управляет двусторонней синхронизацией:

```
Pull (сервер → клиент):
  GET /api/accounts + /categories + /transactions + /obligations
  → маппинг DTO в локальные модели
  → пересчёт балансов
  → LocalDbService.ReplaceAllData()

Push (клиент → сервер):
  Собрать все локальные данные
  → POST /api/sync/push
  → сервер перезаписывает данные пользователя
```

Дополнительно: дедупликация счетов по имени при слиянии.

#### AuthService
- Логин / регистрация через ApiService
- Сохраняет JWT-токен и данные сессии через SettingsService
- Проверяет валидность токена при старте

#### SettingsService (`Client/Services/SettingsService.cs`)
Хранит настройки приложения: URL сервера, JWT-токен, предпочтения пользователя.

#### CurrencyRateService (`Client/Services/CurrencyRateService.cs`)
Запрашивает и кешириует курсы валют (фиат + крипта).

#### NotificationService (`Client/Services/NotificationService.cs`)
Показывает всплывающие уведомления: info, warning, error.

### Навигация (MainWindowViewModel)

`MainWindowViewModel` переключает активный `ViewModel` — каждая страница отображается через `ContentControl` в `MainWindow.axaml`. Переходы происходят командами (например, `NavigateToAccounts`).

---

## Поток данных: создание транзакции

```
Пользователь заполняет форму (NewTransactionView)
  ↓
NewTransactionViewModel:
  1. Валидация: минимум 2 проводки, максимум 50
  2. Проверка: сумма дебетов = сумма кредитов
  3. Проверка: счёт существует, валюта совпадает
  ↓
ApiService.CreateTransactionAsync():
  POST /api/transactions
  Body: { date, description, entries: [ {accountId, categoryId, direction, money} ] }
  ↓
Server TransactionsController:
  1. Извлекает userId из JWT
  2. Проверяет принадлежность счетов пользователю
  3. Сохраняет TransactionEntity + EntryEntity через EF Core
  ↓
Response: TransactionDto
  ↓
LocalDbService.SaveTransaction() → SQLite
  ↓
UI обновляется через ObservableCollection
```

---

## Файлы с комментариями

### Серверная часть

| Файл | Что комментируется |
|------|--------------------|
| `Server/Program.cs` | Очистка строки подключения, настройка миграций, создание seed-данных |
| `Server/Controllers/AccountsController.cs` | Soft delete, расчёт баланса, пояснение Direction enum |
| `Server/Controllers/ReportController.cs` | Описание логики каждого из 4 отчётов |
| `Server/Controllers/SyncController.cs` | Поток данных: Delete → Insert для каждой сущности |
| `Server/Services/CbrExchangeRateService.cs` | Парсинг XML ЦБ РФ |
| `Server/Migrations/*` | Авто-генерированные комментарии EF Core |

### Клиентская часть

| Файл | Что комментируется |
|------|--------------------|
| `Client/Program.cs` | Инициализация Avalonia |
| `Client/App.axaml.cs` | Регистрация сервисов и DI |
| `Client/Services/ApiService.cs` | Разделители секций: Ping, Accounts, Categories, Transactions, Obligations, Sync |
| `Client/Services/LocalDbService.cs` | Операции с БД, пересчёт балансов |
| `Client/Services/SyncService.cs` | Стратегия синхронизации, маппинг данных, расчёт балансов |
| `Client/Services/NotificationService.cs` | Логика очереди уведомлений |
| `Client/Services/CurrencyRateService.cs` | Кеширование курсов |
| `Client/ViewModels/MainWindowViewModel.cs` | Логика навигации |
| `Client/ViewModels/NewTransactionViewModel.cs` | Валидация проводок |
| `Client/ViewModels/DashboardViewModel.cs` | Подготовка данных для дашборда |
| `Client/ViewModels/AccountsViewModel.cs` | Группировка и сортировка счетов |
| `Client/ViewModels/JournalViewModel.cs` | Фильтрация журнала |
| `Client/ViewModels/ReportViewModel.cs` | Агрегация данных для отчётов |
| `Client/ViewModels/CalendarViewModel.cs` | Генерация сетки календаря |
| `Client/ViewModels/AccountGroupViewModel.cs` | Подсчёт итогов по группам |
| `Client/ViewModels/OperationWithReport/*.cs` | Логика отдельных типов отчётов (Balance, Account, Income, Expense, Monthly, Drop) |
| `Client/Views/MainWindow.axaml.cs` | Инициализация главного окна |
| `Client/Views/DialogViews/SyncConflictDialog.axaml.cs` | Разрешение конфликтов синхронизации |
| `Client/Models/CurrencyRate.cs` | Структура модели курса валюты |
| `Client/ViewModels/DialogWindow/CurrencyRatesDialogViewModel.cs` | Загрузка и отображение курсов |
| `Client/ViewModels/DialogWindow/CurrencySelectionViewModel.cs` | Фильтрация списка валют |
| `Client/ViewModels/CurrenciesViewModel.cs` | Логика страницы валют |

---

## Shared (общая библиотека)

Проект `Shared/` содержит DTO-классы, которые используют **и сервер, и клиент**:

```
Shared/
├── Auth/AuthDTOs.cs            — LoginRequest, RegisterRequest, AuthResponse (JWT)
├── Accounts/AccountDTOs.cs     — AccountDto, CreateAccountRequest
├── Categories/CategoryDTOs.cs  — CategoryDto, CreateCategoryRequest
├── Transactions/TransactionDTO.cs — TransactionDto, EntryDto, MoneyDto
├── Obligations/ObligationDto.cs   — ObligationDto
├── Reports/ReportDtos.cs          — SummaryDto, ByCategoryDto, MonthlyDto, TurnoverDto
├── Exchange/ExchangeRateDto.cs    — ExchangeRateDto
└── Sync/SyncPushRequest.cs        — SyncPushRequest (все данные разом)
```

Это гарантирует, что клиент и сервер работают с одинаковыми структурами данных без дублирования кода.
