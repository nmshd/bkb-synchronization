﻿using Enmeshed.DevelopmentKit.Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Synchronization.Domain.Entities.Sync;

namespace Synchronization.Infrastructure.Persistence.Database.Configurations
{
    public class SyncRunEntityTypeConfiguration : IEntityTypeConfiguration<SyncRun>
    {
        public void Configure(EntityTypeBuilder<SyncRun> builder)
        {
            builder.HasIndex(x => new {x.CreatedBy, x.Index}).IsUnique();
            builder.HasIndex(x => new {x.CreatedBy, x.FinalizedAt});
            builder.HasIndex(x => x.CreatedBy);

            builder.Ignore(x => x.IsFinalized);

            builder.Property(x => x.CreatedAt);
            builder.Property(x => x.EventCount);

            builder.Property(x => x.Id).HasColumnType($"char({SyncRunId.MAX_LENGTH})");
            builder.Property(x => x.CreatedBy).HasColumnType($"char({IdentityAddress.MAX_LENGTH})");
            builder.Property(x => x.CreatedByDevice).HasColumnType($"char({DeviceId.MAX_LENGTH})");
        }
    }
}
