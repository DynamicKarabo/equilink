using EquiLink.Domain.Aggregates.Fund;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquiLink.Infrastructure.Persistence.Funds;

public class FundConfiguration : IEntityTypeConfiguration<Fund>
{
    public void Configure(EntityTypeBuilder<Fund> builder)
    {
        builder.ToTable("funds");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(f => f.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(f => f.ManagerName)
            .HasColumnName("manager_name")
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(f => f.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasOne(f => f.RiskLimits)
            .WithOne()
            .HasForeignKey<FundRiskLimits>("FundId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.Name)
            .IsUnique()
            .HasDatabaseName("uq_funds_name");
    }
}
