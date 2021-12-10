using System.Text.Json.Serialization;
using Enmeshed.BuildingBlocks.API;
using Enmeshed.BuildingBlocks.API.Mvc;
using Enmeshed.BuildingBlocks.API.Mvc.ControllerAttributes;
using Enmeshed.BuildingBlocks.Application.Abstractions.Exceptions;
using Enmeshed.BuildingBlocks.Application.Pagination;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Synchronization.Application;
using Synchronization.Application.Datawallets.Commands.PushDatawalletModifications;
using Synchronization.Application.Datawallets.DTOs;
using Synchronization.Application.Datawallets.Queries.GetModifications;
using ApplicationException = Enmeshed.BuildingBlocks.Application.Abstractions.Exceptions.ApplicationException;

namespace Synchronization.API.Controllers
{
    [Obsolete("Use the /Datawallet routes instead.")]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class DatawalletModificationsController : ApiControllerBase
    {
        private readonly ApplicationOptions _options;

        public DatawalletModificationsController(IMediator mediator, IOptions<ApplicationOptions> options) : base(mediator)
        {
            _options = options.Value;
        }

        [HttpPost]
        [ProducesResponseType(typeof(HttpResponseEnvelopeResult<PushDatawalletModificationsResponse>), StatusCodes.Status201Created)]
        [ProducesError(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PushModifications(PushDatawalletModificationsRequest request)
        {
            var modificationsWithVersion = request.Modifications.Select(m => new PushDatawalletModificationItem
            {
                DatawalletVersion = 0,
                Type = m.Type,
                Collection = m.Collection,
                EncryptedPayload = m.EncryptedPayload,
                ObjectIdentifier = m.ObjectIdentifier,
                PayloadCategory = m.PayloadCategory
            }).ToArray();

            var command = new PushDatawalletModificationsCommand(modificationsWithVersion, request.LocalIndex, 0);

            var response = await _mediator.Send(command);
            return Created(string.Empty, response);
        }

        [HttpGet]
        [ProducesResponseType(typeof(HttpResponseEnvelopeResult<GetModificationsResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetModifications([FromQuery] PaginationFilter paginationFilter, [FromQuery] int? localIndex)
        {
            if (paginationFilter.PageSize > _options.Pagination.MaxPageSize)
                throw new ApplicationException(GenericApplicationErrors.Validation.InvalidPageSize(_options.Pagination.MaxPageSize));

            paginationFilter.PageSize ??= _options.Pagination.DefaultPageSize;

            var request = new GetModificationsQuery(paginationFilter, localIndex, 0);

            var response = await _mediator.Send(request);
            return Paged(response);
        }

        #region Requests

        public class PushDatawalletModificationsRequest : IRequest<PushDatawalletModificationsResponse>
        {
            [JsonConstructor]
            public PushDatawalletModificationsRequest(PushDatawalletModificationsRequestItem[] modifications, long? localIndex)
            {
                LocalIndex = localIndex;
                Modifications = modifications;
            }

            public long? LocalIndex { get; set; }
            public PushDatawalletModificationsRequestItem[] Modifications { get; set; }
        }

        public class PushDatawalletModificationsRequestItem
        {
            public string ObjectIdentifier { get; set; }
            public string PayloadCategory { get; set; }
            public string Collection { get; set; }
            public DatawalletModificationDTO.DatawalletModificationType Type { get; set; }
            public byte[] EncryptedPayload { get; set; }
        }

        #endregion
    }
}
