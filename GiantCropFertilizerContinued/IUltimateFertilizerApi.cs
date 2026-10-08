namespace GiantCropFertilizer;

/// <summary>The subset of the Ultimate Fertilizer API used by this mod.</summary>
public interface IUltimateFertilizerApi
{
    /// <summary>Register a group of fertilizers that count as one type (ordered from lowest to highest tier).</summary>
    void RegisterFertilizerType(IEnumerable<string> itemIds);
}
