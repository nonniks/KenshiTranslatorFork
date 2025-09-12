using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiTranslator.Translator
{
    public interface TranslatorInterface
    {
        string Name { get; }
        Task<string> TranslateAsync(string text, string sourceLang = "en", string targetLang = "auto");
        Task<Dictionary<string, string>> GetSupportedLanguagesAsync();


    }
}
