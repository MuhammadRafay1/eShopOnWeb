using System.Reflection;
using Microsoft.eShopWeb.ApplicationCore.Entities;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Helpers;

/// <summary>
/// BaseEntity.Id has a protected setter — EF Core assigns it on AddAsync in production. Unit tests that
/// construct an entity directly (without a real DbContext) need to simulate that assignment.
/// </summary>
public static class EntityIdHelper
{
    public static void SetId(BaseEntity entity, int id)
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(entity, id);
    }
}
