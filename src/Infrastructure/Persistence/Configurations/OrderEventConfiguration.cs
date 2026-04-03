using EquiLink.Infrastructure.Persistence.EventStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquiLink.Infrastructure.Persistence.Configurations;

public class OrderEventConfiguration : IEntityTypeConfiguration<OrderEvent>
{
    public void Configure(EntityTypeBuilder<OrderEvent> builder)
    {
        builder.ToTable("order_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.AggregateId)
            .HasColumnName("aggregate_id")
            .IsRequired();

        builder.Property(e => e.FundId)
            .HasColumnName("fund_id")
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(e => e.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(e => e.AggregateId)
            .HasDatabaseName("ix_order_events_aggregate_id");

        builder.HasIndex(e => new { e.AggregateId, e.Version })
            .IsUnique()
            .HasDatabaseName("uq_order_events_aggregate_version");
    }
}
