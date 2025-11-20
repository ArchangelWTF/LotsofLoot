using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services;

namespace LotsofLoot.Helpers
{
    [Injectable]
    public class ModificationHelper(DatabaseService databaseService, LotsOfLootLogger logger)
    {
        public void RemoveBackpackRestrictions()
        {
            Dictionary<MongoId, TemplateItem>? items = databaseService.GetTables()?.Templates?.Items;

            if (items is null)
            {
                logger.Critical("Database has no template items, is the database loaded?");
                return;
            }

            foreach ((MongoId _, TemplateItem item) in items)
            {
                // Filter out the 'Slim Field Med Pack' bag that can only contain medical items
                if (item.Id == "5e4abc6786f77406812bd572")
                {
                    continue;
                }

                // If the parent is anything else than the 'Backpack' (5448e53e4bdc2d60728b4567)
                if (item.Parent != "5448e53e4bdc2d60728b4567")
                {
                    continue;
                }

                if (item.Properties?.Grids?.Any() == true)
                {
                    foreach (var grid in item.Properties.Grids)
                    {
                        if (grid.Properties?.Filters is null)
                        {
                            continue;
                        }
                        else
                        {
                            grid.Properties.Filters = [];
                        }
                    }
                }
            }
        }
    }
}
