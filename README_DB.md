# Запуск локальной БД

## Требования
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- .NET 8 SDK

## Быстрый старт

### 1. Поднять PostgreSQL
```bash
cd c:\Users\viner\OneDrive\Рабочий стол\Diplom
docker compose up -d
```
Будет создан контейнер `finance_pg` с PostgreSQL 16.

### 2. Запустить сервер
```bash
cd Server
dotnet run
```
При первом запуске в Development-режиме сервер автоматически:
- Применит все EF-миграции
- Создаст администратора
- Создаст demo-пользователя с тестовыми данными

### 3. Проверить
- Swagger UI: http://localhost:5273/swagger
- Админ-панель: http://localhost:5273/admin/

## Учётные данные

| Роль | Email | Пароль |
|------|-------|--------|
| Admin | admin@finance.local | Admin123 |
| Demo | demo@finance.local | Demo123 |

Demo-пользователь содержит:
- 3 счёта (Карта, Наличные, USDT Wallet)
- 5 категорий (Зарплата, Продукты, Транспорт, Развлечения, Кафе)
- 10 транзакций за последний месяц
- 2 обязательства

## Подключение к БД

| Параметр | Значение |
|----------|----------|
| Host | localhost |
| Port | 5432 |
| Database | finance |
| User | finance_user |
| Password | finance_pass |

## Скрипты

### Резервная копия
```powershell
.\db\scripts\db-backup.ps1
```
Сохраняет в `db/backup/seed.dump`.

### Восстановление
```powershell
.\db\scripts\db-restore.ps1
```
Восстанавливает из `db/backup/seed.dump`.

## Сброс БД
```bash
docker compose down -v
docker compose up -d
dotnet run --project Server
```

## База данных клиента (SQLite)

Обратите внимание: данный файл описывает запуск и работу с **серверной** базой данных (PostgreSQL).
Клиентское десктопное приложение (Avalonia) использует собственную локальную базу данных SQLite (`finance.db`), которая позволяет приложению полноценно работать в офлайн-режиме и выполнять двустороннюю синхронизацию с сервером при наличии сети.
