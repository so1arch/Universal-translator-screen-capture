using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ScreenTranslator;

/// <summary>
/// Перевод EN→RU. Бесплатный endpoint Google, HTTP/2 keep-alive, параллельные запросы
/// и кэш (в памяти + на диске): повторяющийся текст интерфейса переводится мгновенно.
/// </summary>
sealed class Translator
{
    static readonly Regex Latin = new("[A-Za-z]{2,}", RegexOptions.Compiled);
    static readonly Regex Cyr = new("[\u0400-\u04FF]", RegexOptions.Compiled);
    static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
    static readonly string CachePath = Path.Combine(Settings.Dir, "cache.json");

    readonly HttpClient http;
    readonly ConcurrentDictionary<string, string> cache = new();
    readonly SemaphoreSlim gate = new(8);

    public string LastError;
    public int Count => cache.Count;

    public Translator()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            AutomaticDecompression = DecompressionMethods.All,
            EnableMultipleHttp2Connections = true
        };
        http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(6),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        Load();
    }

    public static string Norm(string s) => Spaces.Replace(s ?? "", " ").Trim();

    /// <summary>Есть ли смысл переводить строку (английский текст, не ссылка).</summary>
    public static bool Worth(string s) =>
        s.Length >= 2 && s.Length <= 400 && Latin.IsMatch(s) && !Cyr.IsMatch(s)
        && !s.Contains("://") && !s.Contains("www.");

    public bool TryGet(string s, out string t) => cache.TryGetValue(s, out t);

    public async Task<string> TranslateAsync(string s, CancellationToken ct)
    {
        if (cache.TryGetValue(s, out var hit)) return hit;

        await gate.WaitAsync(ct);
        try
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ru&dt=t&q="
                                 + Uri.EscapeDataString(s);
                    using var resp = await http.GetAsync(url, ct);
                    resp.EnsureSuccessStatusCode();
                    await using var st = await resp.Content.ReadAsStreamAsync(ct);
                    using var doc = await JsonDocument.ParseAsync(st, cancellationToken: ct);

                    var sb = new StringBuilder();
                    foreach (var seg in doc.RootElement[0].EnumerateArray())
                        if (seg[0].ValueKind == JsonValueKind.String) sb.Append(seg[0].GetString());

                    string res = sb.ToString().Trim();
                    if (res.Length == 0) return null;
                    cache[s] = res;
                    LastError = null;
                    return res;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    LastError = ex.Message;
                }
            }
            return null;
        }
        finally { gate.Release(); }
    }

    void Load()
    {
        try
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(CachePath));
            if (d != null) foreach (var kv in d) cache[kv.Key] = kv.Value;
        }
        catch { }
    }

    public void Save()
    {
        try
        {
            if (cache.IsEmpty) return;
            Directory.CreateDirectory(Settings.Dir);
            var d = cache.Take(30000).ToDictionary(k => k.Key, k => k.Value);
            File.WriteAllText(CachePath, JsonSerializer.Serialize(d));
        }
        catch { }
    }
}
