using System.Reflection;
using LotsofLoot.Models.Config;
using LotsofLoot.Models.Preset;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
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

    public EditablePresetHolder EditablePresetHolder { get; private set; } = default!;

    public string GetConfigPath()
    {
        return Path.Combine(ModPath, "Config", "config.jsonc");
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
    ///  LoadAsync handles the initial loading of Lots of Loot, this method should not be used after the initial load
    /// </summary>
    /// <exception cref="InvalidOperationException">This exception is thrown if there is no possible way to recover, this will kill the SPT Server</exception>
    public async Task LoadAsync()
    {
        string configPath = GetConfigPath();
        string configDir = Path.GetDirectoryName(configPath)!;

        LotsofLootPresetConfig? loadedConfig = await jsonUtil.DeserializeFromFileAsync<LotsofLootPresetConfig>(configPath);

        if (loadedConfig is not null)
        {
            LotsofLootPresetConfig = loadedConfig;

            logger.Success("[Lots of Loot Redux] Config successfully loaded");
        }
        else
        {
            logger.Warning("[Lots of Loot Redux] No config file found, loading defaults!");

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            throw new InvalidOperationException(
                $"[Lots of Loot Redux] Failed to load config. Please re-install this mod as the default config does not exist anymore!"
            );
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
        if (EditablePresetHolder.PendingChanges.Count <= 0)
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
