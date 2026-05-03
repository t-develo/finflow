using System.Text;
using FinFlow.Api.Middleware;
using FinFlow.Domain.Interfaces;
using FinFlow.Infrastructure.Data;
using FinFlow.Infrastructure.Identity;
using FinFlow.Infrastructure.Services;
using FinFlow.Infrastructure.Services.CsvParsing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Security: Validate JWT key is not using the default development value in production
var jwtKeyAtStartup = builder.Configuration["Jwt:Key"];
if (builder.Environment.IsProduction() &&
    (string.IsNullOrWhiteSpace(jwtKeyAtStartup) ||
     jwtKeyAtStartup == "FinFlow-SuperSecretKey-ChangeInProduction-AtLeast32Chars!"))
{
    throw new InvalidOperationException(
        "Jwt:Key must be set to a strong secret value in production. " +
        "Set it via an environment variable: Jwt__Key=<your-secret>. " +
        "Never use the default development key in production.");
}

// Database
builder.Services.AddDbContext<FinFlowDbContext>(options =>
{
    if (builder.Environment.IsDevelopment())
        options.UseInMemoryDatabase("FinFlowDev");
    else
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<FinFlowDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
    };
});

builder.Services.AddAuthorization();

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FinFlow API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Application services
// SE-A 担当サービス（S1-A-001〜S1-A-004）
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

// CSV Parsing (adapter pattern: bank-specific parsers registered as ICsvParser)
// Sprint 2: MufgCsvParser and RakutenCsvParser added (S2-A-002)
builder.Services.AddScoped<ICsvParser, GenericCsvParser>();
builder.Services.AddScoped<ICsvParser, MufgCsvParser>();
builder.Services.AddScoped<ICsvParser, RakutenCsvParser>();
builder.Services.AddScoped<CsvParserFactory>();

// SE-A Sprint 2 サービス
builder.Services.AddScoped<IClassificationRuleService, ClassificationRuleService>();
builder.Services.AddScoped<ICategoryClassifier, CategoryClassifier>();

// SE-B 担当サービス（S1-B-001〜S1-B-004）
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// SE-B Sprint 2 サービス
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IPdfReportGenerator, QuestPdfReportGenerator>();
builder.Services.AddHostedService<NotificationScheduler>();

builder.Services.AddLogging();

var app = builder.Build();

// Middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

// Support Azure App Service / reverse proxy forwarded headers
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Serve static frontend files
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

// Apply EF Core migrations automatically on startup (skip for InMemory provider used in tests)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FinFlowDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }
}

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
