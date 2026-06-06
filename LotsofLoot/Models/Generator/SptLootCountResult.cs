namespace LotsofLoot.Models.Generator;

public sealed record SptLootCountResult
{
    public int StaticContainersGenerated { get; set; } = 0;
    public int StaticItemsSpawned { get; set; } = 0;
    public int DynamicItemsSpawned { get; set; } = 0;
}
