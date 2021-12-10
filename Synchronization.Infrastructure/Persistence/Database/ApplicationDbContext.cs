using Enmeshed.BuildingBlocks.Application.Extensions;
using Enmeshed.BuildingBlocks.Application.Pagination;
using Enmeshed.BuildingBlocks.Infrastructure.Persistence.Database;
using Enmeshed.DevelopmentKit.Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Synchronization.Application;
using Synchronization.Application.Extensions;
using Synchronization.Domain.Entities;
using Synchronization.Domain.Entities.Sync;
using Synchronization.Infrastructure.Persistence.Database.ValueConverters;

namespace Synchronization.Infrastructure.Persistence.Database;

public class ApplicationDbContext : AbstractDbContextBase, ISynchronizationDbContext
{
    public ApplicationDbContext() { }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }


    public DbSet<Datawallet> Datawallets { get; set; }
    public virtual DbSet<DatawalletModification> DatawalletModifications { get; set; }
    public DbSet<ExternalEvent> ExternalEvents { get; set; }
    public DbSet<SyncRun> SyncRuns { get; set; }
    public DbSet<SyncError> SyncErrors { get; set; }

    public async Task<GetDatawalletModificationsResult> GetDatawalletModifications(IdentityAddress activeIdentity, long? localIndex, PaginationFilter paginationFilter)
    {
        var query = DatawalletModifications
            .FromSqlInterpolated($"SELECT * FROM(SELECT *, ROW_NUMBER() OVER(PARTITION BY ObjectIdentifier, Type, PayloadCategory ORDER BY CreatedAt DESC) AS rank FROM [DatawalletModifications] m1 WHERE CreatedBy = {activeIdentity.StringValue} AND [Index] > {localIndex ?? -1}) AS ignoreDuplicates WHERE rank = 1")
            .AsNoTracking();

        var totalNumberOfItems = await query.CountAsync();

        query = query
            .OrderBy(m => m.Index)
            .Paged(paginationFilter);

        var items = await query.ToListAsync();

        return new GetDatawalletModificationsResult {TotalNumberOfItems = totalNumberOfItems, Items = items};
    }


    public async Task<Datawallet> GetDatawalletForInsertion(IdentityAddress owner, CancellationToken cancellationToken)
    {
        var datawallet = await Datawallets
            .WithLatestModification(owner)
            .OfOwner(owner, cancellationToken);

        return datawallet;
    }

    public async Task<Datawallet> GetDatawallet(IdentityAddress owner, CancellationToken cancellationToken)
    {
        var datawallet = await Datawallets
            .AsNoTracking()
            .OfOwner(owner, cancellationToken);
        return datawallet;
    }

    public async Task<long> GetNextExternalEventIndexForIdentity(IdentityAddress identity)
    {
        var latestIndex = await ExternalEvents
            .WithOwner(identity)
            .OrderByDescending(s => s.Index)
            .Select(s => (long?) s.Index)
            .FirstOrDefaultAsync();

        if (latestIndex == null)
            return 0;

        return latestIndex.Value + 1;
    }

    public async Task<ExternalEvent> CreateExternalEvent(IdentityAddress owner, ExternalEventType type, object payload)
    {
        ExternalEvent externalEvent = null;

        await RunInTransaction(async () =>
        {
            if (externalEvent != null)
                // if the transaction is retried, the old event has to be removed from the DbSet, because a new one with a new index is added
                Set<ExternalEvent>().Remove(externalEvent);

            var nextIndex = await GetNextExternalEventIndexForIdentity(owner);
            externalEvent = new ExternalEvent(type, owner, nextIndex, payload);

            await ExternalEvents.AddAsync(externalEvent);
            await SaveChangesAsync(CancellationToken.None);
        }, new List<int> {DbErrorCodes.INDEX_ALREADY_EXISTS});

        return externalEvent;
    }

    public async Task<SyncRun> GetSyncRun(SyncRunId syncRunId, IdentityAddress createdBy, CancellationToken cancellationToken)
    {
        return await SyncRuns
            .CreatedBy(createdBy)
            .Where(s => s.Id == syncRunId)
            .GetFirst(cancellationToken);
    }

    public async Task<bool> IsActiveSyncRunAvailable(IdentityAddress createdBy, CancellationToken cancellationToken)
    {
        return await SyncRuns
            .AsNoTracking()
            .CreatedBy(createdBy)
            .NotFinalized()
            .Select(s => true)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SyncRun> GetSyncRunAsNoTracking(SyncRunId syncRunId, IdentityAddress createdBy, CancellationToken cancellationToken)
    {
        return await SyncRuns
            .AsNoTracking()
            .CreatedBy(createdBy)
            .Where(s => s.Id == syncRunId)
            .GetFirst(cancellationToken);
    }

    public async Task<SyncRun> GetSyncRunWithExternalEvents(SyncRunId syncRunId, IdentityAddress createdBy, CancellationToken cancellationToken)
    {
        return await SyncRuns
            .CreatedBy(createdBy)
            .Where(s => s.Id == syncRunId)
            .Include(s => s.ExternalEvents)
            .GetFirst(cancellationToken);
    }

    public async Task<SyncRun> GetPreviousSyncRunWithExternalEvents(IdentityAddress createdBy, CancellationToken cancellationToken)
    {
        var previousSyncRun = await SyncRuns
            .Include(s => s.ExternalEvents)
            .CreatedBy(createdBy)
            .OrderByDescending(s => s.Index)
            .FirstOrDefaultAsync(cancellationToken);

        return previousSyncRun;
    }

    public async Task<List<ExternalEvent>> GetUnsyncedExternalEvents(IdentityAddress owner, byte maxErrorCount, CancellationToken cancellationToken)
    {
        var unsyncedEvents = await ExternalEvents
            .WithOwner(owner)
            .Unsynced()
            .WithErrorCountBelow(maxErrorCount)
            .ToListAsync(cancellationToken);

        return unsyncedEvents;
    }

    public async Task<(IEnumerable<ExternalEvent> firstPage, int totalRecords)> GetExternalEventsOfSyncRun(PaginationFilter paginationFilter, IdentityAddress owner, SyncRunId syncRunId, CancellationToken cancellationToken)
    {
        var query = ExternalEvents
            .WithOwner(owner)
            .AssignedToSyncRun(syncRunId);

        var totalRecords = await query.CountAsync(cancellationToken);

        var events = await query
            .OrderBy(d => d.Index)
            .Paged(paginationFilter)
            .ToListAsync(cancellationToken);

        return (events, totalRecords);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.UseValueConverter(new DatawalletIdEntityFrameworkValueConverter());
        builder.UseValueConverter(new DatawalletVersionEntityFrameworkValueConverter());
        builder.UseValueConverter(new DatawalletModificationIdEntityFrameworkValueConverter());
        builder.UseValueConverter(new SyncRunIdEntityFrameworkValueConverter());
        builder.UseValueConverter(new ExternalEventIdEntityFrameworkValueConverter());
        builder.UseValueConverter(new SyncErrorIdEntityFrameworkValueConverter());

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
