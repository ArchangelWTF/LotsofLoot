using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace LotsofLoot.Helpers
{
    [Injectable]
    public class LotsofLootItemHelper
    {
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
    }
}
