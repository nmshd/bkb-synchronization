using Enmeshed.BuildingBlocks.Application.FluentValidation;
using FluentValidation;
using Synchronization.Application.Datawallets.DTOs;

namespace Synchronization.Application.Datawallets.Commands.PushDatawalletModifications;

public class PushDatawalletModificationsCommandValidator : AbstractValidator<PushDatawalletModificationsCommand>
{
    public PushDatawalletModificationsCommandValidator()
    {
        RuleFor(r => r.Modifications).DetailedNotEmpty();
        RuleForEach(r => r.Modifications).SetValidator(new PushDatawalletModificationItemValidator());
    }
}