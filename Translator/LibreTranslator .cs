using System;
using System.Net.Http.Json;

namespace KenshiTranslator.Translator
{
    public class LibreLanguage
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public List<string> Targets { get; set; }
    }
    public sealed class LibreTranslator : ITranslator
    {
        private static readonly Lazy<LibreTranslator> _instance = new(() => new LibreTranslator());
        public static LibreTranslator Instance => _instance.Value;
        private LibreTranslator() { }
        private string LibreUrl="https://libretranslate.com";
        public override async Task<string> Translate(string text, string targetLang = "en", string sourceLang = "auto")
        {
            var payload = new { q = text, source = sourceLang, target = targetLang, format = "text" };
            var response = await client.PostAsJsonAsync($"{LibreUrl}/translate", payload);
            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return result?["translatedText"];//result?["translatedText"] ?? text;
        }
        public override async Task<Dictionary<string, string>> GetSupportedLanguagesAsync()
        {
            if (lang_options == null)
            {
                if (lang_options == null)
                {
                    try
                    {
                        var languages = await client.GetFromJsonAsync<List<LibreLanguage>>($"{LibreUrl}/languages");
                        lang_options = languages.ToDictionary(l => l.Code, l => l.Name);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error fetching languages: {ex.Message}");
                        return null;
                    }
                }
            }
            return lang_options;

        }
    }
}
