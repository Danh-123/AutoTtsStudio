using System.Text;
using System.Text.RegularExpressions;
using AutoTtsStudio.Models;

namespace AutoTtsStudio.Services;

/// <summary>
/// Service for importing English scripts (.txt and .srt) with smart sentence splitting.
/// </summary>
public static class TextImportService
{
    private static readonly string[] Abbreviations =
    {
        "Mr.", "Mrs.", "Ms.", "Dr.", "Prof.", "Sr.", "Jr.", "St.", "vs.", "etc.",
        "e.g.", "i.e.", "U.S.", "U.K.", "a.m.", "p.m.", "Jan.", "Feb.", "Mar.",
        "Apr.", "Aug.", "Sept.", "Oct.", "Nov.", "Dec.", "No.", "Gen.", "Col.",
        "Sgt.", "Lt.", "Capt.", "Maj.", "Brig.", "Adm.", "Cmdr.", "Gov.", "Sen.", "Rep."
    };

    /// <summary>
    /// Reads entire file with automatic UTF-8 BOM detection.
    /// </summary>
    public static string ReadAllTextWithEncoding(string filePath)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Imports a file (.txt or .srt) into a list of TtsLineItems.
    /// </summary>
    public static List<TtsLineItem> ImportFile(string filePath, bool autoSplit, int maxChars, ref int startIndex)
    {
        var content = ReadAllTextWithEncoding(filePath);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        List<string> segments;
        if (ext == ".srt")
        {
            segments = ParseSrt(content, autoSplit, maxChars);
        }
        else
        {
            segments = ParseTxt(content, autoSplit, maxChars);
        }

        return CreateLineItems(segments, ref startIndex);
    }

