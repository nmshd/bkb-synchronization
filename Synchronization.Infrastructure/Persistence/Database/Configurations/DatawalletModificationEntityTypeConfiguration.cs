using Enmeshed.DevelopmentKit.Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Synchronization.Domain.Entities;

namespace Synchronization.Infrastructure.Persistence.Database.Configurations;

public class DatawalletModificationEntityTypeConfiguration : IEntityTypeConfiguration<DatawalletModification>
{
    public void Configure(EntityTypeBuilder<DatawalletModification> builder)
    {
        builder.HasIndex(p => new {p.CreatedBy, p.Index}).IsUnique();
        builder.HasIndex(p => p.CreatedBy);

        builder.Property(x => x.Id).HasColumnType($"char({DatawalletModificationId.MAX_LENGTH})");
        builder.Property(x => x.CreatedBy).HasColumnType($"char({IdentityAddress.MAX_LENGTH})");
        builder.Property(x => x.CreatedByDevice).HasColumnType($"char({DeviceId.MAX_LENGTH})");

        builder.Property(x => x.Collection).HasMaxLength(50);
        builder.Property(x => x.ObjectIdentifier).HasMaxLength(100);
        builder.Property(x => x.PayloadCategory).HasMaxLength(50);
        builder.Property(x => x.Type);

        builder.Ignore(x => x.EncryptedPayload);
    }
}