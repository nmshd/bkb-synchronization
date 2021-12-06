﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Synchronization.Infrastructure.Persistence;

namespace Synchronization.Infrastructure.Persistence.Database.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20210716084034_Init")]
    partial class Init
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .UseIdentityColumns()
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("ProductVersion", "5.0.0");

            modelBuilder.Entity("Synchronization.Domain.Entities.DatawalletModification", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("char(20)");

                    b.Property<string>("Collection")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("CreatedBy")
                        .IsRequired()
                        .HasColumnType("char(36)");

                    b.Property<string>("CreatedByDevice")
                        .IsRequired()
                        .HasColumnType("char(20)");

                    b.Property<long>("Index")
                        .HasColumnType("bigint");

                    b.Property<string>("ObjectIdentifier")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("PayloadCategory")
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<int>("Type")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("CreatedBy");

                    b.HasIndex("CreatedBy", "Index")
                        .IsUnique();

                    b.ToTable("DatawalletModifications");
                });

            modelBuilder.Entity("Synchronization.Domain.Entities.Sync.ExternalEvent", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("char(20)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<long>("Index")
                        .HasColumnType("bigint");

                    b.Property<string>("Owner")
                        .IsRequired()
                        .HasColumnType("char(36)");

                    b.Property<string>("Payload")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<byte>("SyncErrorCount")
                        .HasColumnType("tinyint");

                    b.Property<string>("SyncRunId")
                        .HasColumnType("char(20)");

                    b.Property<int>("Type")
                        .HasMaxLength(50)
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("SyncRunId");

                    b.HasIndex("Owner", "Index")
                        .IsUnique();

                    b.HasIndex("Owner", "SyncRunId");

                    b.ToTable("ExternalEvents");
                });

            modelBuilder.Entity("Synchronization.Domain.Entities.Sync.SyncError", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("char(20)");

                    b.Property<string>("ErrorCode")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<string>("ExternalEventId")
                        .IsRequired()
                        .HasColumnType("char(20)");

                    b.Property<string>("SyncRunId")
                        .IsRequired()
                        .HasColumnType("char(20)");

                    b.HasKey("Id");

                    b.HasIndex("ExternalEventId");

                    b.HasIndex("SyncRunId", "ExternalEventId")
                        .IsUnique();

                    b.ToTable("SyncErrors");
                });

            modelBuilder.Entity("Synchronization.Domain.Entities.Sync.SyncRun", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("char(20)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("CreatedBy")
                        .IsRequired()
                        .HasColumnType("char(36)");

                    b.Property<string>("CreatedByDevice")
                        .IsRequired()
                        .HasColumnType("char(20)");

                    b.Property<int>("EventCount")
                        .HasColumnType("int");

                    b.Property<DateTime>("ExpiresAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("FinalizedAt")
                        .HasColumnType("datetime2");

                    b.Property<long>("Index")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("CreatedBy");

                    b.HasIndex("CreatedBy", "FinalizedAt");

                    b.HasIndex("CreatedBy", "Index")
                        .IsUnique();

                    b.ToTable("SyncRuns");
                });

            modelBuilder.Entity("Synchronization.Domain.Entities.Sync.ExternalEvent", b =>
                {
                    b.HasOne("Synchronization.Domain.Entities.Sync.SyncRun", "SyncRun")
                        .WithMany("ExternalEvents")
                        .HasForeignKey("SyncRunId");

                    b.Navigation("SyncRun");
                });

            modelBuilder.Entity("Synchronization.Domain.Entities.Sync.SyncError", b =>
                {
                    b.HasOne("Synchronization.Domain.Entities.Sync.ExternalEvent", null)
                        .WithMany("Errors")
                        .HasForeignKey("ExternalEventId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Synchronization.Domain.Entities.Sync.SyncRun", null)
                        .WithMany("Errors")
                        .HasForeignKey("SyncRunId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Synchronization.Domain.Entities.Sync.ExternalEvent", b =>
                {
                    b.Navigation("Errors");
                });

            modelBuilder.Entity("Synchronization.Domain.Entities.Sync.SyncRun", b =>
                {
                    b.Navigation("Errors");

                    b.Navigation("ExternalEvents");
                });
#pragma warning restore 612, 618
        }
    }
}
