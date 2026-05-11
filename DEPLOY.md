# Развёртывание сервера в Docker

Это руководство описывает, как запустить сервер и базу данных локально через Docker на **macOS** и **Windows**. Клиент (Avalonia) запускается отдельно, без Docker.

---

## Содержание

1. [Требования](#1-требования)
2. [Установка Docker Desktop](#2-установка-docker-desktop)
   - [macOS](#macos)
   - [Windows](#windows)
3. [Запуск сервера](#3-запуск-сервера)
4. [Проверка работоспособности](#4-проверка-работоспособности)
5. [Запуск клиента](#5-запуск-клиента)
6. [Управление контейнерами](#6-управление-контейнерами)
7. [Удаление файла настроек клиента](#7-удаление-файла-настроек-клиента)
8. [Сброс базы данных](#8-сброс-базы-данных)
9. [Частые проблемы](#9-частые-проблемы)

---

## 1. Требования

| Компонент | Версия |
|-----------|--------|
| Docker Desktop | 4.x и выше |
| .NET SDK | 8.0 (только для клиента) |
| Свободный порт | 5273 (сервер API) |
| Свободный порт | 5432 (PostgreSQL, опционально) |

> **Примечание.** .NET SDK нужен только для запуска клиентского приложения командой `dotnet run`. Сервер целиком работает внутри Docker и .NET на хосте не требует.

---

## 2. Установка Docker Desktop

### macOS

1. Перейдите на [https://www.docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop).
2. Нажмите **Download for Mac**:
   - **Apple Silicon (M1/M2/M3/M4)** — выберите `Mac with Apple Chip`.
   - **Intel** — выберите `Mac with Intel Chip`.
3. Откройте скачанный `.dmg`-файл, перетащите **Docker** в папку **Applications**.
4. Запустите Docker из Applications. При первом запуске система запросит пароль администратора — введите его.
5. В строке меню (верхний правый угол) появится иконка кита 🐳. Дождитесь надписи **Docker Desktop is running**.

**Проверка в Терминале:**
```bash
docker --version
docker compose version
```

---

### Windows

1. Перейдите на [https://www.docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop).
2. Нажмите **Download for Windows**.
3. Запустите скачанный `.exe`-установщик.
4. В процессе установки оставьте включённой опцию **Use WSL 2 instead of Hyper-V** (рекомендуется).
   - Если WSL 2 ещё не установлен, установщик предложит это сделать. Согласитесь и перезагрузите компьютер.
5. После перезагрузки запустите **Docker Desktop** из меню Пуск.
6. В системном трее (правый нижний угол) появится иконка кита 🐳. Дождитесь надписи **Docker Desktop is running**.

**Проверка в PowerShell или cmd:**
```powershell
docker --version
docker compose version
```

> **WSL 2** — обязательный компонент для Docker на Windows. Если его нет, выполните в PowerShell от имени администратора:
> ```powershell
> wsl --install
> ```
> После чего перезагрузите компьютер.

---

## 3. Запуск сервера

Откройте терминал (macOS: **Terminal** / **iTerm2**; Windows: **PowerShell** или **Терминал Windows**) и перейдите в папку проекта:

```bash
# macOS
cd ~/Desktop/Diplom

# Windows (PowerShell)
cd $env:USERPROFILE\Desktop\Diplom
```

Запустите сборку и старт контейнеров:

```bash
docker compose up -d --build
```

Флаг `--build` пересобирает образ сервера из исходников. При последующих запусках, если код не менялся, можно запускать без него:

```bash
docker compose up -d
```

**Что происходит при первом запуске:**
1. Docker скачивает базовые образы `postgres:16` и `mcr.microsoft.com/dotnet/aspnet:8.0` (~500 МБ, только один раз).
2. Собирается образ сервера из исходного кода (~1–2 минуты).
3. Поднимается PostgreSQL, сервер ждёт пока база будет готова (healthcheck), затем стартует.
4. При старте сервер автоматически применяет миграции и создаёт тестовых пользователей.

---

## 4. Проверка работоспособности

### Статус контейнеров

```bash
docker compose ps
```

Оба контейнера должны показывать статус `running` или `Up`:

```
NAME              STATUS          PORTS
finance_pg        Up (healthy)    0.0.0.0:5432->5432/tcp
finance_server    Up              0.0.0.0:5273->8080/tcp
```

### Swagger UI

Откройте в браузере: [http://localhost:5273/swagger](http://localhost:5273/swagger)

Должна открыться интерактивная документация API. Если страница открылась — сервер работает.

### Логи сервера

```bash
# Последние 50 строк
docker compose logs server --tail 50

# В режиме реального времени (Ctrl+C для выхода)
docker compose logs server -f
```

Рабочие логи выглядят примерно так:

```
finance_server  | [DEBUG] Original connection string length: 87
finance_server  | info: Microsoft.Hosting.Lifetime[14]
finance_server  |       Now listening on: http://[::]:8080
finance_server  | info: Microsoft.Hosting.Lifetime[0]
finance_server  |       Application started.
```

---

## 5. Запуск клиента

Клиент запускается на хосте (не в Docker). Требуется **.NET 8 SDK**.

```bash
dotnet run --project Client
```

При первом запуске клиент предложит выбрать базовую валюту. После этого необходимо войти или зарегистрироваться.

**Тестовые учётные данные** (созданы автоматически):

| Роль | Email | Пароль |
|------|-------|--------|
| Администратор | `admin@finance.local` | `Admin123` |
| Демо-пользователь (с данными) | `demo@finance.local` | `Demo123` |

Адрес сервера по умолчанию `http://localhost:5273` — менять не нужно.

---

## 6. Управление контейнерами

| Действие | Команда |
|----------|---------|
| Запустить (без пересборки) | `docker compose up -d` |
| Запустить с пересборкой образа | `docker compose up -d --build` |
| Остановить (данные сохраняются) | `docker compose stop` |
| Остановить и удалить контейнеры | `docker compose down` |
| Посмотреть статус | `docker compose ps` |
| Логи сервера | `docker compose logs server -f` |
| Логи PostgreSQL | `docker compose logs db -f` |

---

## 7. Удаление файла настроек клиента

Клиент хранит настройки (адрес сервера, токен авторизации, базовая валюта) в файле `user_settings.json`. Его нужно удалить если:
- приложение просит войти снова, но токен устарел и вход зависает;
- нужно сбросить адрес сервера на дефолтный (`http://localhost:5273`);
- хотите начать с чистого листа без повторной переустановки.

### macOS

Файл находится по пути:
```
~/Library/Application Support/Diplom/user_settings.json
```

**Через Finder:**
1. Откройте Finder.
2. В строке меню нажмите **Переход → Переход к папке...** (⇧⌘G).
3. Введите путь и нажмите **Перейти**:
   ```
   ~/Library/Application Support/Diplom
   ```
4. Удалите файл `user_settings.json` (в Корзину или ⌘Delete).

**Через Терминал:**
```bash
rm ~/Library/Application\ Support/Diplom/user_settings.json
```

---

### Windows

Файл находится по пути:
```
%AppData%\Diplom\user_settings.json
```

**Через Проводник:**
1. Нажмите **Win + R**, введите в поле:
   ```
   %AppData%\Diplom
   ```
   и нажмите **OK**.
2. Удалите файл `user_settings.json` (клавиша Delete или в корзину).

**Через PowerShell:**
```powershell
Remove-Item "$env:APPDATA\Diplom\user_settings.json"
```

**Через cmd:**
```cmd
del "%AppData%\Diplom\user_settings.json"
```

> После удаления файла при следующем запуске клиент покажет диалог выбора базовой валюты и попросит войти заново.

---

## 8. Сброс базы данных

Полный сброс — удаляет все данные и пересоздаёт базу с нуля:

```bash
docker compose down -v
docker compose up -d --build
```

Флаг `-v` удаляет Docker-том с данными PostgreSQL (`finance_pg_data`). При следующем старте сервер заново применит миграции и создаст тестовых пользователей.

---

## 9. Частые проблемы

### Порт 5273 или 5432 уже занят

**Симптом:** ошибка `port is already allocated` или `address already in use`.

**Решение — найти и остановить процесс:**

```bash
# macOS
sudo lsof -i :5273
sudo lsof -i :5432

# Windows (PowerShell)
netstat -ano | findstr :5273
netstat -ano | findstr :5432
```

Остановите найденный процесс или временно отвяжите порт PostgreSQL от хоста (если он не нужен снаружи) — для этого в `docker-compose.yml` можно убрать строку `- "5432:5432"` у сервиса `db`.

---

### Сервер не может подключиться к базе данных

**Симптом:** в логах `docker compose logs server` — ошибки вида `Connection refused` или `password authentication failed`.

**Решение:** дать базе время стартовать. Healthcheck ждёт до 50 секунд. Если ошибка повторяется — полный сброс:

```bash
docker compose down -v && docker compose up -d --build
```

---

### Docker Desktop не запускается на Windows

**Симптом:** сообщение `WSL 2 installation is incomplete`.

**Решение:** в PowerShell от имени администратора:

```powershell
wsl --install
wsl --update
```

Перезагрузите компьютер. Затем снова запустите Docker Desktop.

---

### `docker compose` не найден (старый Docker)

На очень старых версиях Docker команда пишется через дефис: `docker-compose`. Обновите Docker Desktop до актуальной версии, чтобы использовать `docker compose` (без дефиса).

---

### Образ не пересобирается после изменений в коде

```bash
docker compose up -d --build --force-recreate
```

Флаг `--force-recreate` принудительно пересоздаёт контейнеры даже если конфигурация не изменилась.
