using EquiLink.Domain.Aggregates.Fund;
using EquiLink.Infrastructure.Persistence.Configurations;
using EquiLink.Infrastructure.Persistence.EventStore;
using EquiLink.Infrastructure.Persistence.Funds;
using EquiLink.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EquiLink.Infrastructure.Persistence;

public class EquiLinkDbContext(
    DbContextOptions<EquiLinkDbContext> options,
    ICurrentFundContext currentFundContext) : DbContext(options)
{
    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();
    public DbSet<Fund> Funds => Set<Fund>();
    public DbSet<FundRiskLimits> FundRiskLimits => Set<FundRiskLimits>();
    public DbSet<FundRiskLimitTemplate> FundRiskLimitTemplates => Set<FundRiskLimitTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrderEventConfiguration());
        modelBuilder.ApplyConfiguration(new FundConfiguration());
        modelBuilder.ApplyConfiguration(new FundRiskLimitsConfiguration());
        modelBuilder.ApplyConfiguration(new FundRiskLimitTemplateConfiguration());

        modelBuilder.Entity<OrderEvent>(builder =>
        {
            builder.HasQueryFilter(e => e.FundId == currentFundContext.FundId);
        });
    }

    public IQueryable<T> IgnoreTenantFilter<T>() where T : class
    {
        return Set<T>().IgnoreQueryFilters();
    }
}
