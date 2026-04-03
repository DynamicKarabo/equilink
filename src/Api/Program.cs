using System.Reflection;
using EquiLink.Api.Endpoints;
using EquiLink.Domain.EventStore;
using EquiLink.Infrastructure.Behaviors;
using EquiLink.Infrastructure.Persistence;
using EquiLink.Infrastructure.Compliance;
using EquiLink.Infrastructure.Compliance.Export;
using EquiLink.Infrastructure.DataTier;
using EquiLink.Infrastructure.Persistence.EventStore;
using EquiLink.Infrastructure.ReadRepositories;
using EquiLink.Infrastructure.RiskEngine;
using EquiLink.Infrastructure.Tenancy;
using EquiLink.Shared.Risk;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentFundContext, CurrentFundContext>();

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=equilink;Username=postgres;Password=postgres";

builder.Services.AddSingleton<IConnectionStringProvider, ConnectionStringProvider>();

builder.Services.AddDbContext<EquiLinkDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

builder.Services.AddScoped<IEventStore, EventStore>();

builder.Services.AddScoped<IOrderReadRepository, OrderReadRepository>();

builder.Services.AddSingleton<ICsvExportService, CsvExportService>();
builder.Services.AddSingleton<IPdfExportService, PdfExportService>();
builder.Services.AddScoped<IComplianceAuditService, ComplianceAuditService>();
builder.Services.AddSingleton<IWormArchivalService>(sp =>
    new WormArchivalService(sp.GetRequiredService<IConnectionStringProvider>(), builder.Configuration["AzureBlob:ConnectionString"] ?? "", sp.GetRequiredService<ILogger<WormArchivalService>>()));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

builder.Services.AddSingleton<IRiskStateCache, RedisRiskStateCache>();
builder.Services.AddScoped<IRiskRule, SymbolBlacklistRule>();
builder.Services.AddScoped<IRiskRule, MaxOrderSizeRule>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    cfg.RegisterServicesFromAssembly(typeof(IdempotencyBehavior<,>).Assembly);
    cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
    cfg.AddOpenBehavior(typeof(RiskValidationBehavior<,>));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapHealthChecksEndpoints();

app.MapControllers();

app.Run();
