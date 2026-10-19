# EntityCatalog

EntityCatalog — учебный ASP.NET Core Web API для каталога товаров. API поддерживает товары, категории, теги, изображения товаров, публикацию и вход пользователей с ролью `Editor`.

## Технологии

- C# и .NET 10
- ASP.NET Core Web API
- Entity Framework Core 10
- PostgreSQL и Npgsql; для категорий и тегов используется расширение PostgreSQL `citext`
- ASP.NET Core Identity и JWT Bearer-аутентификация на HMAC-SHA256
- xUnit

## Структура

- `Controllers` — HTTP-маршруты API.
- `Services` — операции с товарами, категориями, тегами и аутентификацией.
- `Dtos` — модели запросов и ответов API и их валидация.
- `Products` — сущности каталога.
- `Data` — `AppDbContext` и конфигурация модели EF Core.
- `Security` — модель пользователя и настройки JWT.
- `Migrations` — миграции базы данных.
- `EntityCatalog.Tests` — API-тесты и интеграционные тесты PostgreSQL.

## Требования и настройка

Нужны .NET 10 SDK и PostgreSQL. Пользователь базы данных должен иметь право создать расширение `citext` при применении начальной миграции. Для команд миграции нужен `dotnet-ef` (если инструмент ещё не установлен: `dotnet tool install --global dotnet-ef`).

Приложение читает строку подключения `ConnectionStrings:DefaultConnection` и настройки `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey`. Задайте локальные значения через .NET User Secrets из каталога проекта:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<строка подключения к PostgreSQL>"
dotnet user-secrets set "Jwt:Issuer" "EntityCatalog"
dotnet user-secrets set "Jwt:Audience" "EntityCatalog.Client"
dotnet user-secrets set "Jwt:SigningKey" "<случайный ключ длиной не менее 32 байт UTF-8>"
```

`Jwt:Issuer`, `Jwt:Audience` и `Jwt:SigningKey` обязательны; приложение проверяет настройки при запуске. Не храните пароли базы данных и ключи подписи в файлах конфигурации, которые попадают в репозиторий. Для размещения приложения передавайте эти значения через хранилище секретов среды.

## Миграции и первый пользователь Editor

После настройки подключения проверьте целевую базу данных и примените миграции:

```powershell
dotnet ef database update
```

Миграции создают схему каталога и таблицы ASP.NET Core Identity. Приложение не применяет миграции автоматически.

После применения миграций создайте первого пользователя с ролью `Editor`:

```powershell
dotnet run -- --create-editor
```

Команда запрашивает имя пользователя и пароль в интерактивной консоли. Она создаёт роль `Editor`, если её ещё нет, и не назначает её существующему пользователю. При обычном запуске приложение не создаёт пользователей и роли.

## Запуск и авторизация

Запустите приложение из каталога проекта:

```powershell
dotnet run
```

HTTP-профиль из `Properties/launchSettings.json` использует `http://localhost:5246`; HTTPS-профиль также доступен на `https://localhost:7027`.

Для получения JWT отправьте `POST /api/auth/login` с именем пользователя и паролем. Успешный ответ содержит `accessToken`, действительный 15 минут; неверные учётные данные дают `401 Unauthorized`. Передавайте токен в заголовке `Authorization: Bearer <accessToken>`.

Чтение опубликованных товаров, категорий и тегов доступно без входа. Пользователь с ролью `Editor` также видит неопубликованные товары. Создание, изменение и удаление товаров, категорий и тегов, а также изменение публикации требуют наличия роли `Editor`.

## API

Основные маршруты:

- `/api/auth/login` — вход и получение JWT.
- `/api/products` — список товаров (`GET`) и создание (`POST`). Список поддерживает `search`, `categoryId`, `tagId`, `createdFrom`, `createdTo`, `sort`, `order`, `page` и `pageSize`. Допустимые значения `sort`: `name`, `createdAt`, `updatedAt`; `order`: `asc`, `desc`; `pageSize` — от 1 до 100.
- `/api/products/{id}` — получение, полное обновление (`PUT`) и удаление товара.
- `/api/products/{id}/publication` — изменение публикации через `PUT`.
- `/api/categories` и `/api/tags` — чтение списков (`GET`) и создание (`POST`); маршруты с `/{id}` поддерживают обновление (`PUT`) и удаление (`DELETE`). Запись доступна только `Editor`.

Для создания и полного обновления товара передавайте `externalKey`, `name`, `price`, непустой список существующих `categoryIds` и `images`. `tagIds` может быть пустым; список `images` обязателен, но может быть пустым. Цена должна быть от 0 до `9999999999999999.99`. Для каждого изображения укажите абсолютный HTTP- или HTTPS-URL. В `products.http` приведены примеры запросов; токен и ID созданных категорий, тегов и товаров нужно подставить вручную из ответов API.

## Тесты

В проекте есть API-тесты с SQLite в памяти и интеграционные тесты PostgreSQL; отдельных модульных тестов сервисов нет. API-тесты можно запустить отдельно:

```powershell
dotnet test EntityCatalog.Tests/EntityCatalog.Tests.csproj --filter FullyQualifiedName~ProductAccessTests
```

Для полного набора задайте `ENTITYCATALOG_TEST_POSTGRES` со строкой подключения к выделенной тестовой базе и запустите:

```powershell
$env:ENTITYCATALOG_TEST_POSTGRES = "Host=localhost;Port=5432;Database=entitycatalog_test;Username=<пользователь>;Password=<пароль>"
dotnet test EntityCatalog.Tests/EntityCatalog.Tests.csproj
```

Имя тестовой базы должно быть `entitycatalog_test` или начинаться с `entitycatalog_test_`. Тесты создают в ней временную схему и удаляют её после завершения; подключение должно разрешать создание и удаление схем. Не указывайте рабочую базу данных.
