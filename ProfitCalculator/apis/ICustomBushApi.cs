namespace ProfitCalculator.apis;

using System.Collections.Generic;

#nullable enable

/// <summary>Mod API for custom bushes (furyx639.CustomBush). Only the members used by this mod are declared.</summary>
public interface ICustomBushApi
{
    /// <summary>Retrieves the data model for all Custom Bush.</summary>
    /// <returns>An enumerable of objects implementing the <see cref="ICustomBushData"/> interface. Each object represents a custom bush.</returns>
    public IEnumerable<ICustomBushData> GetAllBushes();

    /// <summary>Tries to get the custom bush drop associated with the given bush id.</summary>
    /// <param name="id">The id of the bush.</param>
    /// <param name="drops">When this method returns, contains the items produced by the custom bush.</param>
    /// <returns><c>true</c> if the drops associated with the given id is found; otherwise, <c>false</c>.</returns>
    public bool TryGetDrops(string id, out IList<ICustomBushDrop>? drops);
}
