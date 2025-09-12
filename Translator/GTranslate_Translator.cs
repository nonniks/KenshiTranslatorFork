
namespace KenshiTranslator.Translator
{
    public class GTranslate_Translator : TranslatorInterface
    {
        public string Name => "Google Translate";
        private static readonly Lazy<GTranslate_Translator> _instance =
            new(() => new GTranslate_Translator());
        private Dictionary<string, GTranslate.Translators.ITranslator> translators;
        private GTranslate.Translators.ITranslator current_translator;

        public GTranslate_Translator()
        {
            translators = new() {
            { "Aggregate", new GTranslate.Translators.AggregateTranslator()},
            { "Bing", new GTranslate.Translators.BingTranslator()},
            { "Google", new GTranslate.Translators.GoogleTranslator()},
            { "Google2", new GTranslate.Translators.GoogleTranslator2()},
            { "Microsoft", new GTranslate.Translators.MicrosoftTranslator()},
            { "Yandex", new GTranslate.Translators.YandexTranslator()}
        };
            current_translator = translators.GetValueOrDefault("Aggregate");
        }
        public void setTranslator(string translator)
        {
            current_translator = translators.GetValueOrDefault(translator); 
        }
        public static GTranslate_Translator Instance => _instance.Value;
        public async Task<string> TranslateAsync(string text, string sourceLang  = "en", string targetLang = "auto")
        {
            //try
            //{
                var from = GTranslate.Language.GetLanguage(sourceLang);
                var to = GTranslate.Language.GetLanguage(targetLang);
                var translated = await current_translator.TranslateAsync(text, to, from);
                //if (string.IsNullOrWhiteSpace(translated?.Translation) || translated.Translation.Trim() == text.Trim())
                //{
                //    throw new InvalidOperationException(
                //        $"Translator \"{Name}\" did not return a valid translation for '{text}'");
                //}

                return translated.Translation;

            //}
           // catch (Exception ex)
            //{
          //          $"translation failed for '{text}' ({sourceLang}->{targetLang}): {ex.Message}", ex);
           // }
        }
        public async Task<Dictionary<string, string>> GetSupportedLanguagesAsync()
        {
            // GTranslate doesn't need async for this, but keeping signature consistent
            return await Task.Run(() =>
            {
                return GTranslate.Language.LanguageDictionary.Values.OrderBy(l=>(l.ISO6391 ?? l.ISO6393))
                .ToDictionary(
                 lang => lang.ISO6391 ?? lang.ISO6393, // key = code
                  lang => $"{lang.Name} ({lang.NativeName})" // value = display
                );

            });
        }
    }
}
