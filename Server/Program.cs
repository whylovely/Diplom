using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Server.Data;
using Server.Entities;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IExchangeRateService, CbrExchangeRateService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                    ?? builder.Configuration.GetConnectionString("db");

// парсинг под render.com
if (!string.IsNullOrEmpty(connectionString) && connectionString.StartsWith("postgres://"))
{
    var databaseUri = new Uri(connectionString);
    var userInfo = databaseUri.UserInfo.Split(':');

    connectionString = $"Host={databaseUri.Host};Port={databaseUri.Port};Database={databaseUri.LocalPath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SslMode=Require;Trust Server Certificate=true;";
}

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(connectionString);
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var issuer = jwtSection["Issuer"]!;
var audience = jwtSection["Audience"]!;
var key = jwtSection["Key"]!;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Finance API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference
                { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); 
    
    try
    {
        // 2. Применяем миграции
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ошибка при миграции базы данных.");
    }

    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    // Авто-миграция в Development (эту часть можно оставить или убрать, 
    // так как мы уже сделали Migrate() выше, но пусть будет)
    if (app.Environment.IsDevelopment())
    {
        await db.Database.MigrateAsync();
        Console.WriteLine("[DB] Migrations applied.");
    }

    // --- Seed: Admin ---
    var adminSection = cfg.GetSection("Admin");
    var adminEmail = adminSection["Email"];
    var adminPassword = adminSection["Password"];

    if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
    {
        var existingAdmin = await db.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
        if (existingAdmin == null)
        {
            var admin = new UserEntity
            {
                Email = adminEmail,
                Role = "Admin"
            };
            var hasher = new PasswordHasher<UserEntity>();
            admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
            db.Users.Add(admin);
            await db.SaveChangesAsync();

            Console.WriteLine($"[Seed] Admin user created: {adminEmail}");
        }
        else if (existingAdmin.Role != "Admin")
        {
            existingAdmin.Role = "Admin";
            await db.SaveChangesAsync();
            Console.WriteLine($"[Seed] Admin role updated for existing user: {adminEmail}");
        }
    }

    // --- Seed: Demo-пользователь ---
    const string demoEmail = "demo@finance.local";
    const string demoPassword = "Demo123";

    if (!await db.Users.AnyAsync(u => u.Email == demoEmail))
    {
        var hasher = new PasswordHasher<UserEntity>();
        var demoUser = new UserEntity
        {
            Email = demoEmail,
            Role = "User"
        };
        demoUser.PasswordHash = hasher.HashPassword(demoUser, demoPassword);
        db.Users.Add(demoUser);
        await db.SaveChangesAsync();

        var uid = demoUser.Id;
        var now = DateTimeOffset.UtcNow;

        // Счета
        var accCard = new AccountEntity { UserId = uid, Name = "Карта", Kind = 0, Currency = "RUB" };
        var accCash = new AccountEntity { UserId = uid, Name = "Наличные", Kind = 0, Currency = "RUB" };
        var accCrypto = new AccountEntity { UserId = uid, Name = "USDT Wallet", Kind = 0, Currency = "USD", AccountType = 1, SecondaryCurrency = "RUB", ExchangeRate = 92.0m };
        db.Accounts.AddRange(accCard, accCash, accCrypto);

        // Категории
        var catSalary = new CategoryEntity { UserId = uid, Name = "Зарплата" };
        var catFood = new CategoryEntity { UserId = uid, Name = "Продукты" };
        var catTransport = new CategoryEntity { UserId = uid, Name = "Транспорт" };
        var catFun = new CategoryEntity { UserId = uid, Name = "Развлечения" };
        var catCafe = new CategoryEntity { UserId = uid, Name = "Кафе" };
        db.Categories.AddRange(catSalary, catFood, catTransport, catFun, catCafe);
        await db.SaveChangesAsync(); // сохраняем чтобы получить Id

        // Транзакции (10 штук за последний месяц)
        void AddTx(DateTimeOffset date, string desc, Guid accId, Guid? catId, decimal amount, string cur, int dirFrom, int dirTo, Guid? accToId = null)
        {
            var tx = new TransactionEntity { UserId = uid, Date = date, Description = desc };
            db.Transactions.Add(tx);
            db.SaveChanges();

            db.Entries.Add(new EntryEntity { UserId = uid, TransactionId = tx.Id, AccountId = accId, CategoryId = catId, Direction = dirFrom, Amount = amount, Currency = cur });
            if (accToId.HasValue)
                db.Entries.Add(new EntryEntity { UserId = uid, TransactionId = tx.Id, AccountId = accToId.Value, CategoryId = catId, Direction = dirTo, Amount = amount, Currency = cur });
        }

        // Доходы: Debit (0) = деньги ПРИХОДЯТ на счёт
        AddTx(now.AddDays(-25), "Зарплата (аванс)", accCard.Id, catSalary.Id, 85000, "RUB", 0, 0);
        AddTx(now.AddDays(-10), "Зарплата (остаток)", accCard.Id, catSalary.Id, 70000, "RUB", 0, 0);

        // Расходы: Credit (1) = деньги УХОДЯТ со счёта
        AddTx(now.AddDays(-28), "Ашан", accCard.Id, catFood.Id, 3500, "RUB", 1, 0);
        AddTx(now.AddDays(-21), "Фрукты на рынке", accCash.Id, catFood.Id, 500, "RUB", 1, 0);
        AddTx(now.AddDays(-18), "Лента", accCard.Id, catFood.Id, 4100, "RUB", 1, 0);
        AddTx(now.AddDays(-12), "Такси", accCard.Id, catTransport.Id, 800, "RUB", 1, 0);
        AddTx(now.AddDays(-8), "Кино", accCard.Id, catFun.Id, 1200, "RUB", 1, 0);
        AddTx(now.AddDays(-5), "Кафе с друзьями", accCard.Id, catCafe.Id, 2500, "RUB", 1, 0);
        AddTx(now.AddDays(-3), "Проездной", accCard.Id, catTransport.Id, 2500, "RUB", 1, 0);
        AddTx(now.AddDays(-1), "Боулинг", accCard.Id, catFun.Id, 2500, "RUB", 1, 0);

        await db.SaveChangesAsync();

        // Обязательства
        db.Obligations.AddRange(
            new ObligationEntity { UserId = uid, Counterparty = "Максим", Amount = 5000, Currency = "RUB", Type = 0, DueDate = now.AddDays(7), Note = "Одолжил до зарплаты" },
            new ObligationEntity { UserId = uid, Counterparty = "Альфа-Банк", Amount = 12400, Currency = "RUB", Type = 1, DueDate = now.AddDays(15), Note = "Льготный период кредитки" }
        );
        await db.SaveChangesAsync();

        Console.WriteLine($"[Seed] Demo user created: {demoEmail} / {demoPassword}");
        Console.WriteLine($"[Seed]   → 3 accounts, 5 categories, 10 transactions, 2 obligations");
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();