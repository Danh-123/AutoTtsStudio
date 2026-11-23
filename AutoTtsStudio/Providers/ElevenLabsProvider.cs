using System.Net;
using System.Text;
using System.Text.Json;
using AutoTtsStudio.Models;

namespace AutoTtsStudio.Providers;

/// <summary>
/// ElevenLabs API provider — BYOK (Bring Your Own Key).
/// </summary>
public sealed class ElevenLabsProvider : ITtsProvider
{
    private const string BaseUrl = "https://api.elevenlabs.io/v1";
    private readonly AppConfig _config;

    public ElevenLabsProvider(AppConfig config)
    {
        _config = config;
    }

    public ProviderType Type => ProviderType.ElevenLabs;

    public async Task<List<VoiceInfo>> GetVoicesAsync(string? languageFilter, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_config.ElevenLabsApiKey))
            return GetFallbackVoices();

        try
        {
            using var http = BuildClient(null);
            var resp = await http.GetAsync($"{BaseUrl}/voices", ct);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("ElevenLabs: API key không hợp lệ (401 Unauthorized). Hãy kiểm tra lại key trong Cài đặt.");
            }

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);
            return ParseVoices(json);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            return GetFallbackVoices();
        }
    }

    private static List<VoiceInfo> ParseVoices(string json)
    {
        var voices = new List<VoiceInfo>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("voices", out var arr)) return voices;

            foreach (var el in arr.EnumerateArray())
            {
                var id = el.TryGetProperty("voice_id", out var vid) ? vid.GetString() ?? "" : "";
                var name = el.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(id))
                {
                    voices.Add(new VoiceInfo
                    {
                        Id = id,
                        DisplayName = name,
                        Language = "en-US",
                        Provider = ProviderType.ElevenLabs
                    });
                }
            }
        }
        catch { }
        return voices;
    }

    private static List<VoiceInfo> GetFallbackVoices() => new()
    {
        new() { Id = "21m00Tcm4TlvDq8ikWAM", DisplayName = "Rachel (English, calm)",        Language = "en-US", Provider = ProviderType.ElevenLabs },
        new() { Id = "AZnzlk1XvdvUeBnXmlld", DisplayName = "Domi (English, strong)",        Language = "en-US", Provider = ProviderType.ElevenLabs },
        new() { Id = "EXAVITQu4vr4xnSDxMaL", DisplayName = "Bella (English, soft)",         Language = "en-US", Provider = ProviderType.ElevenLabs },
        new() { Id = "ErXwobaYiN019PkySvjV", DisplayName = "Antoni (English, well-rounded)", Language = "en-US", Provider = ProviderType.ElevenLabs },
        new() { Id = "MF3mGyEYCl7XYWbV9V6O", DisplayName = "Elli (English, emotional)",     Language = "en-US", Provider = ProviderType.ElevenLabs },
        new() { Id = "TxGEqnHWrfWFTfGW9XjX", DisplayName = "Josh (English, deep)",          Language = "en-US", Provider = ProviderType.ElevenLabs },
        new() { Id = "VR6AewLTigWG4xSOukaG", DisplayName = "Arnold (English, crisp)",       Language = "en-US", Provider = ProviderType.ElevenLabs },
        new() { Id = "pNInz6obpgDQGcFmaJgB", DisplayName = "Adam (English, deep, narr.)",   Language = "en-US", Provider = ProviderType.ElevenLabs },
        new() { Id = "yoZ06aMxZJJ28mfd3POQ", DisplayName = "Sam (English, raspy)",          Language = "en-US", Provider = ProviderType.ElevenLabs },
    };

    public async Task<string> SynthesizeToFileAsync(
        string text,
        VoiceSettings settings,
        string baseOutputPathWithoutExt,
        ProxyItem? proxy,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_config.ElevenLabsApiKey))
            throw new InvalidOperationException("Chưa cấu hình ElevenLabs API Key. Vào Cài đặt API / FFmpeg để nhập Key.");

        var voiceId = string.IsNullOrWhiteSpace(settings.VoiceId) ? "21m00Tcm4TlvDq8ikWAM" : settings.VoiceId;
        var modelId = string.IsNullOrWhiteSpace(settings.Model) ? "eleven_multilingual_v2" : settings.Model;

        var body = new
        {
            text = text,
            model_id = modelId,
            voice_settings = new
            {
                stability = Math.Round(settings.Stability / 100.0, 3),
                similarity_boost = Math.Round(settings.Similarity / 100.0, 3),
                style = Math.Round(settings.Style / 100.0, 3),
                use_speaker_boost = settings.SpeakerBoost,
                speed = Math.Round(settings.Speed, 2)
            }
        };

        var bodyJson = JsonSerializer.Serialize(body);
        using var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        using var http = BuildClient(proxy);
        using var response = await http.PostAsync($"{BaseUrl}/text-to-speech/{voiceId}", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            var err = await response.Content.ReadAsStringAsync(ct);
            var hint = statusCode switch
            {
                401 => " — Sai API Key. Vui lòng kiểm tra lại.",
                429 => " — Bị giới hạn tần suất (Rate limit). Vui lòng thử lại sau.",
                402 => " — Tài khoản hết ký tự / quota. Vui lòng nạp thêm credit.",
                _ => string.Empty
            };
            throw new HttpRequestException($"ElevenLabs {statusCode}{hint}\n{err.Truncate(300)}");
        }

        var outputPath = baseOutputPathWithoutExt + ".mp3";
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
        await stream.CopyToAsync(fs, ct);

        return outputPath;
    }

    private HttpClient BuildClient(ProxyItem? proxy)
    {
        var handler = new HttpClientHandler();
        if (proxy != null)
        {
            handler.Proxy = new WebProxy(proxy.Host, proxy.Port)
            {
                Credentials = string.IsNullOrEmpty(proxy.Username)
                    ? null
                    : new NetworkCredential(proxy.Username, proxy.Password)
            };
            handler.UseProxy = true;
        }

        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(90) };
        http.DefaultRequestHeaders.Add("xi-api-key", _config.ElevenLabsApiKey.Trim());
        return http;
    }
}

