﻿using Enmeshed.BuildingBlocks.Application.FluentValidation;
using FluentValidation;
using Synchronization.Application.Datawallets.DTOs;

namespace Synchronization.Application.SyncRuns.Commands.FinalizeSyncRun
{
    public class FinalizeDatawalletVersionUpgradeSyncRunCommandValidator : AbstractValidator<FinalizeDatawalletVersionUpgradeSyncRunCommand>
    {
        public FinalizeDatawalletVersionUpgradeSyncRunCommandValidator()
        {
            RuleFor(x => x.NewDatawalletVersion).DetailedNotEmpty();
            RuleForEach(x => x.DatawalletModifications).SetValidator(new PushDatawalletModificationItemValidator());
        }
    }
}
