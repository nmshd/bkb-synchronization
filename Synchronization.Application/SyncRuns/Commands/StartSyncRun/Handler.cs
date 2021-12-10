using AutoMapper;
using Enmeshed.BuildingBlocks.Application.Abstractions.Exceptions;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.UserContext;
using Enmeshed.DevelopmentKit.Identity.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Synchronization.Application.SyncRuns.DTOs;
using Synchronization.Domain.Entities;
using Synchronization.Domain.Entities.Sync;
using static Synchronization.Domain.Entities.Datawallet;

namespace Synchronization.Application.SyncRuns.Commands.StartSyncRun;

public class Handler : IRequestHandler<StartSyncRunCommand, StartSyncRunResponse>
{
    private const int MAX_NUMBER_OF_SYNC_ERRORS_PER_EVENT = 3;
    private const int DEFAULT_DURATION = 10;
    private readonly DeviceId _activeDevice;
    private readonly IdentityAddress _activeIdentity;

    private readonly ISynchronizationDbContext _dbContext;
    private readonly IMapper _mapper;

    private CancellationToken _cancellationToken;
    private Datawallet _datawallet;
    private SyncRun _previousSyncRun;
    private StartSyncRunCommand _request;
    private DatawalletVersion _supportedDatawalletVersion;

    public Handler(ISynchronizationDbContext dbContext, IUserContext userContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _activeIdentity = userContext.GetAddress();
        _activeDevice = userContext.GetDeviceId();
    }


    public async Task<StartSyncRunResponse> Handle(StartSyncRunCommand request, CancellationToken cancellationToken)
    {
        _request = request;
        _supportedDatawalletVersion = new DatawalletVersion(_request.SupportedDatawalletVersion);
        _cancellationToken = cancellationToken;
        _datawallet = await _dbContext.GetDatawallet(_activeIdentity, _cancellationToken);

        return request.Type switch
        {
            SyncRunDTO.SyncRunType.DatawalletVersionUpgrade => await StartDatawalletVersionUpgrade(),
            SyncRunDTO.SyncRunType.ExternalEventSync => await StartExternalEventSync(),
            _ => throw new Exception("Given sync run type is not supported.")
        };
    }

    private async Task<StartSyncRunResponse> StartExternalEventSync()
    {
        if (_datawallet == null)
            await HandleMissingDatawallet();

        if (_supportedDatawalletVersion < _datawallet.Version)
            throw new OperationFailedException(ApplicationErrors.Datawallet.InsufficientSupportedDatawalletVersion());

        _previousSyncRun = await _dbContext.GetPreviousSyncRunWithExternalEvents(_activeIdentity, _cancellationToken);

        if (IsPreviousSyncRunStillActive())
        {
            if (!_previousSyncRun.IsExpired)
                throw new OperationFailedException(ApplicationErrors.SyncRuns.CannotStartSyncRunWhenAnotherSyncRunIsRunning());

            await CancelPreviousSyncRun();
        }

        var unsyncedEvents = await _dbContext.GetUnsyncedExternalEvents(_activeIdentity, MAX_NUMBER_OF_SYNC_ERRORS_PER_EVENT, _cancellationToken);

        if (unsyncedEvents.Count == 0)
            return CreateResponse(StartSyncRunStatus.NoNewEvents);

        var newSyncRun = CreateNewSyncRun(unsyncedEvents);

        await _dbContext.Set<SyncRun>().AddAsync(newSyncRun, _cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(_cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (ex.HResult == -2146233088 && ex.Message.Contains("IX_SyncRuns_CreatedBy_Index")) // Index already exists
                throw new OperationFailedException(ApplicationErrors.SyncRuns.CannotStartSyncRunWhenAnotherSyncRunIsRunning());

            throw;
        }

        return CreateResponse(StartSyncRunStatus.Created, newSyncRun);
    }

    private async Task HandleMissingDatawallet()
    {
        if (_supportedDatawalletVersion > 0)
            throw new NotFoundException(nameof(Datawallet));

        // in order to be backwards compatible, a Datawallet has to be created in case that the supportedDatawalletVersion of the 
        // device is 0, because devices with that version will never execute a DatawalletVersionUpgrade sync run, so a datawallet
        // will never be created for them
        _datawallet = new Datawallet(_supportedDatawalletVersion, _activeIdentity);
        _dbContext.Set<Datawallet>().Add(_datawallet);
        await _dbContext.SaveChangesAsync(_cancellationToken);
    }

    private async Task<StartSyncRunResponse> StartDatawalletVersionUpgrade()
    {
        if (_datawallet != null && _supportedDatawalletVersion < _datawallet.Version)
            throw new OperationFailedException(ApplicationErrors.Datawallet.InsufficientSupportedDatawalletVersion());

        _previousSyncRun = await _dbContext.GetPreviousSyncRunWithExternalEvents(_activeIdentity, _cancellationToken);

        if (IsPreviousSyncRunStillActive())
        {
            if (!_previousSyncRun.IsExpired)
                throw new OperationFailedException(ApplicationErrors.SyncRuns.CannotStartSyncRunWhenAnotherSyncRunIsRunning());

            await CancelPreviousSyncRun();
        }

        var newSyncRun = CreateNewSyncRun();

        await _dbContext.Set<SyncRun>().AddAsync(newSyncRun, _cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(_cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (ex.HResult == -2146233088 && ex.Message.Contains("IX_SyncRuns_CreatedBy_Index")) // Index already exists
                throw new OperationFailedException(ApplicationErrors.SyncRuns.CannotStartSyncRunWhenAnotherSyncRunIsRunning());

            throw;
        }

        return CreateResponse(StartSyncRunStatus.Created, newSyncRun);
    }

    private bool IsPreviousSyncRunStillActive()
    {
        return _previousSyncRun is {IsFinalized: false};
    }

    private async Task CancelPreviousSyncRun()
    {
        _previousSyncRun.Cancel();
        _dbContext.Set<SyncRun>().Update(_previousSyncRun);
        await _dbContext.SaveChangesAsync(_cancellationToken);
    }

    private SyncRun CreateNewSyncRun()
    {
        return CreateNewSyncRun(Array.Empty<ExternalEvent>());
    }

    private SyncRun CreateNewSyncRun(IEnumerable<ExternalEvent> events)
    {
        var newIndex = DetermineNextSyncRunIndex();
        var syncRun = new SyncRun(newIndex, _request.Duration ?? DEFAULT_DURATION, _activeIdentity, _activeDevice, events ?? Array.Empty<ExternalEvent>(), _mapper.Map<SyncRun.SyncRunType>(_request.Type));
        return syncRun;
    }

    private long DetermineNextSyncRunIndex()
    {
        if (_previousSyncRun == null)
            return 0;

        return _previousSyncRun.Index + 1;
    }

    private StartSyncRunResponse CreateResponse(StartSyncRunStatus status, SyncRun newSyncRun = null)
    {
        var syncRunDTO = _mapper.Map<SyncRunDTO>(newSyncRun);

        var response = new StartSyncRunResponse
        {
            Status = status,
            SyncRun = syncRunDTO
        };
        return response;
    }
}