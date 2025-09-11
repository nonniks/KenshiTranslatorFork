using KenshiTranslator.Translator;
using NTextCat;
using System;
using System.Net.Http.Json;
using System.Web;


public class GoogleTranslator : ITranslator
{
    private static readonly Lazy<GoogleTranslator> _instance = new(() => new GoogleTranslator());
    public static GoogleTranslator Instance => _instance.Value;
    private GoogleTranslator() { }
    public override async Task<string> Translate(string text, string targetLang = "en", string sourceLang = "auto")
    {
        string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sourceLang}&tl={targetLang}&dt=t&q={HttpUtility.UrlEncode(text)}";
        var response = await client.GetStringAsync(url);
        var result = System.Text.Json.JsonSerializer.Deserialize<object[][][]>(response);
        return result?[0][0][0]?.ToString();// ?? text;
    }
    public override async Task<Dictionary<string, string>> GetSupportedLanguagesAsync()
    {
        if (lang_options == null)
        {
            lang_options = new Dictionary<string, string>
                {
                    { "af", "Afrikaans" },
                    { "sq", "Albanian" },
                    { "am", "Amharic" },
                    { "ar", "Arabic" },
                    { "hy", "Armenian" },
                    { "az", "Azerbaijani" },
                    { "eu", "Basque" },
                    { "be", "Belarusian" },
                    { "bn", "Bengali" },
                    { "bs", "Bosnian" },
                    { "bg", "Bulgarian" },
                    { "ca", "Catalan" },
                    { "ceb", "Cebuano" },
                    { "zh-CN", "Chinese (Simplified)" },
                    { "zh-TW", "Chinese (Traditional)" },
                    { "hr", "Croatian" },
                    { "cs", "Czech" },
                    { "da", "Danish" },
                    { "nl", "Dutch" },
                    { "en", "English" },
                    { "eo", "Esperanto" },
                    { "et", "Estonian" },
                    { "fi", "Finnish" },
                    { "fr", "French" },
                    { "gl", "Galician" },
                    { "ka", "Georgian" },
                    { "de", "German" },
                    { "el", "Greek" },
                    { "gu", "Gujarati" },
                    { "ht", "Haitian Creole" },
                    { "ha", "Hausa" },
                    { "he", "Hebrew" },
                    { "hi", "Hindi" },
                    { "hu", "Hungarian" },
                    { "is", "Icelandic" },
                    { "id", "Indonesian" },
                    { "ga", "Irish" },
                    { "it", "Italian" },
                    { "ja", "Japanese" },
                    { "jv", "Javanese" },
                    { "kn", "Kannada" },
                    { "kk", "Kazakh" },
                    { "km", "Khmer" },
                    { "ko", "Korean" },
                    { "lo", "Lao" },
                    { "la", "Latin" },
                    { "lv", "Latvian" },
                    { "lt", "Lithuanian" },
                    { "mk", "Macedonian" },
                    { "mg", "Malagasy" },
                    { "ms", "Malay" },
                    { "ml", "Malayalam" },
                    { "mt", "Maltese" },
                    { "mi", "Maori" },
                    { "mr", "Marathi" },
                    { "mn", "Mongolian" },
                    { "my", "Myanmar (Burmese)" },
                    { "ne", "Nepali" },
                    { "no", "Norwegian" },
                    { "fa", "Persian" },
                    { "pl", "Polish" },
                    { "pt", "Portuguese" },
                    { "pa", "Punjabi" },
                    { "ro", "Romanian" },
                    { "ru", "Russian" },
                    { "sm", "Samoan" },
                    { "gd", "Scots Gaelic" },
                    { "sr", "Serbian" },
                    { "st", "Sesotho" },
                    { "sn", "Shona" },
                    { "sd", "Sindhi" },
                    { "si", "Sinhala" },
                    { "sk", "Slovak" },
                    { "sl", "Slovenian" },
                    { "so", "Somali" },
                    { "es", "Spanish" },
                    { "su", "Sundanese" },
                    { "sw", "Swahili" },
                    { "sv", "Swedish" },
                    { "tl", "Tagalog" },
                    { "tg", "Tajik" },
                    { "ta", "Tamil" },
                    { "te", "Telugu" },
                    { "th", "Thai" },
                    { "tr", "Turkish" },
                    { "uk", "Ukrainian" },
                    { "ur", "Urdu" },
                    { "uz", "Uzbek" },
                    { "vi", "Vietnamese" },
                    { "cy", "Welsh" },
                    { "xh", "Xhosa" },
                    { "yi", "Yiddish" },
                    { "yo", "Yoruba" },
                    { "zu", "Zulu" }
                };
        }
        return await Task.FromResult(lang_options);
    }
}