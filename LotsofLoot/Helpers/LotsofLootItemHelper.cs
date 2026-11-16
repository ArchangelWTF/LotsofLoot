using LotsofLoot.Models.Generator;
using LotsofLoot.Models.Preset;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Collections;

namespace LotsofLoot.Helpers;

[Injectable]
public class LotsofLootItemHelper(
    LocationTable locationTable,
    LocationConfig locationConfig,
    SeasonalEventConfig seasonalEventConfig,
    SeasonalEventService seasonalEventService
)
{
    internal static void NormalizeLootProbabilities<T>(
        IEnumerable<T> distribution,
        NormalizedLootPresetConfig config,
        Func<T, double?> getWeight,
        Action<T, double> setWeight
    )
    {
        if (!config.IsActive)
        {
            return;
        }

        var strength = Math.Min(config.Strength, 1);
        var threshold = Math.Max(config.MinProbability, 0);
        var eligible = distribution
            .Select(item => (Item: item, Weight: getWeight(item).GetValueOrDefault()))
            .Where(entry => double.IsFinite(entry.Weight) && entry.Weight > threshold)
            .ToList();

        if (eligible.Count < 2)
        {
            return;
        }

        var maximum = eligible.Max(entry => entry.Weight);
        var logMaximum = Math.Log(maximum);
        foreach (var (item, weight) in eligible)
        {
            // Interpolate in log space toward the largest weight. The ratio between two weights
            // becomes (a / b)^(1 - strength), so increasing strength makes their odds more equal.
            // Keeping the largest weight avoids reducing positive static weights toward zero.
            var logWeight = Math.Log(weight);
            var normalized = strength == 1 ? maximum : Math.Exp(logWeight + strength * (logMaximum - logWeight));
            setWeight(item, Math.Clamp(normalized, weight, maximum));
        }
    }

    internal static ProbabilityObjectArray<MongoId, int?> CreateLooseContainerLootPool(
        IEnumerable<TemplateItem> candidates,
        IReadOnlyDictionary<MongoId, double> prices,
        IReadOnlyDictionary<MongoId, double> multipliers,
        bool useItemWeights,
        ICloner cloner
    )
    {
        ProbabilityObjectArray<MongoId, int?> pool = new(cloner);

        foreach (var item in candidates)
        {
            // Read the current template, even when candidate IDs came from a cached container filter.
            if (item.Properties?.QuestItem == true)
            {
                continue;
            }

            double multiplier = multipliers.GetValueOrDefault(item.Id, 1);
            if (!double.IsFinite(multiplier) || multiplier <= 0)
            {
                continue;
            }

            double weight = 1;
            if (useItemWeights)
            {
                if (item.Id == "5449016a4bdc2d6f028b456f")
                {
                    weight = 500;
                }
                else if (item.Id == "5696686a4bdc2da3298b456a" || item.Id == "569668774bdc2da2298b4568")
                {
                    weight = 100;
                }
                else if (prices.TryGetValue(item.Id, out var price) && double.IsFinite(price) && price > 0)
                {
                    weight = Math.Round(1000 / Math.Pow(price, 1.0 / 3.0));
                }
            }

            // Keep fractional multipliers and remove disabled entries before either draw method sees them.
            weight *= multiplier;
            if (double.IsFinite(weight) && weight > 0)
            {
                pool.Add(new ProbabilityObject<MongoId, int?>(item.Id, weight, null));
            }
        }

        return pool;
    }

    public List<MongoId> FindAndReturnChildItemIdsByItems(Dictionary<MongoId, TemplateItem> items, MongoId rootItemId)
    {
        // 'Item (54009119af1c881c07000029)' Doesn't have a parent, return all of it's children instead.
        if (rootItemId == "54009119af1c881c07000029")
        {
            return items.Keys.ToList();
        }

        var parentToChildrenMap = BuildParentToChildrenMap(items);
        var result = new List<MongoId>();
        var stack = new Stack<MongoId>();
        stack.Push(rootItemId);

        // Main loop to find all children
        while (stack.Count > 0)
        {
            var currentItemId = stack.Pop();

            // If the current item has children, add them to the stack
            if (parentToChildrenMap.TryGetValue(currentItemId, out var children) && children.Count > 0)
            {
                foreach (var child in children)
                {
                    stack.Push(child);
                }
            }
            // If no children were found for the current item, add it to the result
            else
            {
                result.Add(currentItemId);
            }
        }

        return result;
    }

    private Dictionary<MongoId, List<MongoId>> BuildParentToChildrenMap(Dictionary<MongoId, TemplateItem> items)
    {
        Dictionary<MongoId, List<MongoId>> parentToChildrenMap = [];

        foreach (var (itemId, item) in items)
        {
            var parentId = item.Parent;

            if (!string.IsNullOrEmpty(parentId))
            {
                if (!parentToChildrenMap.TryGetValue(parentId, out var children))
                {
                    parentToChildrenMap[parentId] = children = [];
                }

                children.Add(itemId);
            }
        }

        return parentToChildrenMap;
    }

    public SptLootCountResult CountGeneratedSpawns(string locationId, List<SpawnpointTemplate> spawnpoints)
    {
        var result = new SptLootCountResult();

        var mapData = locationTable.GetLocation(locationId);

        if (mapData is null || mapData.StaticContainers is null)
        {
            return new();
        }

        // This will lazy load static containers each time, might be bad?
        var allStaticContainersOnMap = mapData.StaticContainers.Value.StaticContainers;

        if (!seasonalEventService.ChristmasEventEnabled())
        {
            allStaticContainersOnMap = allStaticContainersOnMap.Where(item =>
                !seasonalEventConfig.ChristmasContainerIds.Contains(item.Template.Id)
            );
        }

        var staticContainerIds = allStaticContainersOnMap.Select(c => c.Template.Id).ToHashSet();

        foreach (var spawnpoint in spawnpoints)
        {
            if (spawnpoint is null || spawnpoint.Items is null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(spawnpoint.Id) && staticContainerIds.Contains(spawnpoint.Id))
            {
                result.StaticContainersGenerated++;
                result.StaticItemsSpawned += spawnpoint.Items.Count();
            }
            else
            {
                result.DynamicItemsSpawned++;
            }
        }

        return result;
    }
}
