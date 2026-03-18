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

---

## 🎯 О проекте

**Finance Tracker** — это современное приложение для управления личными финансами, построенное на принципах **двойной бухгалтерии**. Позволяет вести учёт доходов, расходов, переводов между счетами с полной детализацией и аналитикой.

### Ключевые особенности

- 🌍 **Кроссплатформенность** — работает на Windows, Linux, macOS
- 💱 **Мультивалютность** — поддержка фиатных и криптовалют с автоматической конвертацией
- 📊 **Визуализация данных** — графики по категориям, динамика по месяцам, структура расходов
- 🔐 **Безопасность** — JWT-аутентификация, хеширование паролей
- 🎨 **Современный UI** — Avalonia UI
- 📈 **Отчётность** — группировка по категориям, детализация по дням, экспорт в CSV, Excel, txt

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
| DataGrid | **Avalonia.Controls.DataGrid** | 11.3.11 |

### Shared

- **.NET 8.0** — общие DTO, validation models, enums

---

## ✨ Функциональность

### Реализовано

#### Сервер
- ✅ JWT-аутентификация (регистрация, вход)
- ✅ CRUD операции: счета, категории, транзакции, обязательства
- ✅ **Мультивалютные счета** (основная + дополнительная валюта, курс конвертации)
- ✅ Двойная запись (Debit/Credit entries с балансировкой)
- ✅ API курсы валют
- ✅ Soft delete для счетов и категорий
- ✅ Админ-панель для управления пользователями
- ✅ Swagger UI (автодокументация API)

#### Клиент
- ✅ Управление счетами (создание, редактирование, удаление)
- ✅ Управление категориями доходов и расходов
- ✅ Создание транзакций (расход, доход, перевод между счетами)
- ✅ Управление обязательствами
- ✅ Отчёты с группировкой по категориям и детализацией по дням (Expander-группы)
- ✅ Графики: помесячная динамика, структура расходов (pie chart), top-N категорий
- ✅ Обороты по счетам: остатки на начало/конец периода
- ✅ Баланс на дату
- ✅ Экспорт отчётов
- ✅ Локальная БД + синхронизация

### В разработке

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

```bash
docker-compose up -d
```

База данных будет доступна на `localhost:5432`:
- **Database:** finance
- **User:** finance_user
- **Password:** finance_pass

### 3️⃣ Запуск сервера

```bash
cd Server
dotnet restore
dotnet ef database update  # применить миграции
dotnet run
```

Сервер запустится на `https://localhost:5432` (или порт из launchSettings).  
Swagger UI доступен на: `https://localhost:5432/swagger`. 
Админ панель доступна на: `https://localhost:5432/admin`. 

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
│
├── Server/                      # ASP.NET Core API
│   ├── Controllers/             # API контроллеры
│   ├── Entities/                # EF Core entities
│   ├── Data/                    # DbContext
│   ├── Auth/                    # JWT, UserContext
│   ├── Migrations/              # EF миграции
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