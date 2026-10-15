using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DigitestCrm.Api.Data;
using DigitestCrm.Api.Services;
using DigitestCrm.Api.Services.Ai;

var builder = WebApplication.CreateBuilder(args);

// ---------- Veritabanı (PostgreSQL, Docker: docker compose up -d) ----------
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil.")));

// ---------- İş katmanı ----------
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<ActivityService>();
builder.Services.AddScoped<QuoteService>();
builder.Services.AddScoped<DashboardService>();

// ---------- Yapay zeka ----------
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.Section));
builder.Services.Configure<AssistantOptions>(builder.Configuration.GetSection(AssistantOptions.Section));
builder.Services.AddSingleton(sp => new LocalClock(sp.GetRequiredService<IOptions<AssistantOptions>>().Value));
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PendingActionStore>();   // onay bekleyen işlemler
builder.Services.AddSingleton<ModelHealth>();           // hata veren modelleri geçici atlar
builder.Services.AddHttpClient<AiClient>();
builder.Services.AddScoped<CrmToolbox>();
builder.Services.AddScoped<PromptBuilder>();
builder.Services.AddScoped<FallbackAssistant>();
builder.Services.AddScoped<ChatService>();

// ---------- API ----------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Servis katmanındaki iş kuralı hatalarını anlamlı HTTP cevaplarına çevir
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title) = ex switch
    {
        KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
        ArgumentException => (StatusCodes.Status400BadRequest, ex.Message),
        _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu.")
    };
    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new { status, title });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

// Veritabanı şemasını migration'larla güncelle ve örnek verileri yükle
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
        SeedData.Initialize(db);
    }
    catch (Exception ex) when (ex is Npgsql.NpgsqlException or System.Net.Sockets.SocketException or InvalidOperationException)
    {
        app.Logger.LogCritical(ex,
            "PostgreSQL'e bağlanılamadı. Docker Desktop açık mı? Proje klasöründe 'docker compose up -d' çalıştırın.");
        throw;
    }
}

var ai = app.Services.GetRequiredService<IOptions<AiOptions>>().Value;
app.Logger.LogInformation(ai.IsConfigured
    ? "Asistan: {Provider} / {Model} ile çalışıyor."
    : "Asistan: API anahtarı yok, basit modda çalışıyor. ({Provider} / {Model})", ai.Provider, ai.Model);

app.Run();
