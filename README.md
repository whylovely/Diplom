# Finance Tracker

Кроссплатформенное desktop-приложение для учёта личных финансов с поддержкой двойной записи, мультивалютных счетов и синхронизации с сервером.

## Технологический стек

### Клиент (Client)

| Компонент | Технология |
|-----------|------------|
| UI Framework | Avalonia UI 11.3 |
| Архитектура | MVVM (CommunityToolkit.Mvvm 8.2) |
| Графики | LiveCharts2 (SkiaSharp) |
| Локальная БД | SQLite (Microsoft.Data.Sqlite) |
| Экспорт | ClosedXML (Excel), CSV, TXT |
| Платформа | .NET 8, C# 12 |

### Сервер (Server)

| Компонент | Технология |
|-----------|------------|
| Framework | ASP.NET Core 8 |
| ORM | Entity Framework Core 8 |
| БД | PostgreSQL 16 |
| Аутентификация | JWT (HS256) |
| Документация API | Swagger (Swashbuckle) |
| Курсы валют | ЦБ РФ (XML) + CoinGecko (JSON) |

### Shared

Общая библиотека DTO и перечислений (.NET 8, без внешних зависимостей).

## Функциональность

### Клиент
- Управление счетами (обычные и мультивалютные), категориями, обязательствами
- Транзакции: расход, доход, перевод между счетами (двойная запись)
- Отчёты с графиками: помесячная динамика, структура расходов, топ категорий, обороты по счетам, баланс на дату
- Экспорт отчётов в Excel, CSV, TXT
- Календарь операций
- Шаблоны частых операций
- Уведомления о просроченных долгах
- Выбор базовой валюты с автоматическим пересчётом
- Курсы валют (фиатные + крипто) с избранным
- Локальная SQLite БД + двусторонняя синхронизация с сервером
- JWT-авторизация (логин, регистрация, выход с очисткой локальных данных)

### Сервер
- REST API: счета, категории, транзакции, обязательства, отчёты, курсы валют
- JWT-аутентификация (регистрация, вход, роли User/Admin)
- Soft delete для счетов и категорий
- Синхронизация (Pull/Push) с разрешением конфликтов
- Админ-панель (веб-интерфейс): управление пользователями, блокировка
- Автоматический seed: администратор + демо-пользователь с тестовыми данными

## Архитектура

```
┌──────────────────┐         ┌──────────────────┐
│   Client         │         │    Server         │
│  (Avalonia UI)   │◄──REST──►  (ASP.NET Core)  │
│                  │   API   │                   │
│  MVVM:           │         │  Controllers      │
│  Views ↔ VM ↔    │         │      ↓            │
│  Services        │         │  EF Core          │
│      ↓           │         │      ↓            │
│  SQLite          │         │  PostgreSQL       │
└──────────────────┘         └──────────────────┘
         │                            │
         └────── Shared (DTO) ────────┘
```

## Установка и запуск

### Требования
- .NET 8.0 SDK
- Docker Desktop (для PostgreSQL)

### 1. Запуск базы данных

```bash
docker compose up -d
```

PostgreSQL будет доступна на `localhost:5432` (Database: `finance`, User: `finance_user`, Password: `finance_pass`).

### 2. Запуск сервера

```bash
cd Server
dotnet run
```

При первом запуске сервер автоматически применит миграции и создаст начальные данные.

- Swagger UI: http://localhost:5273/swagger
- Админ-панель: http://localhost:5273/admin

### 3. Запуск клиента

```bash
cd Client
dotnet run
```

### Учётные данные

| Роль | Email | Пароль |
|------|-------|--------|
| Admin | admin@finance.local | Admin123 |
| Demo | demo@finance.local | Demo123 |

Demo-пользователь содержит 3 счёта, 5 категорий, 10 транзакций и 2 обязательства.

## Структура проекта

```
/
├── Client/                     # Avalonia UI клиент
│   ├── Models/                 # Модели данных (Account, Transaction, Category, ...)
│   ├── ViewModels/             # MVVM ViewModels
│   │   ├── DialogWindow/       # ViewModels диалоговых окон
│   │   └── OperationWithReport/# Логика экспорта отчётов
│   ├── Views/                  # AXAML-разметка интерфейса
│   │   └── DialogViews/        # Всплывающие диалоговые окна
│   ├── Services/               # LocalDbService, ApiService, SyncService, AuthService, ...
│   ├── Assets/                 # Иконки, шрифты
│   ├── Program.cs              # Точка входа
│   └── App.axaml / App.axaml.cs# Инициализация, глобальные стили
│
├── Server/                     # ASP.NET Core Web API
│   ├── Controllers/            # Auth, Accounts, Categories, Transactions,
│   │                           # Obligations, Report, Exchange, Sync, Admin
│   ├── Entities/               # EF Core сущности (User, Account, Category, ...)
│   ├── Data/                   # AppDbContext
│   ├── Services/               # CbrExchangeRateService (курсы ЦБ + CoinGecko)
│   ├── Auth/                   # UserContext (извлечение userId из JWT)
│   ├── Migrations/             # EF Core миграции
│   ├── wwwroot/admin/          # Веб-интерфейс админ-панели
│   ├── Program.cs              # Настройка и запуск сервера
│   └── appsettings.json        # JWT, подключение к БД, seed-данные
│
├── Shared/                     # Общие DTO между клиентом и сервером
│   ├── Accounts/               # AccountDto, CreateAccountRequest, enums
│   ├── Auth/                   # LoginRequest, RegisterRequest, AuthResponse
│   ├── Categories/             # CategoryDto
│   ├── Transactions/           # TransactionDto, EntryDto, MoneyDto
│   ├── Obligations/            # ObligationDto, enums
│   ├── Reports/                # SummaryDto, MonthlyTotalDto, ...
│   ├── Sync/                   # SyncPushRequest
│   └── Exchange/               # ExchangeRateDto
│
├── db/                         # Docker-скрипты для PostgreSQL
├── docker-compose.yml          # PostgreSQL-контейнер (dev)
├── dockerfile                  # Сборка серверного Docker-образа
└── Diplom.slnx                 # Корневой solution (Client + Server + Shared)
```

## Разработка

### Создание миграции

```bash
cd Server
dotnet ef migrations add MigrationName
dotnet ef database update
```

### Сброс БД

```bash
docker compose down -v
docker compose up -d
dotnet run --project Server
```

### Резервная копия / восстановление БД

```powershell
.\db\scripts\db-backup.ps1    # → db/backup/seed.dump
.\db\scripts\db-restore.ps1   # ← db/backup/seed.dump
```