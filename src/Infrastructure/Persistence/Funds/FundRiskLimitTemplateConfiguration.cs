using EquiLink.Domain.Aggregates.Fund;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquiLink.Infrastructure.Persistence.Funds;

public class FundRiskLimitTemplateConfiguration : IEntityTypeConfiguration<FundRiskLimitTemplate>
{
    public void Configure(EntityTypeBuilder<FundRiskLimitTemplate> builder)
    {
        builder.ToTable("fund_risk_limit_templates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.TemplateName)
            .HasColumnName("template_name")
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.MaxOrderSize)
            .HasColumnName("max_order_size")
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(t => t.DailyLossLimit)
            .HasColumnName("daily_loss_limit")
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(t => t.ConcentrationLimit)
            .HasColumnName("concentration_limit")
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.HasIndex(t => t.TemplateName)
            .IsUnique()
            .HasDatabaseName("uq_fund_risk_limit_templates_name");
    }
}
