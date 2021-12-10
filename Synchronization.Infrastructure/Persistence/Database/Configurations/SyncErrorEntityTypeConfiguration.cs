using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Synchronization.Domain.Entities.Sync;

namespace Synchronization.Infrastructure.Persistence.Database.Configurations;

public class SyncErrorEntityTypeConfiguration : IEntityTypeConfiguration<SyncError>
{
    public void Configure(EntityTypeBuilder<SyncError> builder)
    {
        builder.HasIndex(x => new {x.SyncRunId, x.ExternalEventId}).IsUnique();

        builder.Property(x => x.Id).HasColumnType($"char({SyncErrorId.MAX_LENGTH})");
        builder.Property(x => x.SyncRunId).HasColumnType($"char({SyncRunId.MAX_LENGTH})");
        builder.Property(x => x.ExternalEventId).HasColumnType($"char({ExternalEventId.MAX_LENGTH})");

        builder.Property(x => x.ErrorCode).HasMaxLength(50);
    }
}
