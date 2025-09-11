using System;
using System.Net.Http.Json;
using System.Text.Json;

namespace KenshiTranslator.Translator
{
    public class FunTranslator : ITranslator
    {
        private static readonly Lazy<FunTranslator> _instance = new(() => new FunTranslator());
        public static FunTranslator Instance => _instance.Value;
        private FunTranslator() { }
        
        private string style; // e.g. "pirate", "yoda", etc.

        public override async Task<string> Translate(string text, string targetLang = "en", string sourceLang = "auto")
        {
            try
            {
                string url = $"https://api.funtranslations.com/translate/{style}.json?text={Uri.EscapeDataString(text)}";
                var response = await client.GetStringAsync(url);

                using var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("contents", out var contents))
                {
                    return contents.GetProperty("translated").GetString();
                }
                return text;
            }
            catch
            {
                return null; // fallback on errors or rate limiting
            }
        }
        public override async Task<Dictionary<string, string>> GetSupportedLanguagesAsync()
        {
            if (lang_options == null)
            {
                lang_options = new Dictionary<string, string>
                {
                    { "pirate", "Pirate" },
                    { "yoda", "Yoda" },
                    { "shakespeare", "Shakespeare" },
                    { "dothraki", "Dothraki" },
                    { "sith", "Sith" },
                    { "valyrian", "Valyrian" }
                };
            }
                return await Task.FromResult(lang_options);
            }
    }
    
}
