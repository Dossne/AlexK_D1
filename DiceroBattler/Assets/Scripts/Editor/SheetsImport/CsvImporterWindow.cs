#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using DiceBattler.Boot;
using DiceBattler.Configs;
using DiceBattler.Importing;
using DiceBattler.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DiceBattler.EditorTools
{
    public sealed class CsvImporterWindow : EditorWindow
    {
        private const string DefaultsFolder = "Assets/Configs/Defaults";
        private const string ImportedFolderDefault = "Assets/Resources/DiceBattler/Imported";
        private const string GeneratedPrefabsFolder = "Assets/Prefabs/Generated";
        private const string ResourcesFolder = "Assets/Resources/DiceBattler";
        private const string ContentSetAssetPath = "Assets/Resources/DiceBattler/PrototypeContentSet.asset";
        private const string RuntimeSnapshotJsonPath = "Assets/Resources/DiceBattler/ImportedGameData.json";
        private static readonly string[] RequiredTabNames = { "Hero", "Mobs", "Waves", "Combinations", "Upgrades", "Progression", "RunConfig" };
        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly Regex SpreadsheetIdRegex = new Regex(@"/spreadsheets/d/([a-zA-Z0-9-_]+)", RegexOptions.Compiled);

        private SheetsImportSettings settings;
        private ImportStatusReport statusReport;

        [MenuItem("Tools/Dice Battler/CSV Importer")]
        public static void Open()
        {
            GetWindow<CsvImporterWindow>("Dice Battler Import");
        }

        private void OnEnable()
        {
            EnsureSupportAssets();
        }

        private void OnGUI()
        {
            EnsureSupportAssets();

            EditorGUILayout.HelpBox("Paste a Google Sheets link, then click Update. The importer will download the required tabs, create missing config assets and placeholder prefabs if needed, and update the active PrototypeContentSet.", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            settings.googleSheetUrl = EditorGUILayout.TextField("Google Sheets Link", settings.googleSheetUrl);
            settings.csvFolderPath = EditorGUILayout.TextField("CSV Cache Folder", settings.csvFolderPath);
            settings.outputFolderPath = EditorGUILayout.TextField("Config Output Folder", settings.outputFolderPath);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Optional Tab Overrides", EditorStyles.boldLabel);
                EnsureRequiredTabs(settings);
                for (int index = 0; index < settings.sheetTabs.Count; index++)
                {
                    GoogleSheetTabReference tab = settings.sheetTabs[index];
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(tab.fileName, GUILayout.Width(120f));
                    tab.gid = EditorGUILayout.TextField("gid", tab.gid);
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Update From Google Sheet", GUILayout.Height(34f)))
                {
                    UpdateFromGoogleSheet();
                }

                if (GUILayout.Button("Import Cached CSV Folder", GUILayout.Height(34f)))
                {
                    ImportFromFolder(settings.csvFolderPath, settings.csvFolderPath);
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.ObjectField("Import Settings Asset", settings, typeof(SheetsImportSettings), false);
            EditorGUILayout.ObjectField("Import Status Report", statusReport, typeof(ImportStatusReport), false);
        }

        private void UpdateFromGoogleSheet()
        {
            EnsureSupportAssets();

            string spreadsheetId = ParseSpreadsheetId(settings.googleSheetUrl);
            if (string.IsNullOrWhiteSpace(spreadsheetId))
            {
                EditorUtility.DisplayDialog("Dice Battler Import", "Paste a valid Google Sheets link or spreadsheet ID first.", "OK");
                return;
            }

            settings.spreadsheetId = spreadsheetId;
            EnsureRequiredTabs(settings);
            EditorUtility.SetDirty(settings);

            string absoluteFolder = Path.GetFullPath(settings.csvFolderPath);
            Directory.CreateDirectory(absoluteFolder);

            try
            {
                for (int index = 0; index < RequiredTabNames.Length; index++)
                {
                    string tabName = RequiredTabNames[index];
                    EditorUtility.DisplayProgressBar("Dice Battler Import", $"Downloading {tabName}...", (index + 1f) / RequiredTabNames.Length);
                    string csv = DownloadCsvForTab(spreadsheetId, tabName, FindTabGid(settings, tabName));
                    File.WriteAllText(Path.Combine(absoluteFolder, $"{tabName}.csv"), csv);
                }
            }
            catch (Exception exception)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"Google Sheets download failed: {exception.Message}");
                return;
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            ImportFromFolder(settings.csvFolderPath, $"google-sheet:{spreadsheetId}");
        }

        private void ImportFromFolder(string folderPath, string sourceLabel)
        {
            EnsureSupportAssets();

            ImportedGameData importedData = LoadCsvFolder(folderPath);
            CsvImportValidator validator = new CsvImportValidator();
            List<Runtime.ValidationIssue> issues = validator.Validate(importedData);
            bool hasFatal = issues.Exists(issue => issue.Severity == Runtime.ValidationSeverity.Fatal);

            statusReport.sourceIdentifier = sourceLabel;
            statusReport.importedAtUtc = DateTime.UtcNow.ToString("O");
            statusReport.importSucceeded = !hasFatal;
            statusReport.entries.Clear();
            for (int index = 0; index < issues.Count; index++)
            {
                statusReport.entries.Add(new ImportStatusEntry
                {
                    severity = issues[index].Severity.ToString(),
                    message = issues[index].Message,
                });
            }

            EditorUtility.SetDirty(statusReport);

            if (hasFatal)
            {
                for (int index = 0; index < issues.Count; index++)
                {
                    Debug.LogError($"{issues[index].Severity}: {issues[index].Message}");
                }

                AssetDatabase.SaveAssets();
                Debug.LogError("CSV import failed. Existing imported config assets were left unchanged.");
                return;
            }

            ImportedAssetSet authoringAssetSet = WriteAssets(importedData, settings.outputFolderPath);
            ImportedAssetSet runtimeAssetSet = EnsureBuildImportedAssets(importedData, authoringAssetSet);
            WriteRuntimeSnapshot(importedData);
            PrototypeContentSet contentSet = UpdateContentSetAndGeneratedPrefabs(runtimeAssetSet);
            AssignImportedContentSetToOpenBootstraps(contentSet);
            ForcePersistImportedAssets(authoringAssetSet, runtimeAssetSet);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Dice Battler config import completed successfully. Waves imported: {runtimeAssetSet.WaveCount}. RunConfig totalWaves: {runtimeAssetSet.RunConfig.totalWaves}. Build content path: {AssetDatabase.GetAssetPath(runtimeAssetSet.RunConfig)}.");
        }

        private void EnsureSupportAssets()
        {
            EnsureFolder("Assets/Configs");
            EnsureFolder(DefaultsFolder);
            EnsureFolder(ResourcesFolder);

            if (settings == null)
            {
                settings = CreateOrLoadAsset<SheetsImportSettings>(DefaultsFolder, "SheetsImportSettings.asset");
                if (string.IsNullOrWhiteSpace(settings.csvFolderPath))
                {
                    settings.csvFolderPath = "Assets/Data/ImportedCsv";
                }

                if (string.IsNullOrWhiteSpace(settings.outputFolderPath)
                    || string.Equals(settings.outputFolderPath, "Assets/Configs/Imported", StringComparison.OrdinalIgnoreCase))
                {
                    settings.outputFolderPath = ImportedFolderDefault;
                }

                EnsureRequiredTabs(settings);
                EditorUtility.SetDirty(settings);
            }
            else if (string.Equals(settings.outputFolderPath, "Assets/Configs/Imported", StringComparison.OrdinalIgnoreCase))
            {
                settings.outputFolderPath = ImportedFolderDefault;
                EditorUtility.SetDirty(settings);
            }

            if (statusReport == null)
            {
                statusReport = CreateOrLoadAsset<ImportStatusReport>(DefaultsFolder, "ImportStatusReport.asset");
                EditorUtility.SetDirty(statusReport);
            }
        }

        private static void EnsureRequiredTabs(SheetsImportSettings importSettings)
        {
            if (importSettings.sheetTabs == null)
            {
                importSettings.sheetTabs = new List<GoogleSheetTabReference>();
            }

            for (int index = 0; index < RequiredTabNames.Length; index++)
            {
                string tabName = RequiredTabNames[index];
                if (importSettings.sheetTabs.Find(tab => tab != null && tab.fileName == tabName) == null)
                {
                    importSettings.sheetTabs.Add(new GoogleSheetTabReference(tabName, string.Empty));
                }
            }
        }

        private static ImportedGameData LoadCsvFolder(string folderPath)
        {
            ImportedGameData data = new ImportedGameData();
            string absoluteFolder = Path.GetFullPath(folderPath);
            data.hero = LoadHero(Path.Combine(absoluteFolder, "Hero.csv"));
            data.mobs = LoadMobs(Path.Combine(absoluteFolder, "Mobs.csv"));
            data.waves = LoadWaves(Path.Combine(absoluteFolder, "Waves.csv"));
            data.combinations = LoadCombinations(Path.Combine(absoluteFolder, "Combinations.csv"));
            data.upgrades = LoadUpgrades(Path.Combine(absoluteFolder, "Upgrades.csv"));
            data.progression = LoadProgression(Path.Combine(absoluteFolder, "Progression.csv"));
            data.runConfig = LoadRunConfig(Path.Combine(absoluteFolder, "RunConfig.csv"));
            return data;
        }

        private static HeroRow LoadHero(string path)
        {
            List<Dictionary<string, string>> rows = ReadCsv(path);
            if (rows.Count == 0)
            {
                return null;
            }

            Dictionary<string, string> row = rows[0];
            return new HeroRow
            {
                id = GetString(row, "id"),
                displayName = GetString(row, "displayName"),
                maxHp = GetInt(row, "maxHp"),
                startingUnlockedDice = GetInt(row, "startingUnlockedDice", 1),
                startingRerollsPerTurn = GetInt(row, "startingRerollsPerTurn", 3),
                startingFlatDamageBonus = GetInt(row, "startingFlatDamageBonus", 0),
                baseRunId = GetString(row, "baseRunId", "run_01"),
            };
        }

        private static List<MobRow> LoadMobs(string path)
        {
            List<MobRow> result = new List<MobRow>();
            List<Dictionary<string, string>> rows = ReadCsv(path);
            for (int index = 0; index < rows.Count; index++)
            {
                Dictionary<string, string> row = rows[index];
                result.Add(new MobRow
                {
                    id = GetString(row, "id"),
                    displayName = GetString(row, "displayName"),
                    hp = GetInt(row, "hp"),
                    damageMin = GetInt(row, "damageMin"),
                    damageMax = GetInt(row, "damageMax"),
                    expReward = GetInt(row, "expReward"),
                    prefabKey = GetString(row, "prefabKey"),
                });
            }

            return result;
        }

        private static List<WaveRow> LoadWaves(string path)
        {
            List<WaveRow> result = new List<WaveRow>();
            List<Dictionary<string, string>> rows = ReadCsv(path);
            for (int index = 0; index < rows.Count; index++)
            {
                Dictionary<string, string> row = rows[index];
                WaveRow wave = new WaveRow
                {
                    waveNumber = GetInt(row, "waveNumber"),
                    expReward = GetInt(row, "expReward"),
                };

                string mobListValue = GetString(row, "mobList");
                if (!string.IsNullOrWhiteSpace(mobListValue))
                {
                    string[] mobIds = mobListValue.Split(',');
                    for (int mobIndex = 0; mobIndex < mobIds.Length; mobIndex++)
                    {
                        wave.mobList.Add(mobIds[mobIndex].Trim());
                    }
                }

                result.Add(wave);
            }

            return result;
        }

        private static List<CombinationRow> LoadCombinations(string path)
        {
            List<CombinationRow> result = new List<CombinationRow>();
            List<Dictionary<string, string>> rows = ReadCsv(path);
            for (int index = 0; index < rows.Count; index++)
            {
                Dictionary<string, string> row = rows[index];
                Enum.TryParse(GetString(row, "combination"), true, out CombinationFamily family);
                result.Add(new CombinationRow
                {
                    combination = family,
                    one = GetFloat(row, "1"),
                    two = GetFloat(row, "2"),
                    three = GetFloat(row, "3"),
                    four = GetFloat(row, "4"),
                    five = GetFloat(row, "5"),
                });
            }

            return result;
        }

        private static List<UpgradeRow> LoadUpgrades(string path)
        {
            List<UpgradeRow> result = new List<UpgradeRow>();
            List<Dictionary<string, string>> rows = ReadCsv(path);
            for (int index = 0; index < rows.Count; index++)
            {
                Dictionary<string, string> row = rows[index];
                Enum.TryParse(GetString(row, "type"), true, out UpgradeType type);
                result.Add(new UpgradeRow
                {
                    id = GetString(row, "id"),
                    type = type,
                    title = GetString(row, "title"),
                    description = GetString(row, "description"),
                    value = GetFloat(row, "value"),
                    eligibleFromLevel = GetInt(row, "eligibleFromLevel", 1),
                    enabled = GetBool(row, "enabled", true),
                    targetDiceCount = GetInt(row, "targetDiceCount", 0),
                    uiIconKey = GetString(row, "uiIconKey"),
                });
            }

            return result;
        }

        private static List<ProgressionRow> LoadProgression(string path)
        {
            List<ProgressionRow> result = new List<ProgressionRow>();
            List<Dictionary<string, string>> rows = ReadCsv(path);
            for (int index = 0; index < rows.Count; index++)
            {
                Dictionary<string, string> row = rows[index];
                result.Add(new ProgressionRow
                {
                    level = GetInt(row, "level"),
                    expToNext = GetInt(row, "expToNext"),
                });
            }

            return result;
        }

        private static RunConfigRow LoadRunConfig(string path)
        {
            RunConfigRow row = new RunConfigRow();
            if (!File.Exists(path))
            {
                return row;
            }

            string[] lines = File.ReadAllLines(path);
            if (lines.Length == 0)
            {
                return row;
            }

            string[] firstLine = SplitCsvLine(lines[0]);
            bool hasHeader = firstLine.Length >= 2
                             && string.Equals(firstLine[0].Trim(), "key", StringComparison.OrdinalIgnoreCase)
                             && string.Equals(firstLine[1].Trim(), "value", StringComparison.OrdinalIgnoreCase);

            int startIndex = hasHeader ? 1 : 0;
            for (int index = startIndex; index < lines.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(lines[index]))
                {
                    continue;
                }

                string[] parts = SplitCsvLine(lines[index]);
                if (parts.Length < 2)
                {
                    continue;
                }

                ApplyRunConfigValue(row, parts[0].Trim(), parts[1].Trim());
            }

            return row;
        }

        private static void ApplyRunConfigValue(RunConfigRow row, string key, string value)
        {
            switch (key)
            {
                case "runId":
                    row.runId = value;
                    break;
                case "totalWaves":
                    row.totalWaves = ParseInt(value, row.totalWaves);
                    break;
                case "diceSlotsTotal":
                    row.diceSlotsTotal = ParseInt(value, row.diceSlotsTotal);
                    break;
                case "rerollsPerTurnDefault":
                    row.rerollsPerTurnDefault = ParseInt(value, row.rerollsPerTurnDefault);
                    break;
                case "diceHighlightDuration":
                    row.diceHighlightDuration = ParseFloat(value, row.diceHighlightDuration);
                    break;
                case "postWaveRunTransitionDuration":
                    row.postWaveRunTransitionDuration = ParseFloat(value, row.postWaveRunTransitionDuration);
                    break;
                case "damagePanelDisplayDuration":
                    row.damagePanelDisplayDuration = ParseFloat(value, row.damagePanelDisplayDuration);
                    break;
                case "showBonusWhenZero":
                    row.showBonusWhenZero = ParseBool(value, row.showBonusWhenZero);
                    break;
                case "lockedDiceVisible":
                    row.lockedDiceVisible = ParseBool(value, row.lockedDiceVisible);
                    break;
            }
        }

        private static ImportedAssetSet WriteAssets(ImportedGameData data, string outputFolderPath)
        {
            EnsureFolder(outputFolderPath);
            DeleteGeneratedImportAssets(outputFolderPath);

            HeroConfig hero = CreateOrLoadAsset<HeroConfig>(outputFolderPath, "HeroConfig.asset");
            hero.heroId = data.hero.id;
            hero.displayName = data.hero.displayName;
            hero.maxHp = data.hero.maxHp;
            hero.startingUnlockedDice = data.hero.startingUnlockedDice;
            hero.startingRerollsPerTurn = data.hero.startingRerollsPerTurn;
            hero.startingFlatDamageBonus = data.hero.startingFlatDamageBonus;
            hero.baseRunId = data.hero.baseRunId;
            EditorUtility.SetDirty(hero);

            RunConfig runConfig = CreateOrLoadAsset<RunConfig>(outputFolderPath, "RunConfig.asset");
            runConfig.runId = data.runConfig.runId;
            runConfig.totalWaves = data.runConfig.totalWaves;
            runConfig.diceSlotsTotal = data.runConfig.diceSlotsTotal;
            runConfig.rerollsPerTurnDefault = data.runConfig.rerollsPerTurnDefault;
            runConfig.diceHighlightDuration = data.runConfig.diceHighlightDuration;
            runConfig.postWaveRunTransitionDuration = data.runConfig.postWaveRunTransitionDuration;
            runConfig.damagePanelDisplayDuration = data.runConfig.damagePanelDisplayDuration;
            runConfig.showBonusWhenZero = data.runConfig.showBonusWhenZero;
            runConfig.lockedDiceVisible = data.runConfig.lockedDiceVisible;
            EditorUtility.SetDirty(runConfig);

            List<MobConfig> importedMobs = new List<MobConfig>(data.mobs.Count);
            for (int index = 0; index < data.mobs.Count; index++)
            {
                MobConfig mob = CreateOrLoadAsset<MobConfig>(outputFolderPath, $"Mob_{data.mobs[index].id}.asset");
                mob.mobId = data.mobs[index].id;
                mob.displayName = data.mobs[index].displayName;
                mob.hp = data.mobs[index].hp;
                mob.damageMin = data.mobs[index].damageMin;
                mob.damageMax = data.mobs[index].damageMax;
                mob.expReward = data.mobs[index].expReward;
                mob.prefabKey = data.mobs[index].prefabKey;
                importedMobs.Add(mob);
                EditorUtility.SetDirty(mob);
            }

            MobDatabase mobDatabase = CreateOrLoadAsset<MobDatabase>(outputFolderPath, "MobDatabase.asset");
            mobDatabase.mobs = importedMobs;
            EditorUtility.SetDirty(mobDatabase);

            List<WaveConfig> importedWaves = new List<WaveConfig>(data.waves.Count);
            for (int index = 0; index < data.waves.Count; index++)
            {
                WaveConfig wave = CreateOrLoadAsset<WaveConfig>(outputFolderPath, $"Wave_{data.waves[index].waveNumber:00}.asset");
                wave.waveNumber = data.waves[index].waveNumber;
                wave.mobIds = new List<string>(data.waves[index].mobList);
                wave.expReward = data.waves[index].expReward;
                importedWaves.Add(wave);
                EditorUtility.SetDirty(wave);
            }

            WaveDatabase waveDatabase = CreateOrLoadAsset<WaveDatabase>(outputFolderPath, "WaveDatabase.asset");
            waveDatabase.totalWaves = data.runConfig.totalWaves;
            waveDatabase.waves = importedWaves;
            EditorUtility.SetDirty(waveDatabase);

            CombinationDatabase combinationDatabase = CreateOrLoadAsset<CombinationDatabase>(outputFolderPath, "CombinationDatabase.asset");
            combinationDatabase.entries.Clear();
            for (int index = 0; index < data.combinations.Count; index++)
            {
                combinationDatabase.entries.Add(new CombinationMultiplierEntry
                {
                    combination = data.combinations[index].combination,
                    oneDie = data.combinations[index].one,
                    twoDice = data.combinations[index].two,
                    threeDice = data.combinations[index].three,
                    fourDice = data.combinations[index].four,
                    fiveDice = data.combinations[index].five,
                });
            }
            EditorUtility.SetDirty(combinationDatabase);

            List<UpgradeConfig> importedUpgrades = new List<UpgradeConfig>(data.upgrades.Count);
            for (int index = 0; index < data.upgrades.Count; index++)
            {
                UpgradeConfig upgrade = CreateOrLoadAsset<UpgradeConfig>(outputFolderPath, $"Upgrade_{data.upgrades[index].id}.asset");
                upgrade.upgradeId = data.upgrades[index].id;
                upgrade.type = data.upgrades[index].type;
                upgrade.title = data.upgrades[index].title;
                upgrade.description = data.upgrades[index].description;
                upgrade.value = data.upgrades[index].value;
                upgrade.eligibleFromLevel = data.upgrades[index].eligibleFromLevel;
                upgrade.enabled = data.upgrades[index].enabled;
                upgrade.targetDiceCount = data.upgrades[index].targetDiceCount;
                upgrade.uiIconKey = data.upgrades[index].uiIconKey;
                importedUpgrades.Add(upgrade);
                EditorUtility.SetDirty(upgrade);
            }

            UpgradeDatabase upgradeDatabase = CreateOrLoadAsset<UpgradeDatabase>(outputFolderPath, "UpgradeDatabase.asset");
            upgradeDatabase.upgrades = importedUpgrades;
            EditorUtility.SetDirty(upgradeDatabase);

            ProgressionDatabase progressionDatabase = CreateOrLoadAsset<ProgressionDatabase>(outputFolderPath, "ProgressionDatabase.asset");
            progressionDatabase.levels.Clear();
            for (int index = 0; index < data.progression.Count; index++)
            {
                progressionDatabase.levels.Add(new ProgressionLevelEntry
                {
                    level = data.progression[index].level,
                    expToNext = data.progression[index].expToNext,
                });
            }
            EditorUtility.SetDirty(progressionDatabase);

            return new ImportedAssetSet
            {
                HeroConfig = hero,
                MobDatabase = mobDatabase,
                WaveDatabase = waveDatabase,
                CombinationDatabase = combinationDatabase,
                UpgradeDatabase = upgradeDatabase,
                ProgressionDatabase = progressionDatabase,
                RunConfig = runConfig,
                WaveCount = importedWaves.Count,
            };
        }

        private static void WriteRuntimeSnapshot(ImportedGameData data)
        {
            EnsureFolder(ResourcesFolder);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(RuntimeSnapshotJsonPath, json, Encoding.UTF8);
            AssetDatabase.ImportAsset(RuntimeSnapshotJsonPath, ImportAssetOptions.ForceUpdate);
        }

        private static void DeleteGeneratedImportAssets(string outputFolderPath)
        {
            string[] assetGuids = AssetDatabase.FindAssets(string.Empty, new[] { outputFolderPath });
            for (int index = 0; index < assetGuids.Length; index++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[index]);
                if (!assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fileName = Path.GetFileName(assetPath);
                if (fileName.StartsWith("Mob_", StringComparison.OrdinalIgnoreCase)
                    || fileName.StartsWith("Wave_", StringComparison.OrdinalIgnoreCase)
                    || fileName.StartsWith("Upgrade_", StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }
        }

        private static ImportedAssetSet EnsureBuildImportedAssets(ImportedGameData data, ImportedAssetSet authoringAssetSet)
        {
            if (authoringAssetSet.RunConfig != null
                && string.Equals(AssetDatabase.GetAssetPath(authoringAssetSet.RunConfig), $"{ImportedFolderDefault}/RunConfig.asset", StringComparison.OrdinalIgnoreCase))
            {
                return authoringAssetSet;
            }

            return WriteAssets(data, ImportedFolderDefault);
        }

        private static PrototypeContentSet UpdateContentSetAndGeneratedPrefabs(ImportedAssetSet imported)
        {
            EnsureFolder(ResourcesFolder);
            EnsureFolder("Assets/Configs");
            EnsureFolder("Assets/Configs/Registries");
            EnsureFolder(GeneratedPrefabsFolder);
            EnsureFolder($"{GeneratedPrefabsFolder}/Hero");
            EnsureFolder($"{GeneratedPrefabsFolder}/Enemies");

            HeroPresenter heroPrefab = CreateOrLoadHeroPlaceholderPrefab(imported.HeroConfig);
            List<MobPrefabEntry> mobEntries = new List<MobPrefabEntry>();
            for (int index = 0; index < imported.MobDatabase.mobs.Count; index++)
            {
                MobConfig mob = imported.MobDatabase.mobs[index];
                mobEntries.Add(new MobPrefabEntry
                {
                    prefabKey = mob.prefabKey,
                    presenterPrefab = CreateOrLoadEnemyPlaceholderPrefab(mob),
                });
            }

            HeroPrefabRegistry heroRegistry = CreateOrLoadAsset<HeroPrefabRegistry>("Assets/Configs/Registries", "HeroPrefabRegistry.asset");
            heroRegistry.entries = new List<HeroPrefabEntry>
            {
                new HeroPrefabEntry
                {
                    heroId = imported.HeroConfig.heroId,
                    presenterPrefab = heroPrefab,
                },
            };
            EditorUtility.SetDirty(heroRegistry);

            MobPrefabRegistry mobRegistry = CreateOrLoadAsset<MobPrefabRegistry>("Assets/Configs/Registries", "MobPrefabRegistry.asset");
            mobRegistry.entries = mobEntries;
            EditorUtility.SetDirty(mobRegistry);

            UISkinRegistry uiSkinRegistry = CreateOrLoadAsset<UISkinRegistry>("Assets/Configs/Registries", "UISkinRegistry.asset");
            EditorUtility.SetDirty(uiSkinRegistry);

            VfxRegistry vfxRegistry = CreateOrLoadAsset<VfxRegistry>("Assets/Configs/Registries", "VfxRegistry.asset");
            EditorUtility.SetDirty(vfxRegistry);

            PrototypeContentSet contentSet = AssetDatabase.LoadAssetAtPath<PrototypeContentSet>(ContentSetAssetPath);
            if (contentSet == null)
            {
                contentSet = ScriptableObject.CreateInstance<PrototypeContentSet>();
                AssetDatabase.CreateAsset(contentSet, ContentSetAssetPath);
            }

            contentSet.heroConfig = imported.HeroConfig;
            contentSet.mobDatabase = imported.MobDatabase;
            contentSet.waveDatabase = imported.WaveDatabase;
            contentSet.combinationDatabase = imported.CombinationDatabase;
            contentSet.upgradeDatabase = imported.UpgradeDatabase;
            contentSet.progressionDatabase = imported.ProgressionDatabase;
            contentSet.runConfig = imported.RunConfig;
            contentSet.heroPrefabRegistry = heroRegistry;
            contentSet.mobPrefabRegistry = mobRegistry;
            contentSet.uiSkinRegistry = uiSkinRegistry;
            contentSet.vfxRegistry = vfxRegistry;
            EditorUtility.SetDirty(contentSet);
            return contentSet;
        }

        private static void AssignImportedContentSetToOpenBootstraps(PrototypeContentSet contentSet)
        {
            if (contentSet == null)
            {
                return;
            }

            GameBootstrap[] bootstraps = UnityEngine.Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None);
            for (int index = 0; index < bootstraps.Length; index++)
            {
                SerializedObject serializedBootstrap = new SerializedObject(bootstraps[index]);
                SerializedProperty contentSetProperty = serializedBootstrap.FindProperty("contentSet");
                if (contentSetProperty != null)
                {
                    contentSetProperty.objectReferenceValue = contentSet;
                    serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(bootstraps[index]);
                    EditorSceneManager.MarkSceneDirty(bootstraps[index].gameObject.scene);
                }
            }
        }

        private static void ForcePersistImportedAssets(params ImportedAssetSet[] importedSets)
        {
            List<string> assetPaths = new List<string>
            {
                ContentSetAssetPath,
                "Assets/Configs/Registries/HeroPrefabRegistry.asset",
                "Assets/Configs/Registries/MobPrefabRegistry.asset",
            };

            for (int index = 0; index < importedSets.Length; index++)
            {
                ImportedAssetSet imported = importedSets[index];
                if (imported.RunConfig == null)
                {
                    continue;
                }

                assetPaths.Add(AssetDatabase.GetAssetPath(imported.HeroConfig));
                assetPaths.Add(AssetDatabase.GetAssetPath(imported.MobDatabase));
                assetPaths.Add(AssetDatabase.GetAssetPath(imported.WaveDatabase));
                assetPaths.Add(AssetDatabase.GetAssetPath(imported.CombinationDatabase));
                assetPaths.Add(AssetDatabase.GetAssetPath(imported.UpgradeDatabase));
                assetPaths.Add(AssetDatabase.GetAssetPath(imported.ProgressionDatabase));
                assetPaths.Add(AssetDatabase.GetAssetPath(imported.RunConfig));
            }

            assetPaths.RemoveAll(string.IsNullOrWhiteSpace);
            assetPaths = new List<string>(new HashSet<string>(assetPaths));
            AssetDatabase.ForceReserializeAssets(assetPaths);
        }

        private static HeroPresenter CreateOrLoadHeroPlaceholderPrefab(HeroConfig heroConfig)
        {
            string prefabPath = $"{GeneratedPrefabsFolder}/Hero/{SanitizeName(heroConfig.heroId)}.prefab";
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                GameObject root = new GameObject($"{heroConfig.displayName} Placeholder");
                root.AddComponent<HeroPresenter>();
                prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                UnityEngine.Object.DestroyImmediate(root);
            }

            return prefabAsset.GetComponent<HeroPresenter>();
        }

        private static EnemyPresenter CreateOrLoadEnemyPlaceholderPrefab(MobConfig mobConfig)
        {
            string prefabPath = $"{GeneratedPrefabsFolder}/Enemies/{SanitizeName(mobConfig.prefabKey)}.prefab";
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                GameObject root = new GameObject($"{mobConfig.displayName} Placeholder");
                root.AddComponent<EnemyPresenter>();
                prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                UnityEngine.Object.DestroyImmediate(root);
            }

            return prefabAsset.GetComponent<EnemyPresenter>();
        }

        private static T CreateOrLoadAsset<T>(string folderPath, string fileName) where T : ScriptableObject
        {
            string assetPath = $"{folderPath}/{fileName}";
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            string[] pieces = assetFolderPath.Split('/');
            string current = pieces[0];
            for (int index = 1; index < pieces.Length; index++)
            {
                string next = $"{current}/{pieces[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, pieces[index]);
                }

                current = next;
            }
        }

        private static List<Dictionary<string, string>> ReadCsv(string path)
        {
            List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>();
            if (!File.Exists(path))
            {
                return rows;
            }

            string[] lines = File.ReadAllLines(path);
            if (lines.Length <= 1)
            {
                return rows;
            }

            string[] headers = SplitCsvLine(lines[0]);
            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                if (string.IsNullOrWhiteSpace(lines[lineIndex]))
                {
                    continue;
                }

                string[] columns = SplitCsvLine(lines[lineIndex]);
                Dictionary<string, string> row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int columnIndex = 0; columnIndex < headers.Length; columnIndex++)
                {
                    row[headers[columnIndex].Trim()] = columnIndex < columns.Length ? columns[columnIndex].Trim() : string.Empty;
                }

                rows.Add(row);
            }

            return rows;
        }

        private static string[] SplitCsvLine(string line)
        {
            List<string> values = new List<string>();
            StringBuilder current = new StringBuilder();
            bool inQuotes = false;

            for (int index = 0; index < line.Length; index++)
            {
                char character = line[index];
                if (character == '"')
                {
                    if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (character == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(character);
                }
            }

            values.Add(current.ToString());
            return values.ToArray();
        }

        private static string GetString(Dictionary<string, string> row, string key, string defaultValue = "")
        {
            return row.TryGetValue(key, out string value) ? value : defaultValue;
        }

        private static int GetInt(Dictionary<string, string> row, string key, int defaultValue = 0)
        {
            return row.TryGetValue(key, out string value) ? ParseInt(value, defaultValue) : defaultValue;
        }

        private static float GetFloat(Dictionary<string, string> row, string key, float defaultValue = 0f)
        {
            return row.TryGetValue(key, out string value) ? ParseFloat(value, defaultValue) : defaultValue;
        }

        private static bool GetBool(Dictionary<string, string> row, string key, bool defaultValue = false)
        {
            return row.TryGetValue(key, out string value) ? ParseBool(value, defaultValue) : defaultValue;
        }

        private static int ParseInt(string value, int defaultValue)
        {
            return int.TryParse(value, out int parsed) ? parsed : defaultValue;
        }

        private static float ParseFloat(string value, float defaultValue)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : defaultValue;
        }

        private static bool ParseBool(string value, bool defaultValue)
        {
            return bool.TryParse(value, out bool parsed) ? parsed : defaultValue;
        }

        private static string DownloadCsvForTab(string spreadsheetId, string tabName, string gidOverride)
        {
            try
            {
                string byNameUrl = $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/gviz/tq?tqx=out:csv&sheet={Uri.EscapeDataString(tabName)}";
                HttpResponseMessage byNameResponse = HttpClient.GetAsync(byNameUrl).GetAwaiter().GetResult();
                if (byNameResponse.IsSuccessStatusCode)
                {
                    string byNameCsv = byNameResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (!string.IsNullOrWhiteSpace(byNameCsv))
                    {
                        return byNameCsv;
                    }
                }
            }
            catch
            {
                // Fall back to gid when available.
            }

            if (string.IsNullOrWhiteSpace(gidOverride))
            {
                throw new InvalidOperationException($"Could not download tab '{tabName}' by name, and no gid override is configured.");
            }

            string byGidUrl = $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/export?format=csv&gid={gidOverride}";
            HttpResponseMessage byGidResponse = HttpClient.GetAsync(byGidUrl).GetAwaiter().GetResult();
            byGidResponse.EnsureSuccessStatusCode();
            return byGidResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }

        private static string ParseSpreadsheetId(string urlOrId)
        {
            if (string.IsNullOrWhiteSpace(urlOrId))
            {
                return string.Empty;
            }

            Match match = SpreadsheetIdRegex.Match(urlOrId);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return urlOrId.Trim();
        }

        private static string FindTabGid(SheetsImportSettings importSettings, string tabName)
        {
            GoogleSheetTabReference tab = importSettings.sheetTabs.Find(entry => entry != null && entry.fileName == tabName);
            return tab != null ? tab.gid : string.Empty;
        }

        private static string SanitizeName(string value)
        {
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidCharacter, '_');
            }

            return value;
        }

        private struct ImportedAssetSet
        {
            public HeroConfig HeroConfig;
            public MobDatabase MobDatabase;
            public WaveDatabase WaveDatabase;
            public CombinationDatabase CombinationDatabase;
            public UpgradeDatabase UpgradeDatabase;
            public ProgressionDatabase ProgressionDatabase;
            public RunConfig RunConfig;
            public int WaveCount;
        }
    }
}
#endif
