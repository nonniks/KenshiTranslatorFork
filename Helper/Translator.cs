using System;
using System.Net.Http.Json;
using System.Web;
using NTextCat;
public enum TranslationProvider
{
    Google,
    Libre
}

public static class Translator
{
    
    private static readonly HttpClient client = new HttpClient();

    public static TranslationProvider Provider { get; set; } = TranslationProvider.Google;
    public static string LibreUrl { get; set; } = "https://libretranslate.com/translate";

    public static async Task<string> Translate(string text, string targetLang = "en", string sourceLang = "auto")
    {
        switch (Provider)
        {
            case TranslationProvider.Google:
                return await TranslateGoogle(text, targetLang, sourceLang);
            case TranslationProvider.Libre:
                return await TranslateLibre(text, targetLang, sourceLang);
            default:
                throw new NotImplementedException();
        }
    }
   

    private static async Task<string> TranslateGoogle(string text, string targetLang, string sourceLang)
    {
        string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sourceLang}&tl={targetLang}&dt=t&q={HttpUtility.UrlEncode(text)}";
        var response = await client.GetStringAsync(url);
        var result = System.Text.Json.JsonSerializer.Deserialize<object[][][]>(response);
        return result?[0][0][0]?.ToString() ?? text;
    }

    private static async Task<string> TranslateLibre(string text, string targetLang, string sourceLang)
    {
        var payload = new { q = text, source = sourceLang, target = targetLang, format = "text" };
        var response = await client.PostAsJsonAsync(LibreUrl, payload);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return result?["translatedText"] ?? text;
    }
}