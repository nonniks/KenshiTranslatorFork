using KenshiTranslator.Helper;
using System;
using System.Collections.Generic;
using System.IO;

public class TranslationDictionary
{
    private ReverseEngineer reverseEngineer;

    // Stores loaded translations from a .dict file
    private Dictionary<string, string> translations = new();

    public TranslationDictionary(ReverseEngineer re)
    {
        reverseEngineer = re;
    }

    // Export all strings to a .dict file
    public void ExportToDictFile(string path)
    {
        using var writer = new StreamWriter(path);

        // Export description
        if (reverseEngineer.modData.Header.FileType == 16 && reverseEngineer.modData.Header.Description != null)
            writer.WriteLine($"description|{reverseEngineer.modData.Header.Description}|");

        // Export records
        int recordIndex = 1;
        foreach (var record in reverseEngineer.modData.Records)
        {
            if (record.Name != null)
                writer.WriteLine($"record{recordIndex}_name|{record.Name}|");

            if (record.StringFields != null)
            {
                foreach (var kvp in record.StringFields)
                    if (!kvp.Value.Equals(""))
                        writer.WriteLine($"record{recordIndex}_{kvp.Key}|{kvp.Value}|");
            }
            recordIndex++;
        }
    }
    public void ImportFromDictFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Dictionary file not found.", path);

        translations.Clear();
        var lines = File.ReadAllLines(path);

        // First: load translations into dictionary
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split('|');
            if (parts.Length < 2) continue; // malformed line

            var key = parts[0].Trim();
            var original = parts[1].Trim();
            var translated = parts.Length >= 3 ? parts[2].Trim() : "";

            // prefer translated if present, otherwise original
            translations[key] = !string.IsNullOrWhiteSpace(translated) ? translated : original;
        }

        // Second: apply translations into reverseEngineer.modData
        if (reverseEngineer.modData.Header.FileType == 16 && reverseEngineer.modData.Header.Description != null)
        {
            if (translations.TryGetValue("description", out var desc) && !string.IsNullOrWhiteSpace(desc))
            {
                reverseEngineer.modData.Header.Description = desc;
            }
        }

        int recordIndex = 1;
        foreach (var record in reverseEngineer.modData.Records)
        {
            // record name
            string nameKey = $"record{recordIndex}_name";
            if (record.Name != null && translations.TryGetValue(nameKey, out var newName) && !string.IsNullOrWhiteSpace(newName))
            {
                record.Name = newName;
            }

            // string fields
            if (record.StringFields != null)
            {
                foreach (var kvp in record.StringFields.ToList())
                {
                    string fieldKey = $"record{recordIndex}_{kvp.Key}";
                    if (translations.TryGetValue(fieldKey, out var newValue) && !string.IsNullOrWhiteSpace(newValue))
                    {
                        record.StringFields[kvp.Key] = newValue;
                    }
                }
            }
            recordIndex++;
        }
    }
    public static async Task ApplyTranslationsAsync(
    string dictFilePath,
    Func<string, Task<string>> translateFunc,
    IProgress<int> progress = null,
    int batchSize = 50) // optional batch save
    {
        var lines = File.ReadAllLines(dictFilePath).ToList();
        int total = lines.Count;
        int completed = 0;
        List<string> failedTranslations = new();

        for (int i = 0; i < lines.Count; i++)
        {
            var parts = lines[i].Split('|');
            if (parts.Length < 3) { completed++; progress?.Report((completed * 100) / total); continue; }

            string original = parts[1];
            string translated = parts[2];

            if (string.IsNullOrWhiteSpace(translated))
            {
                try
                {
                    int retries = 3;
                    for (int attempt = 0; attempt < retries; attempt++)
                    {
                        try
                        {
                            translated = await translateFunc(original);
                            if (!string.IsNullOrWhiteSpace(translated))
                            {
                                parts[2] = translated;
                                break;
                            }
                        }
                        catch (Exception ex) when (ex.Message.Contains("429"))
                        {
                            await Task.Delay((attempt + 1) * 1000); // exponential backoff
                        }
                    }

                    if (string.IsNullOrWhiteSpace(parts[2]))
                        failedTranslations.Add(original); // log failure

                    await Task.Delay(100); // optional: throttle
                }
                catch
                {
                    failedTranslations.Add(original);
                }
            }

            lines[i] = string.Join('|', parts);
            completed++;

            // Save every batchSize lines
            if (i % batchSize == 0)
                File.WriteAllLines(dictFilePath, lines);

            progress?.Report((completed * 100) / total);
        }

        // Save final file
        File.WriteAllLines(dictFilePath, lines);

        if (failedTranslations.Any())
            File.WriteAllLines(Path.ChangeExtension(dictFilePath, ".failed.txt"), failedTranslations);
    }
    public static int GetTranslationProgress(string dictFilePath)
    {
        if (!File.Exists(dictFilePath)) return 0;

        var lines = File.ReadAllLines(dictFilePath)
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToArray();

        if (lines.Length == 0) return 100;

        int translatedCount = lines.Count(l => l.Split('|').Length >= 3 && !string.IsNullOrWhiteSpace(l.Split('|')[2]));


        return (int)Math.Round(Math.Ceiling(((translatedCount / (double)lines.Length) * 100)));
    }
}
