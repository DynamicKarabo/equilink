using EquiLink.Domain.Aggregates.Fund;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquiLink.Infrastructure.Persistence.Funds;

public class FundRiskLimitsConfiguration : IEntityTypeConfiguration<FundRiskLimits>
{
    public void Configure(EntityTypeBuilder<FundRiskLimits> builder)
    {
        builder.ToTable("fund_risk_limits");

        builder.HasKey(r => r.FundId);

        builder.Property(r => r.FundId)
            .HasColumnName("fund_id")
            .ValueGeneratedNever();

        builder.Property(r => r.MaxOrderSize)
            .HasColumnName("max_order_size")
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(r => r.DailyLossLimit)
            .HasColumnName("daily_loss_limit")
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(r => r.ConcentrationLimit)
            .HasColumnName("concentration_limit")
            .HasColumnType("decimal(18,8)")
            .IsRequired();
    }
}
