using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AutoTtsStudio.Models;

namespace AutoTtsStudio.Providers;

/// <summary>
/// Provider for local OpenAI-compatible TTS servers (Kokoro CPU, Chatterbox TTS, etc.)
/// and remote Cloudflare-tunnelled Colab endpoints.
/// </summary>
public sealed class LocalOpenAiTtsProvider : ITtsProvider
{
    private readonly AppConfig _config;

    // Kokoro voice presets (user can also enter custom mix formulas or clone names)
    private static readonly string[] KokoroVoices =
    {
        "am_adam", "am_michael", "am_eric", "am_onyx",
        "bm_george", "bm_lewis",
        "af_heart", "af_bella",
        "bf_emma"
    };

    public LocalOpenAiTtsProvider(AppConfig config)
    {
        _config = config;
    }

    public ProviderType Type => ProviderType.LocalOpenAiApi;

    public async Task<List<VoiceInfo>> GetVoicesAsync(string? languageFilter, CancellationToken ct)
    {
        // 1. Try to fetch predefined voices from Chatterbox-TTS-Server or similar endpoint
        try
        {
            var rawUrl = string.IsNullOrWhiteSpace(_config.LocalApiUrl)
                ? "http://localhost:8880/v1/audio/speech"
                : _config.LocalApiUrl.Trim();

            if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            {
                var baseUrl = uri.GetLeftPart(UriPartial.Authority);
                var endpoint = $"{baseUrl.TrimEnd('/')}/get_predefined_voices";

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(8));

                using var handler = BuildHandler(null);
                using var http = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(8)
                };

                if (!string.IsNullOrWhiteSpace(_config.LocalApiKey))
                {
                    http.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", _config.LocalApiKey.Trim());
                }

                using var response = await http.GetAsync(endpoint, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cts.Token);
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<VoiceInfo>();
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            string? filename = null;
                            string? displayName = null;

                            if (el.TryGetProperty("filename", out var pFile))
                                filename = pFile.GetString();
                            else if (el.TryGetProperty("id", out var pId))
                                filename = pId.GetString();

                            if (el.TryGetProperty("display_name", out var pDisp))
                                displayName = pDisp.GetString();
                            else if (el.TryGetProperty("name", out var pName))
                                displayName = pName.GetString();

                            if (string.IsNullOrWhiteSpace(filename))
                                continue;

                            var disp = string.IsNullOrWhiteSpace(displayName)
                                ? filename
                                : (displayName.Contains(filename, StringComparison.OrdinalIgnoreCase)
                                    ? displayName
                                    : $"{displayName} ({filename})");

                            list.Add(new VoiceInfo
                            {
                                Id          = filename,
                                DisplayName = disp,
                                Language    = "en-US",
                                Gender      = "Custom",
                                Provider    = ProviderType.LocalOpenAiApi
                            });
                        }

                        if (list.Count > 0)
                        {
                            if (!string.IsNullOrWhiteSpace(languageFilter) &&
                                !string.Equals(languageFilter, "All", StringComparison.OrdinalIgnoreCase))
                            {
                                var filtered = list.Where(v => v.Language.Equals(languageFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                                if (filtered.Count > 0)
                                    return filtered;
                            }
                            return list;
                        }
                    }
                }
            }
        }
        catch
        {
            // Graceful fallback for any connection error, timeout, or offline server
        }

        // 2. Default fallback: Kokoro presets
        var kokoroList = KokoroVoices.Select(id => new VoiceInfo
        {
            Id          = id,
            DisplayName = id,
            Language    = "en-US",
            Gender      = id.StartsWith("af") || id.StartsWith("bf") ? "Female" : "Male",
            Provider    = ProviderType.LocalOpenAiApi
        }).ToList();

        if (!string.IsNullOrWhiteSpace(languageFilter) &&
            !string.Equals(languageFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            var filtered = kokoroList.Where(v => v.Language.Equals(languageFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            if (filtered.Count > 0)
                return filtered;
        }

        return kokoroList;
    }

    public async Task<string> SynthesizeToFileAsync(
        string text,
        VoiceSettings settings,
        string baseOutputPathWithoutExt,
        ProxyItem? proxy,
        CancellationToken ct)
    {
        var apiUrl = string.IsNullOrWhiteSpace(_config.LocalApiUrl)
            ? "http://localhost:8880/v1/audio/speech"
            : _config.LocalApiUrl.Trim();

        var model = string.IsNullOrWhiteSpace(settings.Model)
            ? (string.IsNullOrWhiteSpace(_config.LocalApiModel) ? "kokoro" : _config.LocalApiModel)
            : settings.Model;

        var voice = string.IsNullOrWhiteSpace(settings.VoiceId)
            ? "am_adam"
            : settings.VoiceId.Trim();

        // If voice has display format like "Eli (Eli.wav) (en-US)" or "Eli (Eli.wav)", extract "Eli.wav"
        var matchWav = System.Text.RegularExpressions.Regex.Match(voice, @"\(([^()]+\.(?:wav|mp3))\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (matchWav.Success)
        {
            voice = matchWav.Groups[1].Value.Trim();
        }

        // Build standard OpenAI TTS payload with response_format = "wav" for Chatterbox TTS Server
        var payload = new Dictionary<string, object>
        {
            ["model"] = model,
            ["input"] = text,
            ["voice"] = voice,
            ["speed"] = Math.Round(settings.Speed, 2),
            ["response_format"] = "wav"
        };

        // If custom params enabled, include Chatterbox params
        if (settings.CustomParams || settings.Style != 0 || settings.Stability != 50)
        {
            payload["exaggeration"] = Math.Round(settings.Style / 100.0, 2);
            payload["cfg_weight"] = Math.Round(settings.Stability / 100.0, 2);
        }

        var json = JsonSerializer.Serialize(payload);

        using var handler = BuildHandler(proxy);
        using var http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(120) // 120s timeout for Colab / slow GPU
        };

        if (!string.IsNullOrWhiteSpace(_config.LocalApiKey))
        {
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config.LocalApiKey.Trim());
        }

        using var requestContent = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(apiUrl, requestContent, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            var trimmedErr = err?.Replace("\r", " ").Replace("\n", " ").Trim() ?? string.Empty;
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {trimmedErr.Truncate(200)}");
        }

        // Auto-detect audio format from first 4 bytes
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var magic = new byte[4];
        var read = await stream.ReadAsync(magic.AsMemory(0, 4), ct);

        var isWav = read >= 4 && magic[0] == (byte)'R' && magic[1] == (byte)'I' && magic[2] == (byte)'F' && magic[3] == (byte)'F';
        var outputPath = baseOutputPathWithoutExt + (isWav ? ".wav" : ".mp3");

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
        await fs.WriteAsync(magic.AsMemory(0, read), ct);
        await stream.CopyToAsync(fs, ct);

        return outputPath;
    }

    private static HttpClientHandler BuildHandler(ProxyItem? proxy)
    {
        var handler = new HttpClientHandler();
        if (proxy != null)
        {
            handler.Proxy = new System.Net.WebProxy(proxy.Host, proxy.Port)
            {
                Credentials = string.IsNullOrEmpty(proxy.Username)
                    ? null
                    : new System.Net.NetworkCredential(proxy.Username, proxy.Password)
            };
            handler.UseProxy = true;
        }
        return handler;
    }
}

internal static class StringExtensions
{
    public static string Truncate(this string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}