    /// <summary>
    /// Imports raw text directly (e.g. from clipboard or paste dialog) into a list of TtsLineItems.
    /// </summary>
    public static List<TtsLineItem> ImportRawText(string rawText, bool autoSplit, int maxChars, ref int startIndex)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return new List<TtsLineItem>();
        var segments = ParseTxt(rawText, autoSplit, maxChars);
        return CreateLineItems(segments, ref startIndex);
    }

    private static List<TtsLineItem> CreateLineItems(List<string> segments, ref int startIndex)
    {
        var items = new List<TtsLineItem>();
        foreach (var seg in segments)
        {
            if (string.IsNullOrWhiteSpace(seg)) continue;
            items.Add(new TtsLineItem
            {
                Index = startIndex++,
                Text = seg.Trim(),
                Status = TtsStatus.Pending
            });
        }
        return items;
    }

    /// <summary>
    /// Parses an SRT file content into clean subtitle blocks.
    /// </summary>
    public static List<string> ParseSrt(string content, bool autoSplit, int maxChars)
    {
        var result = new List<string>();
        var text = content.Replace("\r\n", "\n").Replace("\r", "\n");
        var blocks = Regex.Split(text, @"\n\s*\n");

        foreach (var block in blocks)
        {
            if (string.IsNullOrWhiteSpace(block)) continue;
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var textLines = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                // Skip subtitle sequence index (e.g. 1, 2, 3)
                if (int.TryParse(trimmed, out _)) continue;
                // Skip timestamp line (e.g. 00:00:01,000 --> 00:00:04,000)
                if (trimmed.Contains("-->")) continue;
                if (!string.IsNullOrEmpty(trimmed))
                    textLines.Add(trimmed);
            }

            if (textLines.Count == 0) continue;
            var joined = string.Join(" ", textLines).Trim();
            joined = Regex.Replace(joined, @"\s+", " ");

            if (autoSplit && joined.Length > maxChars)
            {
                result.AddRange(SplitEnglishText(joined, maxChars));
            }
            else
            {
                result.Add(joined);
            }
        }
        return result;
    }

    /// <summary>
    /// Parses TXT content: if autoSplit is off, each non-empty line is an item.
    /// If autoSplit is on, applies smart English sentence splitting.
    /// </summary>
    public static List<string> ParseTxt(string content, bool autoSplit, int maxChars)
    {
        if (!autoSplit)
        {
            return content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(l => l.Trim())
                          .Where(l => !string.IsNullOrEmpty(l))
                          .ToList();
        }
        return SplitEnglishText(content, maxChars);
    }

    /// <summary>
    /// Smart English sentence splitter:
    /// - Protects abbreviations (Mr., Mrs., U.S., a.m., p.m., etc.)
    /// - Protects decimals (3.14) and ellipsis (...)
    /// - Splits on sentence endings (. ! ? \n)
    /// - If a single sentence exceeds maxChars, soft-splits at commas/semicolons/spaces without breaking words.
    /// </summary>
    public static List<string> SplitEnglishText(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();
        if (maxChars <= 0) maxChars = 350;

        // Step 1: Protect ellipsis and decimals
        var protectedText = text;
        const string EllipsisToken = "___ELLIPSIS___";
        protectedText = protectedText.Replace("...", EllipsisToken);
        protectedText = protectedText.Replace("…", EllipsisToken);

        // Protect numbers with decimals e.g. 3.14
        protectedText = Regex.Replace(protectedText, @"(?<=\d)\.(?=\d)", "___NUMDOT___");

        // Protect known abbreviations
        var tokenMap = new Dictionary<string, string>();
        int tokenIdx = 0;
        foreach (var abbr in Abbreviations)
        {
            var pattern = @"\b" + Regex.Escape(abbr);
            var matches = Regex.Matches(protectedText, pattern, RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                var original = m.Value;
                if (!tokenMap.ContainsKey(original))
                {
                    var token = $"___ABBR{tokenIdx++}___";
                    tokenMap[original] = token;
                    protectedText = Regex.Replace(protectedText, @"\b" + Regex.Escape(original), token);
                }
            }
        }

        // Protect single letter initials like "J. K. Rowling" or "John F. Kennedy"
        protectedText = Regex.Replace(protectedText, @"\b([A-Z])\.\s+", "$1___INITIALDOT___ ");

        // Step 2: Split by sentence boundaries (. ! ? ... \n)
        var rawSentences = Regex.Split(protectedText, @"(?<=[.!?]|___ELLIPSIS___)\s+|\r?\n+")
                                .Select(s => s.Trim())
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();

        // Step 3: Restore protected tokens in each sentence
        var result = new List<string>();
        foreach (var raw in rawSentences)
        {
            var s = raw;
            s = s.Replace(EllipsisToken, "...");
            s = s.Replace("___NUMDOT___", ".");
            s = s.Replace("___INITIALDOT___", ".");
            foreach (var kvp in tokenMap)
            {
                s = s.Replace(kvp.Value, kvp.Key);
            }
            s = Regex.Replace(s, @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(s)) continue;

            // Step 4: If sentence <= maxChars, keep as-is. If > maxChars, soft-split!
            if (s.Length <= maxChars)
            {
                result.Add(s);
            }
            else
            {
                var subSentences = SoftSplitOversized(s, maxChars);
                result.AddRange(subSentences);
            }
        }

        return result;
    }

    /// <summary>
    /// Soft-splits a sentence exceeding maxChars by looking backwards for punctuation (, ; : -)
    /// then space, ensuring words are never split in half.
    /// </summary>
    private static List<string> SoftSplitOversized(string text, int maxChars)
    {
        var pieces = new List<string>();
        var remaining = text.Trim();

        while (remaining.Length > maxChars)
        {
            var slice = remaining.Substring(0, maxChars);

            int splitPos = -1;
            char[] puncts = { ',', ';', ':', '—' };
            foreach (var p in puncts)
            {
                int idx = slice.LastIndexOf(p);
                if (idx > splitPos && idx >= 20)
                {
                    splitPos = idx + 1;
                }
            }

            int dashIdx = slice.LastIndexOf(" - ", StringComparison.Ordinal);
            if (dashIdx > splitPos && dashIdx >= 20)
            {
                splitPos = dashIdx + 2;
            }

            if (splitPos <= 0)
            {
                splitPos = slice.LastIndexOf(' ');
            }

            if (splitPos <= 0)
            {
                splitPos = maxChars;
            }

            var chunk = remaining.Substring(0, splitPos).Trim();
            if (!string.IsNullOrEmpty(chunk))
                pieces.Add(chunk);

            remaining = remaining.Substring(splitPos).Trim();
        }

        if (!string.IsNullOrEmpty(remaining))
            pieces.Add(remaining);

        return pieces;
    }
}
