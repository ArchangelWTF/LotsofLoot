using LotsofLoot.Models.Preset;

namespace LotsofLoot.Models.Config;

/// <param name="Key">Stable identity of the setting, also used to group changes under a settings panel</param>
/// <param name="Label">What the setting is called in the UI, for the pending-changes drawer</param>
/// <param name="Revert">Puts the setting back to the value the saved preset holds</param>
public sealed record PendingChange(string Key, string Label, Func<Task> Revert);

public sealed class EditablePresetHolder(LotsofLootPresetConfig config)
{
    // Event that a few components in Blazor listen to for changes
    public event Action? PendingChangesUpdated;

    public LotsofLootPresetConfig presetConfig { get; init; } =
        FastCloner.FastCloner.DeepClone(config) ?? throw new InvalidOperationException("Could not clone config!");

    private readonly Dictionary<string, PendingChange> _pendingChanges = [];

    public IReadOnlyCollection<PendingChange> GetPendingChanges()
    {
        return _pendingChanges.Values;
    }

    /// <summary>
    /// Number of pending changes whose key sits under the given panel prefix
    /// </summary>
    public int CountPendingChanges(string keyPrefix)
    {
        return _pendingChanges.Keys.Count(key => key.StartsWith(keyPrefix, StringComparison.Ordinal));
    }

    public void AddPendingChange(string key, string label, Func<Task> revert)
    {
        // A setting that is edited twice is still one pending change, but the label may as well stay fresh
        _pendingChanges[key] = new PendingChange(key, label, revert);

        PendingChangesUpdated?.Invoke();
    }

    public void RemovePendingChange(string key)
    {
        if (_pendingChanges.Remove(key))
        {
            PendingChangesUpdated?.Invoke();
        }
    }
}
