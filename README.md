# 💰 Finance Tracker

> Кроссплатформенное desktop-приложение для учёта личных финансов с поддержкой двойной записи и мультивалютных счетов

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-11.3-8B44AC?logo=avalonia)](https://avaloniaui.net/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?logo=postgresql)](https://postgresql.org/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

## 📋 Оглавление

- [О проекте](#-о-проекте)
- [Технологический стек](#-технологический-стек)
- [Функциональность](#-функциональность)
- [Архитектура](#-архитектура)
- [Установка и запуск](#-установка-и-запуск)
- [Структура проекта](#-структура-проекта)
- [Разработка](#-разработка)
- [API документация](#-api-документация)

---

## 🎯 О проекте

**Finance Tracker** — это современное приложение для управления личными финансами, построенное на принципах **двойной бухгалтерии**. Позволяет вести учёт доходов, расходов, переводов между счетами с полной детализацией и аналитикой.

### Ключевые особенности

- 🌍 **Кроссплатформенность** — работает на Windows, Linux, macOS
- 💱 **Мультивалютность** — поддержка фиатных и криптовалют с автоматической конвертацией
- 📊 **Визуализация данных** — графики по категориям, динамика по месяцам, структура расходов
- 🔐 **Безопасность** — JWT-аутентификация, хеширование паролей
- 🎨 **Современный UI** — Avalonia UI с Fluent Design
- 📈 **Отчётность** — группировка по категориям, детализация по дням, экспорт в CSV

---

## 🛠 Технологический стек

### Backend (Server)

| Компонент | Технология | Версия |
|-----------|------------|--------|
| Framework | **ASP.NET Core** | 8.0 |
| ORM | **Entity Framework Core** | 8.0.8 |
| База данных | **PostgreSQL** | 16 |
| Аутентификация | **JWT** (JwtBearer) | 8.0.23 |
| API документация | **Swagger** (Swashbuckle) | 6.6.2 |

### Frontend (Client)

| Компонент | Технология | Версия |
|-----------|------------|--------|
| UI Framework | **Avalonia UI** | 11.3.11 |
| Архитектура | **MVVM** (CommunityToolkit) | 8.2.1 |
| Графики | **LiveCharts** (SkiaSharp) | 2.0.0-rc6 |
| Тема | **Fluent Design** | 11.3.11 |
| DataGrid | **Avalonia.Controls.DataGrid** | 11.3.11 |

### Shared

- **.NET 8.0** — общие DTO, validation models, enums

---

## ✨ Функциональность

### Реализовано

#### Сервер
- ✅ JWT-аутентификация (регистрация, вход)
- ✅ CRUD операции: счета, категории, транзакции
- ✅ **Мультивалютные счета** (основная + дополнительная валюта, курс конвертации)
- ✅ Двойная запись (Debit/Credit entries с балансировкой)
- ✅ Soft delete для счетов и категорий
- ✅ Swagger UI (автодокументация API)

#### Клиент
- ✅ Управление счетами (создание, редактирование, удаление)
- ✅ Управление категориями доходов и расходов
- ✅ Создание транзакций (расход, доход, перевод между счетами)
- ✅ **Отчёты с группировкой по категориям** и детализацией по дням (Expander-группы)
- ✅ Графики: помесячная динамика, структура расходов (pie chart), top-N категорий
- ✅ Обороты по счетам: остатки на начало/конец периода
- ✅ Баланс на дату
- ✅ Экспорт отчётов в CSV
- ✅ Mock data service для разработки без backend

### В разработке

- 🚧 API обязательств (долги и задолженности)
- 🚧 API курсов валют (интеграция с ЦБ РФ / Open Exchange Rates)
- 🚧 Локальная БД + синхронизация (offline-first режим с SQLite)
- 🚧 Админ-панель для управления пользователями
- 🚧 Деплой на облачный сервер (Render.com / Railway)

---

## 🏗 Архитектура

```
┌─────────────────────────────────────────────────────────────┐
│                       Finance Tracker                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────┐         ┌──────────────┐                 │
│  │   Client     │         │    Server    │                 │
│  │  (Avalonia)  │◄───────►│  (ASP.NET)   │                 │
│  │              │   REST  │              │                 │
│  │  ViewModels  │   API   │ Controllers  │                 │
│  │  ↕           │         │  ↕           │                 │
│  │  Services    │         │  EF Core     │                 │
│  │  ↕           │         │  ↕           │                 │
│  │  Models      │         │  Entities    │                 │
│  └──────────────┘         └───────┬──────┘                 │
│                                   │                        │
│                           ┌───────▼──────┐                 │
│                           │  PostgreSQL  │                 │
│                           │  (Docker)    │                 │
│                           └──────────────┘                 │
│                                                             │
│  ┌──────────────┐                                          │
│  │   Shared     │  (DTO, Enums, Validation)                │
│  └──────────────┘                                          │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Принципы проектирования

- 🎯 **Clean Architecture** — разделение на слои (UI → Services → Domain)
- 📦 **MVVM** на клиенте (CommunityToolkit.Mvvm)
- 🔄 **RESTful API** с JWT аутентификацией
- 💾 **Repository pattern** через EF Core
- 🧩 **Shared DTO** между клиентом и сервером

---

## 🚀 Установка и запуск

### Предварительные требования

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (для PostgreSQL)
- [PostgreSQL](https://www.postgresql.org/download/) (альтернатива Docker)

### 1️⃣ Клонирование репозитория

```bash
git clone https://github.com/yourusername/finance-tracker.git
cd finance-tracker/Diplom
```

### 2️⃣ Запуск базы данных

#### Вариант A: Docker Compose (рекомендуется)

```bash
docker-compose up -d
```

База данных будет доступна на `localhost:5432`:
- **Database:** finance
- **User:** finance_user
- **Password:** finance_pass

#### Вариант B: Локальная PostgreSQL

Создайте БД вручную и обновите строку подключения в `Server/appsettings.json`.

### 3️⃣ Запуск сервера

```bash
cd Server
dotnet restore
dotnet ef database update  # применить миграции
dotnet run
```

Сервер запустится на `https://localhost:7000` (или порт из launchSettings).  
Swagger UI доступен на: `https://localhost:7000/swagger`

### 4️⃣ Запуск клиента

В отдельном терминале:

```bash
cd ../Client
dotnet restore
dotnet run
```

Avalonia-приложение откроется автоматически.

---

## 📁 Структура проекта

```
Diplom/
├── Client/                      # Avalonia UI клиент
│   ├── Models/                  # Модели данных
│   ├── ViewModels/              # MVVM ViewModels
│   ├── Views/                   # AXAML разметка
│   ├── Services/                # DataService, NotificationService
│   └── ПЛАН_КЛИЕНТ.md           # План доработки клиента
│
├── Server/                      # ASP.NET Core API
│   ├── Controllers/             # API контроллеры
│   ├── Entities/                # EF Core entities
│   ├── Data/                    # DbContext
│   ├── Auth/                    # JWT, UserContext
│   ├── Migrations/              # EF миграции
│   └── ПЛАН_СЕРВЕР.md           # План доработки сервера
│
├── Shared/                      # Общие DTO между клиентом/сервером
│   ├── Accounts/                # AccountDto, CreateAccountRequest
│   ├── Categories/              # CategoryDto
│   ├── Transactions/            # TransactionDto, EntryDto
│   └── Auth/                    # LoginRequest, RegisterRequest
│
├── db/                          # SQL-скрипты для инициализации
├── docker-compose.yml           # PostgreSQL контейнер
└── README.md                    # Этот файл
```

---

## 👨‍💻 Разработка

### Создание миграции

```bash
cd Server
dotnet ef migrations add MigrationName
dotnet ef database update
```

### Тестирование API через Swagger

1. Запустите сервер: `dotnet run` в папке `Server`
2. Откройте `https://localhost:7000/swagger`
3. Используйте `/api/auth/register` для создания пользователя
4. Используйте `/api/auth/login` для получения JWT токена
5. Нажмите **Authorize** и вставьте `Bearer YOUR_TOKEN`
6. Тестируйте эндпоинты `/api/accounts`, `/api/categories`, `/api/transactions`

### Mock-данные в клиенте

Клиент использует `MockDS.cs` для разработки UI без backend:

```csharp
// App.axaml.cs
IDataService dataService = new MockDS(); // имитация данных
// IDataService dataService = new ApiDataService(); // реальный API
```

---

## 📡 API документация

### Аутентификация

| Метод | Эндпоинт | Описание |
|-------|----------|----------|
| POST | `/api/auth/register` | Регистрация нового пользователя |
| POST | `/api/auth/login` | Вход (возвращает JWT токен) |

### Счета

| Метод | Эндпоинт | Описание |
|-------|----------|----------|
| GET | `/api/accounts` | Получить все счета пользователя |
| GET | `/api/accounts/{id}` | Получить счёт по ID |
| POST | `/api/accounts` | Создать новый счёт |
| PUT | `/api/accounts/{id}` | Обновить счёт |
| DELETE | `/api/accounts/{id}` | Удалить счёт (soft delete) |

### Категории

| Метод | Эндпоинт | Описание |
|-------|----------|----------|
| GET | `/api/categories` | Получить все категории |
| POST | `/api/categories` | Создать категорию |
| PUT | `/api/categories/{id}` | Обновить категорию |
| DELETE | `/api/categories/{id}` | Удалить категорию |

### Транзакции

| Метод | Эндпоинт | Описание |
|-------|----------|----------|
| GET | `/api/transactions/{id}` | Получить транзакцию по ID |
| POST | `/api/transactions` | Создать транзакцию (2+ entries) |

Подробнее см. Swagger UI: `https://localhost:7000/swagger`