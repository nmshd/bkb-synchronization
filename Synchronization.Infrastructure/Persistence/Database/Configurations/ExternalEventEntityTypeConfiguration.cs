﻿using System.Dynamic;
using Enmeshed.DevelopmentKit.Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using Synchronization.Domain.Entities.Sync;

namespace Synchronization.Infrastructure.Persistence.Database.Configurations;

public class ExternalEventEntityTypeConfiguration : IEntityTypeConfiguration<ExternalEvent>
{
    public void Configure(EntityTypeBuilder<ExternalEvent> builder)
    {
        builder.HasIndex(x => new {x.Owner, x.Index}).IsUnique();
        builder.HasIndex(x => new {x.Owner, x.SyncRunId});

        builder.Property(x => x.Id).HasColumnType($"char({ExternalEventId.MAX_LENGTH})");
        builder.Property(x => x.SyncRunId).HasColumnType($"char({SyncRunId.MAX_LENGTH})");
        builder.Property(x => x.Owner).HasColumnType($"char({IdentityAddress.MAX_LENGTH})");

        builder.Property(x => x.Type).HasMaxLength(50);
        builder.Property(x => x.CreatedAt);

        builder.Property(x => x.Payload)
            .HasMaxLength(200)
            .HasConversion(
                o => JsonConvert.SerializeObject(o),
                s => JsonConvert.DeserializeObject<ExpandoObject>(s));
    }
}
