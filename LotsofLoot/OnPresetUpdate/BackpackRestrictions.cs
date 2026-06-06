using LotsofLoot.Models.Preset;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;

namespace LotsofLoot.OnPresetUpdate;

[Injectable(InjectionType.Singleton)]
public sealed class BackpackRestrictions(DatabaseService databaseService, ICloner cloner) : IOnPresetUpdate
{
    private bool _isEnabled = false;
    private readonly Dictionary<MongoId, List<IEnumerable<GridFilter>>> _backpackFilterBackup = [];

    public void Apply(LotsofLootPresetConfig preset)
    {
        if (preset.General.RemoveBackpackRestrictions)
        {
            foreach ((MongoId id, TemplateItem item) in databaseService.GetTables().Templates.Items)
            {
                // Filter out the 'Slim Field Med Pack' bag that can only contain medical items
                if (item.Id == "5e4abc6786f77406812bd572")
                {
                    continue;
                }

                // If the parent is anything else than the 'Backpack' ( 5448e53e4bdc2d60728b4567)
                if (item.Parent != "5448e53e4bdc2d60728b4567")
                {
                    continue;
                }

                if (item.Properties?.Grids?.Any() != true)
                {
                    continue;
                }

                List<IEnumerable<GridFilter>> filtersBackup = [];

                foreach (Grid grid in item.Properties.Grids)
                {
                    if (grid.Properties?.Filters is null)
                    {
                        continue;
                    }
                    else
                    {
                        // Only add if _backpackFilterBackup hasn't been hydrated yet.
                        if (!_backpackFilterBackup.ContainsKey(id))
                        {
                            // Clone here to make sure that we dont keep a reference to the original values
                            filtersBackup.Add(cloner.Clone(grid.Properties.Filters) ?? []);
                        }

                        grid.Properties.Filters = [];
                    }
                }

                if (filtersBackup.Count > 0)
                {
                    _backpackFilterBackup.Add(id, filtersBackup);
                }
            }

            _isEnabled = true;
        }
    }

    public void Revert()
    {
        if (_backpackFilterBackup.Count == 0 || !_isEnabled)
        {
            return;
        }

        foreach ((MongoId id, TemplateItem item) in databaseService.GetTables().Templates.Items)
        {
            if (!_backpackFilterBackup.TryGetValue(id, out var savedFilters))
            {
                continue;
            }

            if (item.Properties?.Grids == null)
            {
                continue;
            }

            foreach ((var grid, var original) in item.Properties.Grids.Zip(savedFilters))
            {
                if (grid.Properties != null && original != null)
                {
                    // Clone here once more, so that the values in the backup dictionary dont get changed
                    grid.Properties.Filters = cloner.Clone(original);
                }
            }
        }

        _isEnabled = false;
    }
}
