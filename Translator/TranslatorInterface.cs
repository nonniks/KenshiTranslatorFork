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
        Task<string> TranslateAsync(string text, string sourceLang = "auto" , string targetLang = "en");
        Task<Dictionary<string, string>> GetSupportedLanguagesAsync();


    }
}
