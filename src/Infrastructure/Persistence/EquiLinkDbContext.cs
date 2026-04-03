using EquiLink.Infrastructure.Persistence.Configurations;
using EquiLink.Infrastructure.Persistence.EventStore;
using EquiLink.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EquiLink.Infrastructure.Persistence;

public class EquiLinkDbContext(
    DbContextOptions<EquiLinkDbContext> options,
    ICurrentFundContext currentFundContext) : DbContext(options)
{
    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrderEventConfiguration());

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
