using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiTranslator.Translator
{
    public abstract class ITranslator
    {
        public abstract Task<string> Translate(string text, string targetLang = "en", string sourceLang = "auto");
        public abstract Task<Dictionary<string, string>> GetSupportedLanguagesAsync();
        protected readonly HttpClient client = new HttpClient();
        protected Dictionary<string, string> lang_options= null;
    }
}
