using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services;

namespace LotsofLoot.Helpers
{
    [Injectable]
    public class ModificationHelper(DatabaseService databaseService, ILogger<ModificationHelper> logger)
    {
        public void RemoveBackpackRestrictions()
        {
            Dictionary<MongoId, TemplateItem>? items = databaseService.GetTables()?.Templates?.Items;

            if (items is null)
            {
                logger.LogError("[Lots of Loot] Database has no template items, is the database loaded?");
                return;
            }

            foreach (KeyValuePair<MongoId, TemplateItem> itemKvP in items)
            {
                // Filter out the 'Slim Field Med Pack' bag that can only contain medical items
                if (itemKvP.Value.Id == "5e4abc6786f77406812bd572")
                {
                    continue;
                }

                // If the parent is anything else than the 'Backpack' (5448e53e4bdc2d60728b4567)
                if (itemKvP.Value.Parent != "5448e53e4bdc2d60728b4567")
                {
                    continue;
                }

                if (itemKvP.Value.Properties?.Grids?.Any() == true)
                {
                    var firstGrid = itemKvP.Value.Properties.Grids.First();

                    if (firstGrid?.Props?.Filters == null)
                    {
                        continue;
                    }
                    else
                    {
                        firstGrid.Props.Filters = [];
                    }
                }
            }
        }


    }
}
