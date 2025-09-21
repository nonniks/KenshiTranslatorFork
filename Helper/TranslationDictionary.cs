using KenshiTranslator.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class TranslationDictionary
{
    private ReverseEngineer reverseEngineer;

    // Stores loaded translations from a .dict file
    private Dictionary<string, string> translations = new();
    private static string lineEnd="|_END_|";
    private static string sep = "|_SEP_|";

    // Technical markers that should not be translated
    private static readonly HashSet<string> TechnicalMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "DIALOG_ACTION", "DIALOGUE_LINE", "DIALOGUE", "DIALOGUE_PACKAGE",
        "CRAFTING", "SMITHING", "BUILDING", "FUNC", "MAT", "ARMOUR",
        "RPG", "Default", "Light", "Medium", "Heavy", "AUTO", "STATS",
        "TRAINEE", "COMBAT", "TACTICAL", "AMMUNITION", "AMMO",
        "M4A1", "CAPCOM", "HU", "EN", "RU", "DE", "FR", "ES", "IT",
        // Additional game-specific terms
        "FN", "GERALT_PACKAGE", "DIALOGUEGERALT", "DIALOGUE_PACKAGE_CIRI",
        "DIALOGUE_CIRI", "Package", "_Package"
    };

    // Check if a string should be translated
    private static bool ShouldTranslateString(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Don't translate short technical codes
        if (text.Length <= 3 && text.All(char.IsUpper))
            return false;

        // Don't translate known technical markers
        if (TechnicalMarkers.Contains(text.Trim()))
            return false;

        // Don't translate strings that are mostly uppercase technical terms
        if (text.Length > 3 && text.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || char.IsDigit(c) || c == '_'))
            return false;

        // Don't translate single words that are all caps with underscores
        if (text.Contains('_') && text.All(c => char.IsUpper(c) || c == '_' || char.IsDigit(c)))
            return false;

        // Don't translate file extensions or obvious codes
        if (text.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ||
            text.EndsWith(".mesh", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("MAT") && text.Length < 20)
            return false;

        // Don't translate pure numbers
        if (text.All(c => char.IsDigit(c) || c == '.' || c == '-'))
            return false;

        return true;
    }

    // Check if translation was successful
    private static bool IsValidTranslation(string original, string translated, string sourceLang, string targetLang)
    {
        if (string.IsNullOrWhiteSpace(translated))
            return false;

        // Allow certain specific terms to remain unchanged (proper nouns and technical terms)
        var allowedIdentical = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Heft", "Light", "Medium", "Heavy", "Default", "RPG", "Auto",
            "Ciri", "Geralt", "Cirilla", "Vesemir", "Roach", "OK"
        };

        // If it's an allowed identical translation, accept it
        if (allowedIdentical.Contains(original.Trim()))
            return true;

        // If translation is identical to original (and we're translating between different languages), it might not be translated
        if (original.Equals(translated, StringComparison.Ordinal) &&
            !sourceLang.Equals(targetLang, StringComparison.OrdinalIgnoreCase) &&
            original.Length > 2) // Check words longer than 2 characters for translation
        {
            return false;
        }

        // Check for common untranslated patterns
        if (original.Equals(translated, StringComparison.OrdinalIgnoreCase) &&
            original.Any(char.IsLetter) &&
            original.Length > 1 &&
            !allowedIdentical.Contains(original.Trim()))
        {
            return false;
        }

        // Check if translation contains Cyrillic characters (good for EN->RU)
        if (targetLang.Contains("ru", StringComparison.OrdinalIgnoreCase))
        {
            bool hasCyrillic = translated.Any(c => c >= 0x0400 && c <= 0x04FF);
            if (!hasCyrillic && original.Any(char.IsLetter) && original.Length > 2)
            {
                return false; // Probably not translated to Russian
            }
        }

        return true;
    }

    public TranslationDictionary(ReverseEngineer re)
    {
        reverseEngineer = re;
    }

    // Export all strings to a .dict file
    public void ExportToDictFile(string path)
    {
        using var writer = new StreamWriter(path);

        // Export description
        if (reverseEngineer.modData.Header!.FileType == 16 && reverseEngineer.modData.Header.Description != null)
            writer.Write($"description{sep}{reverseEngineer.modData.Header.Description}{sep}{lineEnd}");

        // Export records
        int recordIndex = 1;
        foreach (var record in reverseEngineer.modData.Records!)
        {
            if (record.Name != null)
                writer.Write($"record{recordIndex}_name{sep}{record.Name}{sep}{lineEnd}");

            if (record.StringFields != null)
            {
                foreach (var kvp in record.StringFields)
                    if (!kvp.Value.Equals(""))
                        writer.Write($"record{recordIndex}_{kvp.Key}{sep}{kvp.Value}{sep}{lineEnd}");
            }
            recordIndex++;
        }
    }
    public void ImportFromDictFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Dictionary file not found.", path);
        translations.Clear();
        var all = File.ReadAllText(path);
        foreach (var segment in all.Split(lineEnd))
        {
            if (string.IsNullOrWhiteSpace(segment)) continue;
            var parts = segment.Split(sep);
            if (parts.Length < 2) continue;
            var key = parts[0].Trim();
            var original = parts[1].Trim();
            var translated = parts.Length >= 3 ? parts[2].Trim() : "";
            translations[key] = !string.IsNullOrWhiteSpace(translated) ? translated : original;
        }


        if (reverseEngineer.modData.Header!.FileType == 16 && reverseEngineer.modData.Header.Description != null)
        {
            if (translations.TryGetValue("description", out var desc) && !string.IsNullOrWhiteSpace(desc))
            {
                reverseEngineer.modData.Header.Description = desc;
            }
        }

        int recordIndex = 1;
        foreach (var record in reverseEngineer.modData.Records!)
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
    public static async Task<int> ApplyTranslationsAsync(
    string dictFilePath,
    Func<string, Task<string>> translateFunc,
    IProgress<int>? progress = null,
    int batchSize = 100,
    Action<string, string, bool>? logTranslation = null,
    Action<string, string>? logError = null,
    Func<List<string>, string, string, Task<List<string>>>? batchTranslateFunc = null)
    {
        var all = File.ReadAllText(dictFilePath).Split(lineEnd);
        int total = all.Length;
        int completed = 0;
        int successCount = 0;
        List<string> failedTranslations = new();

        // If batch translation function is provided, use optimized batch processing
        if (batchTranslateFunc != null)
        {
            successCount = await ProcessBatchTranslations(all, batchTranslateFunc, progress, batchSize, logTranslation, logError, dictFilePath).ConfigureAwait(false);
            return successCount;
        }

        // Fall back to single translation processing
        for (int i = 0; i < total; i++)
        {
            var parts = all[i].Split(sep);
            if (parts.Length < 3) { completed++; progress?.Report((completed * 100) / total); continue; }
            string original = parts[1];
            string translated = parts[2];
            if (string.IsNullOrWhiteSpace(translated))
            {
                // Check if this string should be translated
                if (!ShouldTranslateString(original))
                {
                    // Skip translation for technical markers, keep original
                    parts[2] = original;
                    System.Diagnostics.Debug.WriteLine($"[TranslationDict] Skipped technical marker: '{original}'");
                }
                else
                {
                    try
                    {
                        int maxRetries = 5; // Increased retries
                        string bestTranslation = original;
                        bool translationSucceeded = false;

                        for (int attempt = 0; attempt < maxRetries && !translationSucceeded; attempt++)
                        {
                            try
                            {
                                translated = await translateFunc(original).ConfigureAwait(false);
                                System.Diagnostics.Debug.WriteLine($"[TranslationDict] Attempt {attempt + 1}: '{original}' -> '{translated}'");

                                // Check if translation is valid
                                if (IsValidTranslation(original, translated, "en", "ru"))
                                {
                                    bestTranslation = translated;
                                    translationSucceeded = true;
                                    successCount++;
                                    System.Diagnostics.Debug.WriteLine($"[TranslationDict] SUCCESS: '{original}' -> '{translated}'");
                                    logTranslation?.Invoke(original, translated, true);
                                    break;
                                }
                                else if (!string.IsNullOrWhiteSpace(translated))
                                {
                                    // Keep this translation as backup, but try again
                                    if (bestTranslation == original || translated.Length > bestTranslation.Length)
                                    {
                                        bestTranslation = translated;
                                    }
                                    System.Diagnostics.Debug.WriteLine($"[TranslationDict] Inadequate translation, retrying: '{original}' -> '{translated}'");

                                    // Wait longer before retry for inadequate translations
                                    await Task.Delay(500 + attempt * 300).ConfigureAwait(false);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[TranslationDict] Empty translation received for: '{original}'");
                                }
                            }
                            catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("quota") || ex.Message.Contains("rate"))
                            {
                                System.Diagnostics.Debug.WriteLine($"[TranslationDict] Rate limit hit, waiting... (attempt {attempt + 1})");
                                await Task.Delay((attempt + 1) * 3000).ConfigureAwait(false);
                                continue;
                            }
                            catch (Exception ex) when (ex.Message.Contains("403") || ex.Message.Contains("forbidden"))
                            {
                                System.Diagnostics.Debug.WriteLine($"[TranslationDict] API access forbidden: {ex.Message}");
                                logError?.Invoke(original, $"API access forbidden: {ex.Message}");
                                break; // Don't retry for permission errors
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[TranslationDict] Translation error (attempt {attempt + 1}): {ex.Message}");
                                if (attempt == maxRetries - 1)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[TranslationDict] Final attempt failed for: '{original}'");
                                    logError?.Invoke(original, $"All {maxRetries} attempts failed: {ex.Message}");
                                }
                                await Task.Delay(1000).ConfigureAwait(false);
                            }
                        }

                        // Use the best translation we got
                        parts[2] = bestTranslation;

                        if (translationSucceeded)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TranslationDict] FINAL SUCCESS: '{original}' -> '{bestTranslation}'");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[TranslationDict] FINAL RESULT (may be unchanged): '{original}' -> '{bestTranslation}'");
                            logTranslation?.Invoke(original, bestTranslation, false);
                        }

                        // Always add delay between different strings
                        await Task.Delay(200).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TranslationDict] Unexpected error: {ex.Message}");
                        parts[2] = original; // Keep original on unexpected errors
                    }
                }
            }
            all[i] = string.Join(sep, parts);
            completed++;

            // Save periodically to prevent data loss
            if (i % 100 == 0 || i == total - 1)
            {
                File.WriteAllText(dictFilePath, string.Join(lineEnd, all));
                System.Diagnostics.Debug.WriteLine($"[TranslationDict] Saved progress at item {i + 1}/{total}");
            }

            progress?.Report((completed * 100) / total);
        }

        // Smart retry pass: use batch translation for remaining untranslated strings
        int retrySuccessCount = await PerformSmartRetryPass(all, batchTranslateFunc, translateFunc, logTranslation, logError, dictFilePath).ConfigureAwait(false);
        successCount += retrySuccessCount;

        // Final save
        File.WriteAllText(dictFilePath, string.Join(lineEnd, all));

        System.Diagnostics.Debug.WriteLine($"[TranslationDict] Total successful translations: {successCount}");
        return successCount;
    }

    private static async Task<int> PerformSmartRetryPass(
        string[] all,
        Func<List<string>, string, string, Task<List<string>>>? batchTranslateFunc,
        Func<string, Task<string>> translateFunc,
        Action<string, string, bool>? logTranslation,
        Action<string, string>? logError,
        string dictFilePath)
    {
        int retrySuccessCount = 0;
        System.Diagnostics.Debug.WriteLine("[TranslationDict] Starting smart retry pass...");

        // Find all strings that still need translation
        var failedTranslations = new List<(int index, string original)>();

        for (int i = 0; i < all.Length; i++)
        {
            var parts = all[i].Split(sep);
            if (parts.Length >= 3)
            {
                string original = parts[1];
                string translated = parts[2];

                // More strict validation for retry pass
                if (ShouldTranslateString(original) && !IsTranslationComplete(original, translated, "en", "ru"))
                {
                    failedTranslations.Add((i, original));
                    System.Diagnostics.Debug.WriteLine($"[TranslationDict] Retry needed: '{original}' -> '{translated}'");
                }
            }
        }

        if (failedTranslations.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[TranslationDict] No strings need retry translation");
            return retrySuccessCount;
        }

        System.Diagnostics.Debug.WriteLine($"[TranslationDict] Found {failedTranslations.Count} strings needing retry translation");

        // If batch translation is available, use it for efficiency
        if (batchTranslateFunc != null && failedTranslations.Count > 1)
        {
            System.Diagnostics.Debug.WriteLine("[TranslationDict] Using batch translation for retry pass");

            try
            {
                var textsToRetry = failedTranslations.Select(item => item.original).ToList();
                var retryTranslations = await batchTranslateFunc(textsToRetry, "en", "ru").ConfigureAwait(false);

                // Apply retry results with even stricter validation
                for (int i = 0; i < failedTranslations.Count && i < retryTranslations.Count; i++)
                {
                    var (index, original) = failedTranslations[i];
                    var newTranslation = retryTranslations[i];

                    if (IsTranslationComplete(original, newTranslation, "en", "ru"))
                    {
                        var parts = all[index].Split(sep);
                        if (parts.Length >= 3)
                        {
                            parts[2] = newTranslation;
                            all[index] = string.Join(sep, parts);
                            retrySuccessCount++;
                            logTranslation?.Invoke(original, newTranslation, true);
                            System.Diagnostics.Debug.WriteLine($"[TranslationDict] Retry SUCCESS: '{original}' -> '{newTranslation}'");
                        }
                    }
                    else
                    {
                        logTranslation?.Invoke(original, newTranslation, false);
                        System.Diagnostics.Debug.WriteLine($"[TranslationDict] Retry still inadequate: '{original}' -> '{newTranslation}'");
                    }
                }

                // Save progress after batch retry
                File.WriteAllText(dictFilePath, string.Join(lineEnd, all));
                System.Diagnostics.Debug.WriteLine($"[TranslationDict] Batch retry completed: {retrySuccessCount}/{failedTranslations.Count} successful");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TranslationDict] Batch retry failed: {ex.Message}");
                logError?.Invoke("Batch retry", ex.Message);

                // Fall back to individual retries
                int batchFallbackSuccessCount = await PerformIndividualRetries(all, failedTranslations, translateFunc, logTranslation, logError, dictFilePath).ConfigureAwait(false);
                retrySuccessCount += batchFallbackSuccessCount;
            }
        }
        else
        {
            // No batch translation available, use individual retries
            System.Diagnostics.Debug.WriteLine("[TranslationDict] Using individual translation for retry pass");
            int individualRetrySuccessCount = await PerformIndividualRetries(all, failedTranslations, translateFunc, logTranslation, logError, dictFilePath).ConfigureAwait(false);
            retrySuccessCount += individualRetrySuccessCount;
        }

        return retrySuccessCount;
    }

    private static async Task<int> PerformIndividualRetries(
        string[] all,
        List<(int index, string original)> failedTranslations,
        Func<string, Task<string>> translateFunc,
        Action<string, string, bool>? logTranslation,
        Action<string, string>? logError,
        string dictFilePath)
    {
        int individualSuccessCount = 0;
        int maxIndividualRetries = Math.Min(50, failedTranslations.Count); // Limit individual retries

        System.Diagnostics.Debug.WriteLine($"[TranslationDict] Performing individual retries for {maxIndividualRetries} strings");

        for (int i = 0; i < maxIndividualRetries; i++)
        {
            var (index, original) = failedTranslations[i];

            try
            {
                var newTranslation = await translateFunc(original).ConfigureAwait(false);

                if (IsTranslationComplete(original, newTranslation, "en", "ru"))
                {
                    var parts = all[index].Split(sep);
                    if (parts.Length >= 3)
                    {
                        parts[2] = newTranslation;
                        all[index] = string.Join(sep, parts);
                        individualSuccessCount++;
                        logTranslation?.Invoke(original, newTranslation, true);
                        System.Diagnostics.Debug.WriteLine($"[TranslationDict] Individual retry SUCCESS: '{original}' -> '{newTranslation}'");
                    }
                }
                else
                {
                    logTranslation?.Invoke(original, newTranslation, false);
                    System.Diagnostics.Debug.WriteLine($"[TranslationDict] Individual retry failed: '{original}' -> '{newTranslation}'");
                }

                // Delay between individual requests to avoid rate limiting
                await Task.Delay(300).ConfigureAwait(false);

                // Save progress every 10 individual retries
                if ((i + 1) % 10 == 0)
                {
                    File.WriteAllText(dictFilePath, string.Join(lineEnd, all));
                    System.Diagnostics.Debug.WriteLine($"[TranslationDict] Saved progress: {i + 1}/{maxIndividualRetries} individual retries completed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TranslationDict] Individual retry failed for '{original}': {ex.Message}");
                logError?.Invoke(original, ex.Message);
            }
        }

        // Final save after individual retries
        File.WriteAllText(dictFilePath, string.Join(lineEnd, all));
        System.Diagnostics.Debug.WriteLine($"[TranslationDict] Individual retries completed: {individualSuccessCount}/{maxIndividualRetries} successful");

        return individualSuccessCount;
    }

    // More strict validation for retry pass
    private static bool IsTranslationComplete(string original, string translated, string sourceLang, string targetLang)
    {
        // First apply standard validation
        if (!IsValidTranslation(original, translated, sourceLang, targetLang))
            return false;

        // Additional strict checks for retry pass
        if (string.IsNullOrWhiteSpace(translated))
            return false;

        // For Russian translation, be extra strict about Cyrillic presence
        if (targetLang.Contains("ru", StringComparison.OrdinalIgnoreCase))
        {
            // Allow very short words and numbers to remain unchanged
            if (original.Length <= 2 || original.All(c => char.IsDigit(c) || char.IsPunctuation(c)))
                return true;

            // For words longer than 2 characters with letters, require Cyrillic
            if (original.Any(char.IsLetter) && original.Length > 2)
            {
                bool hasCyrillic = translated.Any(c => c >= 0x0400 && c <= 0x04FF);
                if (!hasCyrillic)
                {
                    System.Diagnostics.Debug.WriteLine($"[TranslationDict] Strict check failed - no Cyrillic in '{translated}' for '{original}'");
                    return false;
                }
            }
        }

        // Check for suspicious patterns that indicate failed translation
        var suspiciousPatterns = new[]
        {
            "error", "Error", "ERROR", "failed", "Failed", "FAILED",
            "invalid", "Invalid", "INVALID", "timeout", "Timeout", "TIMEOUT"
        };

        if (suspiciousPatterns.Any(pattern => translated.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
        {
            System.Diagnostics.Debug.WriteLine($"[TranslationDict] Strict check failed - suspicious pattern in '{translated}'");
            return false;
        }

        return true;
    }

    private static async Task<int> ProcessBatchTranslations(
        string[] all,
        Func<List<string>, string, string, Task<List<string>>> batchTranslateFunc,
        IProgress<int>? progress,
        int batchSize,
        Action<string, string, bool>? logTranslation,
        Action<string, string>? logError,
        string dictFilePath)
    {
        int total = all.Length;
        int completed = 0;
        int successCount = 0;

        System.Diagnostics.Debug.WriteLine($"[TranslationDict] Using batch translation with batch size: {batchSize}");

        // Collect all texts that need translation
        var textsToTranslate = new List<(int index, string original)>();
        for (int i = 0; i < total; i++)
        {
            var parts = all[i].Split(sep);
            if (parts.Length >= 3)
            {
                string original = parts[1];
                string translated = parts[2];

                if (string.IsNullOrWhiteSpace(translated) && ShouldTranslateString(original))
                {
                    textsToTranslate.Add((i, original));
                }
            }
        }

        System.Diagnostics.Debug.WriteLine($"[TranslationDict] Found {textsToTranslate.Count} texts to translate out of {total} total");

        // Process in batches
        for (int batchStart = 0; batchStart < textsToTranslate.Count; batchStart += batchSize)
        {
            var batchEnd = Math.Min(batchStart + batchSize, textsToTranslate.Count);
            var currentBatch = textsToTranslate.Skip(batchStart).Take(batchEnd - batchStart).ToList();

            try
            {
                System.Diagnostics.Debug.WriteLine($"[TranslationDict] Processing batch {batchStart / batchSize + 1}: {currentBatch.Count} texts");

                // Extract just the text for batch translation
                var textsOnly = currentBatch.Select(item => item.original).ToList();

                // Call batch translation function
                var translations = await batchTranslateFunc(textsOnly, "en", "ru").ConfigureAwait(false);

                // CRITICAL: Validate batch result integrity
                if (translations == null)
                {
                    throw new InvalidOperationException("Batch translation returned null results");
                }

                if (translations.Count != textsOnly.Count)
                {
                    System.Diagnostics.Debug.WriteLine($"[TranslationDict] WARNING: Expected {textsOnly.Count} translations, got {translations.Count}");

                    // If we got fewer results, pad with originals to maintain data integrity
                    while (translations.Count < textsOnly.Count)
                    {
                        translations.Add(textsOnly[translations.Count]);
                    }

                    // If we got more results, truncate to prevent index errors
                    if (translations.Count > textsOnly.Count)
                    {
                        translations = translations.Take(textsOnly.Count).ToList();
                    }
                }

                // Apply translations back to the array with strict index validation
                for (int i = 0; i < currentBatch.Count; i++)
                {
                    var (index, original) = currentBatch[i];

                    // Double-check array bounds
                    if (i >= translations.Count)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TranslationDict] ERROR: Translation index {i} out of bounds, using original text");
                        translations.Add(original);
                    }

                    var translation = translations[i];

                    // Verify that the translation corresponds to the right original text
                    if (i < textsOnly.Count && textsOnly[i] != original)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TranslationDict] ERROR: Text mismatch at index {i}. Expected '{original}', found '{textsOnly[i]}'. Using original.");
                        translation = original;
                    }

                    var parts = all[index].Split(sep);
                    if (parts.Length >= 3)
                    {
                        bool isValid = IsValidTranslation(original, translation, "en", "ru");
                        parts[2] = isValid ? translation : original;
                        all[index] = string.Join(sep, parts);

                        if (isValid) successCount++;

                        logTranslation?.Invoke(original, translation, isValid);
                        System.Diagnostics.Debug.WriteLine($"[TranslationDict] Batch result [{i}]: '{original}' -> '{translation}' (valid: {isValid})");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[TranslationDict] ERROR: Invalid dict entry structure at index {index}");
                    }
                }

                completed += currentBatch.Count;
                progress?.Report((completed * 100) / textsToTranslate.Count);

                // Save progress every batch
                File.WriteAllText(dictFilePath, string.Join(lineEnd, all));
                System.Diagnostics.Debug.WriteLine($"[TranslationDict] Saved batch progress: {completed}/{textsToTranslate.Count}");

                // Small delay between batches to avoid rate limiting
                await Task.Delay(100).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TranslationDict] Batch translation failed: {ex.Message}");
                logError?.Invoke($"Batch {batchStart / batchSize + 1}", ex.Message);

                // Fall back to individual translation for this batch
                foreach (var (index, original) in currentBatch)
                {
                    var parts = all[index].Split(sep);
                    if (parts.Length >= 3)
                    {
                        parts[2] = original; // Keep original on batch failure
                        all[index] = string.Join(sep, parts);
                        logTranslation?.Invoke(original, original, false);
                    }
                }

                completed += currentBatch.Count;
                progress?.Report((completed * 100) / textsToTranslate.Count);
            }
        }

        // Update progress for non-translated items
        var nonTranslatedCount = total - textsToTranslate.Count;
        if (nonTranslatedCount > 0)
        {
            completed += nonTranslatedCount;
            progress?.Report(100);
        }

        System.Diagnostics.Debug.WriteLine($"[TranslationDict] Batch processing completed. Total processed: {completed}");

        // Final save
        File.WriteAllText(dictFilePath, string.Join(lineEnd, all));

        // Count actual successful translations in the processed dictionary
        int actualSuccessCount = 0;
        for (int i = 0; i < all.Length; i++)
        {
            var parts = all[i].Split(sep);
            if (parts.Length >= 3)
            {
                string original = parts[1];
                string translated = parts[2];

                if (ShouldTranslateString(original) && !string.IsNullOrWhiteSpace(translated))
                {
                    actualSuccessCount++;
                }
            }
        }

        System.Diagnostics.Debug.WriteLine($"[TranslationDict] Actual successful translations: {actualSuccessCount}");
        return actualSuccessCount;
    }
    public static int GetTranslationProgress(string dictFilePath)
    {
        if (!File.Exists(dictFilePath)) return 0;
        var all = File.ReadAllText(dictFilePath).Split(lineEnd);

        int totalToTranslate = 0;
        int translated = 0;

        for (int i = 0; i < all.Length; i++)
        {
            var parts = all[i].Split(sep);
            if (parts.Length >= 3)
            {
                string original = parts[1];
                string translatedText = parts[2];

                // Only count strings that should be translated
                if (ShouldTranslateString(original))
                {
                    totalToTranslate++;
                    // Count as translated if field is filled
                    if (!string.IsNullOrWhiteSpace(translatedText))
                    {
                        translated++;
                    }
                }
            }
        }

        if (totalToTranslate == 0) return 100;

        double percentage = (translated / (double)totalToTranslate) * 100;
        int progress = (int)Math.Ceiling(percentage);

        // If all translatable strings are translated, that's 100%
        if (translated == totalToTranslate) progress = 100;

        System.Diagnostics.Debug.WriteLine($"[GetTranslationProgress] {translated}/{totalToTranslate} = {percentage:F2}% -> {progress}%");
        return progress;
    }
}
