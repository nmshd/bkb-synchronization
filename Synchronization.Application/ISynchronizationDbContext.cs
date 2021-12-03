﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.Persistence.Database;
using Enmeshed.BuildingBlocks.Application.Pagination;
using Enmeshed.DevelopmentKit.Identity.ValueObjects;
using Synchronization.Domain.Entities;
using Synchronization.Domain.Entities.Sync;

namespace Synchronization.Application
{
    public interface ISynchronizationDbContext : IDbContext
    {
        Task<GetDatawalletModificationsResult> GetDatawalletModifications(IdentityAddress activeIdentity, long? localIndex, PaginationFilter paginationFilter);

        Task<Datawallet> GetDatawalletForInsertion(IdentityAddress owner, CancellationToken cancellationToken);
        Task<Datawallet> GetDatawallet(IdentityAddress owner, CancellationToken cancellationToken);
        Task<long> GetNextExternalEventIndexForIdentity(IdentityAddress identity);
        Task<ExternalEvent> CreateExternalEvent(IdentityAddress owner, ExternalEventType type, object payload);
        Task<SyncRun> GetSyncRun(SyncRunId syncRunId, IdentityAddress createdBy, CancellationToken cancellationToken);
        Task<bool> IsActiveSyncRunAvailable(IdentityAddress createdBy, CancellationToken cancellationToken);
        Task<SyncRun> GetSyncRunAsNoTracking(SyncRunId syncRunId, IdentityAddress createdBy, CancellationToken cancellationToken);
        Task<SyncRun> GetSyncRunWithExternalEvents(SyncRunId syncRunId, IdentityAddress createdBy, CancellationToken cancellationToken);
        Task<SyncRun> GetPreviousSyncRunWithExternalEvents(IdentityAddress createdBy, CancellationToken cancellationToken);
        Task<List<ExternalEvent>> GetUnsyncedExternalEvents(IdentityAddress owner, byte maxErrorCount, CancellationToken cancellationToken);
        Task<(IEnumerable<ExternalEvent> firstPage, int totalRecords)> GetExternalEventsOfSyncRun(PaginationFilter paginationFilter, IdentityAddress owner, SyncRunId syncRunId, CancellationToken cancellationToken);
    }

    public class GetDatawalletModificationsResult
    {
        public IEnumerable<DatawalletModification> Items { get; set; }
        public int TotalNumberOfItems { get; set; }
    }
}
