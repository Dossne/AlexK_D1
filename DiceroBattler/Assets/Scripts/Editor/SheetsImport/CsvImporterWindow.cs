#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using DiceBattler.Configs;
using DiceBattler.Importing;
using UnityEditor;
using UnityEngine;

namespace DiceBattler.EditorTools
{
    public sealed class CsvImporterWindow : EditorWindow
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private SheetsImportSettings settings;
        private ImportStatusReport statusReport;

        [MenuItem("Tools/Dice Battler/CSV Importer")]
        public static void Open()
        {
            GetWindow<CsvImporterWindow>("Dice Battler CSV Importer");
        }

        private void OnGUI()
        {
            settings = (SheetsImportSettings)EditorGUILayout.ObjectField("Import Settings", settings, typeof(SheetsImportSettings), false);
            statusReport = (ImportStatusReport)EditorGUILayout.ObjectField("Status Report", statusReport, typeof(ImportStatusReport), false);

            EditorGUILayout.HelpBox("Import exported CSV tabs into ScriptableObject config assets. Existing active data is only replaced when validation has no fatal errors.", MessageType.Info);

            using (new EditorGUI.DisabledScope(settings == null))
            {
                if (GUILayout.Button("Download From Google Sheets"))
                {
                    DownloadAndImportFromGoogleSheets();
                }

                if (GUILayout.Button("Import CSV Folder"))
                {
                    ImportFromFolder(settings.csvFolderPath, settings.csvFolderPath);
                }
            }
        }

        private void DownloadAndImportFromGoogleSheets()
        {
            if (string.IsNullOrWhiteSpace(settings.spreadsheetId))
            {
                Debug.LogError("SheetsImportSettings is missing spreadsheetId.");
                return;
            }

            if (settings.sheetTabs == null || settings.sheetTabs.Count == 0)
            {
                Debug.LogError("SheetsImportSettings needs the required tab gid mappings.");
                return;
            }

            string absoluteFolder = Path.GetFullPath(settings.csvFolderPath);
            Directory.CreateDirectory(absoluteFolder);

            try
            {
                EditorUtility.DisplayProgressBar("Dice Battler Import", "Downloading Google Sheets CSV tabs...", 0f);
                for (int index = 0; index < settings.sheetTabs.Count; index++)
                {
                    GoogleSheetTabReference tab = settings.sheetTabs[index];
                    if (tab == null || string.IsNullOrWhiteSpace(tab.fileName) || string.IsNullOrWhiteSpace(tab.gid))
                    {
                        Debug.LogError("Each configured sheet tab needs both fileName and gid.");
                        return;
                    }

                    string csv = DownloadCsv(settings.spreadsheetId, tab.gid);
                    string outputPath = Path.Combine(absoluteFolder, $"{tab.fileName}.csv");
                    File.WriteAllText(outputPath, csv);
                    EditorUtility.DisplayProgressBar("Dice Battler Import", $"Downloaded {tab.fileName}.csv", (index + 1f) / settings.sheetTabs.Count);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Google Sheets download failed: {exception.Message}");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            ImportFromFolder(settings.csvFolderPath, BuildSheetSourceLabel());
        }

        private void ImportFromFolder(string folderPath, string sourceLabel)
        {
            ImportedGameData importedData = LoadCsvFolder(folderPath);
            CsvImportValidator validator = new CsvImportValidator();
            List<Runtime.ValidationIssue> issues = validator.Validate(importedData);
            bool hasFatal = issues.Exists(issue => issue.Severity == Runtime.ValidationSeverity.Fatal);

            if (statusReport != null)
            {
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
            }

            if (hasFatal)
            {
                AssetDatabase.SaveAssets();
                Debug.LogError("CSV import failed. Active config assets were left unchanged.");
                return;
            }

            WriteAssets(importedData, settings.outputFolderPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CSV import completed successfully.");
        }

        private string BuildSheetSourceLabel()
        {
            return $"google-sheet:{settings.spreadsheetId}";
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
                string mobListValue = GetString(row, "mobList");
                WaveRow wave = new WaveRow
                {
                    waveNumber = GetInt(row, "waveNumber"),
                    expReward = GetInt(row, "expReward"),
                };

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
                if (!Enum.TryParse(GetString(row, "combination"), true, out CombinationFamily family))
                {
                    family = CombinationFamily.None;
                }

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
                UpgradeType type;
                Enum.TryParse(GetString(row, "type"), true, out type);
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
            List<Dictionary<string, string>> rows = ReadCsv(path);
            RunConfigRow row = new RunConfigRow();
            for (int index = 0; index < rows.Count; index++)
            {
                string key = GetString(rows[index], "key");
                string value = GetString(rows[index], "value");
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

            return row;
        }

        private static void WriteAssets(ImportedGameData data, string outputFolderPath)
        {
            EnsureFolder(outputFolderPath);

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

            MobDatabase mobDatabase = CreateOrLoadAsset<MobDatabase>(outputFolderPath, "MobDatabase.asset");
            mobDatabase.mobs.Clear();
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
                mobDatabase.mobs.Add(mob);
                EditorUtility.SetDirty(mob);
            }
            EditorUtility.SetDirty(mobDatabase);

            WaveDatabase waveDatabase = CreateOrLoadAsset<WaveDatabase>(outputFolderPath, "WaveDatabase.asset");
            waveDatabase.totalWaves = data.runConfig.totalWaves;
            waveDatabase.waves.Clear();
            for (int index = 0; index < data.waves.Count; index++)
            {
                WaveConfig wave = CreateOrLoadAsset<WaveConfig>(outputFolderPath, $"Wave_{data.waves[index].waveNumber:00}.asset");
                wave.waveNumber = data.waves[index].waveNumber;
                wave.mobIds = new List<string>(data.waves[index].mobList);
                wave.expReward = data.waves[index].expReward;
                waveDatabase.waves.Add(wave);
                EditorUtility.SetDirty(wave);
            }
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

            UpgradeDatabase upgradeDatabase = CreateOrLoadAsset<UpgradeDatabase>(outputFolderPath, "UpgradeDatabase.asset");
            upgradeDatabase.upgrades.Clear();
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
                upgradeDatabase.upgrades.Add(upgrade);
                EditorUtility.SetDirty(upgrade);
            }
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
                    string header = headers[columnIndex].Trim();
                    string value = columnIndex < columns.Length ? columns[columnIndex].Trim() : string.Empty;
                    row[header] = value;
                }

                rows.Add(row);
            }

            return rows;
        }

        private static string[] SplitCsvLine(string line)
        {
            return line.Split(',');
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

        private static string DownloadCsv(string spreadsheetId, string gid)
        {
            string url = $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/export?format=csv&gid={gid}";
            HttpResponseMessage response = HttpClient.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
    }
}
#endif
