using System.Reflection;
using LotsofLoot.Models.Config;
using LotsofLoot.Models.Preset;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace LotsofLoot.Services;

[Injectable(InjectionType.Singleton)]
public class ConfigService(
    ModHelper modHelper,
    JsonUtil jsonUtil,
    IEnumerable<IOnPresetUpdate> onPresetUpdates,
    ISptLogger<ConfigService> logger
)
{
    // Event that allows me to unsubscribe before I dispose of the PresetHolder in MudBlazor
    public event Action? EditablePresetHolderChanged;

    public string ModPath { get; init; } = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

    public static LotsofLootConfig LotsofLootConfig { get; private set; } = new();
    public LotsofLootModMetadata ModMetadata { get; init; } = new();

    public string CurrentlyLoadedPreset { get; private set; } = string.Empty;

    /// <summary>
    /// The current loaded preset
    ///
    /// We set it to default here, as it isn't used until after SPT's database is loaded in which case it shouldn't be null anymore
    /// </summary>
    public LotsofLootPresetConfig LotsofLootPresetConfig { get; private set; } = default!;

    private EditablePresetHolder _editablePresetHolder = default!;
    public EditablePresetHolder EditablePresetHolder
    {
        get { return _editablePresetHolder; }
        internal set
        {
            _editablePresetHolder = value;
            EditablePresetHolderChanged?.Invoke();
        }
    }

    public string GetConfigPath()
    {
        return Path.Combine(ModPath, "config.json");
    }

    public string GetPresetPath(string preset)
    {
        return Path.Combine(ModPath, "Presets", preset);
    }

    public List<string> GetPresets()
    {
        var presetPath = Path.Combine(ModPath, "Presets");

        if (!Directory.Exists(presetPath))
        {
            return [];
        }

        return Directory.GetFiles(presetPath, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().ToList();
    }

    /// <summary>
    /// Case insensitive on purpose: presets are files, and Windows would treat "Raider" and "raider" as one
    /// </summary>
    public bool PresetExists(string preset)
    {
        return GetPresets().Any(existing => string.Equals(existing, preset, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Writes <paramref name="source"/> out as a new preset and switches to it
    /// </summary>
    public async Task<bool> CreatePresetFrom(string preset, LotsofLootPresetConfig source)
    {
        try
        {
            var presetPath = GetPresetPath(preset + ".json");

            Directory.CreateDirectory(Path.GetDirectoryName(presetPath)!);

            await File.WriteAllTextAsync(presetPath, jsonUtil.Serialize(source, true));

            // Load it back rather than adopting `source` directly, so a preset that fails to round-trip
            // through JSON surfaces here instead of silently diverging from its own file
            return await LoadPresetConfig(preset, true, true);
        }
        catch (Exception ex)
        {
            logger.Error($"[Lots of Loot Redux] Failed to create preset '{preset}'", ex);
            return false;
        }
    }

    /// <summary>
    ///  LoadAsync handles the initial loading of Lots of Loot, this method should not be used after the initial load
    /// </summary>
    /// <exception cref="InvalidOperationException">This exception is thrown if there is no possible way to recover, this will kill the SPT Server</exception>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        string configPath = GetConfigPath();

        LotsofLootConfig? loadedConfig = await jsonUtil.DeserializeFromFileAsync<LotsofLootConfig>(configPath, cancellationToken);

        if (loadedConfig is not null)
        {
            LotsofLootConfig = loadedConfig;
        }
        else
        {
            logger.Warning("[Lots of Loot Redux] Could not load config! Using default settings");

            // Write the default config file back, for some reason it's missing
            await WriteConfig();
        }

        // We are too early to update the preset here, and since this is the initial init we dont need to save the config again
        bool couldLoadPresetConfig = await LoadPresetConfig(LotsofLootConfig.PresetName, false, false);

        if (!couldLoadPresetConfig)
        {
            if (LotsofLootConfig.PresetName != "default")
            {
                logger.Warning(
                    $"[Lots of Loot Redux] Preset '{LotsofLootConfig.PresetName}' could not be loaded! Attempting to load default preset"
                );

                // This will set the preset back to default if it loads successfully
                // This might have happened because the user removed a preset they were using
                couldLoadPresetConfig = await LoadPresetConfig("default", false, true);

                if (!couldLoadPresetConfig)
                {
                    throw new InvalidOperationException(
                        $"[Lots of Loot Redux] Failed to load preset '{LotsofLootConfig.PresetName}'."
                            + "Also failed to load the default preset, please re-install this mod as the default preset does not exist anymore!"
                    );
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "[Lots of Loot Redux] Failed to load the default preset, please re-install this mod as the default preset does not exist anymore!"
                );
            }
        }
    }

    /// <summary>
    /// The main preset load method
    /// </summary>
    /// <param name="preset">Which preset to load</param>
    /// <param name="shouldPresetUpdate">If preset updates should be passed (Essentially meaning if the mod should be reloaded or not)</param>
    /// <returns>Returns true if loaded successfully, returns false if not</returns>
    public async Task<bool> LoadPresetConfig(string preset, bool shouldPresetUpdate, bool shouldWritePresetNameToConfig)
    {
        try
        {
            LotsofLootPresetConfig? loadedPreset = await jsonUtil.DeserializeFromFileAsync<LotsofLootPresetConfig>(
                GetPresetPath(preset + ".json")
            );

            if (loadedPreset is null)
            {
                return false;
            }

            CurrentlyLoadedPreset = preset;
            LotsofLootPresetConfig = loadedPreset;
            EditablePresetHolder = new EditablePresetHolder(LotsofLootPresetConfig);

            if (shouldPresetUpdate)
            {
                foreach (IOnPresetUpdate presetUpdate in onPresetUpdates)
                {
                    presetUpdate.Revert();
                    presetUpdate.Apply(LotsofLootPresetConfig);
                }
            }

            logger.Success($"[Lots of Loot Redux] Preset '{preset}' successfully loaded");

            if (shouldWritePresetNameToConfig)
            {
                LotsofLootConfig.PresetName = preset;
                await WriteConfig();
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.Error($"[Lots of Loot Redux] Failed to load preset '{preset}'", ex);
            return false;
        }
    }

    /// <summary>
    /// Swaps the active preset without touching config.json or the editable copy the web UI is bound to.
    ///
    /// Synchronous because it is called from a Harmony prefix on the raid's loot generation, which cannot
    /// await. A preset is a small file and this runs once per raid, so the read is not worth restructuring.
    /// </summary>
    /// <returns>True if the preset loaded and was applied</returns>
    public bool ApplyPresetForRaid(string preset)
    {
        try
        {
            var loadedPreset = jsonUtil.DeserializeFromFile<LotsofLootPresetConfig>(GetPresetPath(preset + ".json"));

            if (loadedPreset is null)
            {
                return false;
            }

            CurrentlyLoadedPreset = preset;
            LotsofLootPresetConfig = loadedPreset;

            foreach (IOnPresetUpdate presetUpdate in onPresetUpdates)
            {
                presetUpdate.Revert();
                presetUpdate.Apply(LotsofLootPresetConfig);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.Error($"[Lots of Loot Redux] Failed to apply preset '{preset}' for this raid", ex);
            return false;
        }
    }

    public async Task WriteConfig()
    {
        await File.WriteAllTextAsync(GetConfigPath(), jsonUtil.Serialize(LotsofLootConfig, true));
    }

    public async Task ReloadConfig()
    {
        EditablePresetHolder = new EditablePresetHolder(LotsofLootPresetConfig);
    }

    public async Task SavePendingChanges()
    {
        if (EditablePresetHolder.GetPendingChanges().Count <= 0)
        {
            return;
        }

        LotsofLootPresetConfig = EditablePresetHolder.presetConfig;

        await WritePresetConfig(CurrentlyLoadedPreset);

        foreach (IOnPresetUpdate presetUpdate in onPresetUpdates)
        {
            presetUpdate.Revert();
            presetUpdate.Apply(LotsofLootPresetConfig);
        }

        EditablePresetHolder = new EditablePresetHolder(LotsofLootPresetConfig);
    }

    public async Task WritePresetConfig(string preset)
    {
        var presetPath = GetPresetPath(preset + ".json");
        var presetDir = Path.GetDirectoryName(presetPath)!;

        if (!Directory.Exists(presetDir))
        {
            Directory.CreateDirectory(presetDir);
        }

        await File.WriteAllTextAsync(presetPath, jsonUtil.Serialize(LotsofLootPresetConfig, true));
    }
}
