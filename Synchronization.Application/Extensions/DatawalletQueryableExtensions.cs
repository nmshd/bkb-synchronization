﻿using Enmeshed.DevelopmentKit.Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Synchronization.Domain.Entities;

namespace Synchronization.Application.Extensions
{
    public static class DatawalletQueryableExtensions
    {
        public static async Task<Datawallet> OfOwner(this IQueryable<Datawallet> query, IdentityAddress owner, CancellationToken cancellationToken)
        {
            return await query.FirstOrDefaultAsync(e => e.Owner == owner, cancellationToken);
        }

        public static IQueryable<Datawallet> WithLatestModification(this IQueryable<Datawallet> query, IdentityAddress owner)
        {
            return query.Include(dw => dw.Modifications.Where(m => m.CreatedBy == owner).OrderByDescending(m => m.Index).Take(1));
        }

        public static IQueryable<Datawallet> OfOwner(this IQueryable<Datawallet> query, IdentityAddress owner)
        {
            return query.Where(e => e.Owner == owner);
        }
    }
}
