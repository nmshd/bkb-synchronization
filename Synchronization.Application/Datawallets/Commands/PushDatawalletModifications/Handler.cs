using AutoMapper;
using Enmeshed.BuildingBlocks.Application.Abstractions.Exceptions;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.EventBus;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.Persistence.BlobStorage;
using Enmeshed.BuildingBlocks.Application.Abstractions.Infrastructure.UserContext;
using Enmeshed.DevelopmentKit.Identity.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Synchronization.Application.IntegrationEvents.Outgoing;
using Synchronization.Application.SyncRuns.Commands.StartSyncRun;
using Synchronization.Domain.Entities;
using static Synchronization.Domain.Entities.Datawallet;

namespace Synchronization.Application.Datawallets.Commands.PushDatawalletModifications;

public class Handler : IRequestHandler<PushDatawalletModificationsCommand, PushDatawalletModificationsResponse>
{
    private readonly DeviceId _activeDevice;
    private readonly IdentityAddress _activeIdentity;
    private readonly IBlobStorage _blobStorage;
    private readonly ISynchronizationDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly IMapper _mapper;
    private CancellationToken _cancellationToken;
    private Datawallet _datawallet;
    private PushDatawalletModificationsCommand _request;
    private DatawalletVersion _supportedDatawalletVersion;

    public Handler(ISynchronizationDbContext dbContext, IUserContext userContext, IMapper mapper, IBlobStorage blobStorage, IEventBus eventBus)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _blobStorage = blobStorage;
        _eventBus = eventBus;
        _activeIdentity = userContext.GetAddress();
        _activeDevice = userContext.GetDeviceId();
    }

    public async Task<PushDatawalletModificationsResponse> Handle(PushDatawalletModificationsCommand request, CancellationToken cancellationToken)
    {
        _request = request;
        _supportedDatawalletVersion = new DatawalletVersion(_request.SupportedDatawalletVersion);
        _cancellationToken = cancellationToken;

        var isActiveSyncRunAvailable = await _dbContext.IsActiveSyncRunAvailable(_activeIdentity, cancellationToken);

        if (isActiveSyncRunAvailable)
            throw new OperationFailedException(ApplicationErrors.Datawallet.CannotPushModificationsDuringActiveSyncRun());

        _datawallet = await _dbContext.GetDatawalletForInsertion(_activeIdentity, cancellationToken);

        if (_datawallet == null)
            await HandleMissingDatawallet();

        if (_supportedDatawalletVersion < _datawallet.Version)
            throw new OperationFailedException(ApplicationErrors.Datawallet.InsufficientSupportedDatawalletVersion());

        var modifications = await CreateModifications();

        PublishIntegrationEvent();

        var responseItems = _mapper.Map<PushDatawalletModificationsResponseItem[]>(modifications);
        return new PushDatawalletModificationsResponse {Modifications = responseItems, NewIndex = responseItems.Max(i => i.Index)};
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

    private async Task<DatawalletModification[]> CreateModifications()
    {
        EnsureDeviceIsUpToDate();

        var newModifications = new List<DatawalletModification>(_request.Modifications.Length);

        foreach (var modificationDto in _request.Modifications)
        {
            var newModification = _datawallet.AddModification(
                _mapper.Map<DatawalletModificationType>(modificationDto.Type),
                new DatawalletVersion(modificationDto.DatawalletVersion),
                modificationDto.Collection,
                modificationDto.ObjectIdentifier,
                modificationDto.PayloadCategory,
                modificationDto.EncryptedPayload,
                _activeDevice);

            newModifications.Add(newModification);
        }

        _dbContext.Set<Datawallet>().Update(_datawallet);

        var modificationsArray = newModifications.ToArray();

        await Save(modificationsArray);

        return modificationsArray;
    }

    private void EnsureDeviceIsUpToDate()
    {
        if (_datawallet.LatestModification != null && _datawallet.LatestModification.Index != _request.LocalIndex)
            throw new OperationFailedException(ApplicationErrors.Datawallet.DatawalletNotUpToDate(_request.LocalIndex, _datawallet.LatestModification.Index));
    }
    
    private async Task Save(DatawalletModification[] modifications)
    {
        await _dbContext.Set<DatawalletModification>().AddRangeAsync(modifications, _cancellationToken);
        foreach (var newModification in modifications)
        {
            if (newModification.EncryptedPayload != null)
                _blobStorage.Add(newModification.Id, newModification.EncryptedPayload);
        }

        await _blobStorage.SaveAsync();

        try
        {
            await _dbContext.SaveChangesAsync(_cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (ex.HasReason(DbUpdateExceptionReason.DuplicateIndex))
                throw new OperationFailedException(ApplicationErrors.Datawallet.DatawalletNotUpToDate());

            throw;
        }
    }

    private void PublishIntegrationEvent()
    {
        _eventBus.Publish(new DatawalletModifiedIntegrationEvent(_activeIdentity, _activeDevice));
    }
}
