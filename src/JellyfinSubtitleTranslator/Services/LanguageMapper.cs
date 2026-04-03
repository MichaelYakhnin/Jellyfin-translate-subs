namespace JellyfinSubtitleTranslator.Services;

public interface ILanguageMapper
{
    string ToIso6392(string iso6391);
    string ToIso6391(string iso6392);
}

public class LanguageMapper : ILanguageMapper
{
    private static readonly Dictionary<string, string> Iso6391ToIso6392 = new(StringComparer.OrdinalIgnoreCase)
    {
        { "en", "eng" }, { "he", "heb" }, { "ar", "ara" }, { "ru", "rus" },
        { "fr", "fra" }, { "de", "deu" }, { "es", "spa" }, { "it", "ita" },
        { "pt", "por" }, { "nl", "nld" }, { "pl", "pol" }, { "ja", "jpn" },
        { "zh", "zho" }, { "ko", "kor" }, { "hi", "hin" }, { "tr", "tur" },
        { "vi", "vie" }, { "th", "tha" }, { "sv", "swe" }, { "da", "dan" },
        { "fi", "fin" }, { "no", "nor" }, { "cs", "ces" }, { "el", "ell" },
        { "hu", "hun" }, { "ro", "ron" }, { "uk", "ukr" }, { "id", "ind" },
        { "ms", "msa" }, { "fa", "fas" }, { "ur", "urd" }, { "bn", "ben" },
        { "ta", "tam" }, { "te", "tel" }, { "ml", "mal" }, { "mr", "mar" },
        { "bg", "bul" }, { "sr", "srp" }, { "hr", "hrv" }, { "sk", "slk" },
        { "sl", "slv" }, { "lt", "lit" }, { "lv", "lav" }, { "et", "est" }
    };

    private static readonly Dictionary<string, string> Iso6392ToIso6391 = Iso6391ToIso6392
        .ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

    public string ToIso6392(string iso6391)
    {
        if (string.IsNullOrEmpty(iso6391))
            return iso6391;

        if (iso6391.Length == 3)
            return iso6391.ToLowerInvariant();

        return Iso6391ToIso6392.TryGetValue(iso6391, out var iso6392) 
            ? iso6392 
            : iso6391.ToLowerInvariant();
    }

    public string ToIso6391(string iso6392)
    {
        if (string.IsNullOrEmpty(iso6392))
            return iso6392;

        if (iso6392.Length == 2)
            return iso6392.ToLowerInvariant();

        return Iso6392ToIso6391.TryGetValue(iso6392, out var iso6391) 
            ? iso6391 
            : iso6392.ToLowerInvariant();
    }
}
