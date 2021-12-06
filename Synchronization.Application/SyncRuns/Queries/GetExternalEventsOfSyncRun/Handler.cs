﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Enmeshed.BuildingBlocks.Application.Abstractions.Exceptions;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.UserContext;
using Enmeshed.DevelopmentKit.Identity.ValueObjects;
using MediatR;
using Synchronization.Application.SyncRuns.DTOs;

namespace Synchronization.Application.SyncRuns.Queries.GetExternalEventsOfSyncRun
{
    public class Handler : IRequestHandler<GetExternalEventsOfSyncRunQuery, GetExternalEventsOfSyncRunResponse>
    {
        private readonly DeviceId _activeDevice;
        private readonly IdentityAddress _activeIdentity;
        private readonly ISynchronizationDbContext _dbContext;
        private readonly IMapper _mapper;

        public Handler(ISynchronizationDbContext dbContext, IMapper mapper, IUserContext userContext)
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _activeIdentity = userContext.GetAddress();
            _activeDevice = userContext.GetDeviceId();
        }

        public async Task<GetExternalEventsOfSyncRunResponse> Handle(GetExternalEventsOfSyncRunQuery request, CancellationToken cancellationToken)
        {
            var syncRun = await _dbContext.GetSyncRunAsNoTracking(request.SyncRunId, _activeIdentity, cancellationToken);

            if (syncRun.IsFinalized)
                throw new OperationFailedException(ApplicationErrors.SyncRuns.SyncRunAlreadyFinalized());

            if (syncRun.CreatedByDevice != _activeDevice)
                throw new OperationFailedException(ApplicationErrors.SyncRuns.CannotReadExternalEventsOfSyncRunStartedByAnotherDevice());

            var (firstPage, totalRecords) = await _dbContext.GetExternalEventsOfSyncRun(request.PaginationFilter, _activeIdentity, syncRun.Id, cancellationToken);

            var dtos = _mapper.Map<IEnumerable<ExternalEventDTO>>(firstPage);

            return new GetExternalEventsOfSyncRunResponse(dtos, request.PaginationFilter, totalRecords);
        }
    }
}
