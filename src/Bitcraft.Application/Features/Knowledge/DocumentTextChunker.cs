using System.Text;
using System.Text.RegularExpressions;
using Bitcraft.Application.Common.Models;

namespace Bitcraft.Application.Features.Knowledge;

public static partial class DocumentTextChunker
{
    public static Result<IReadOnlyList<string>> Split(
        string? text,
        int maxChunkLength,
        int chunkOverlap,
        int maxChunkCount)
    {
        if (maxChunkLength <= 0)
        {
            return Result.Failure<IReadOnlyList<string>>(new Error(
                "Knowledge.ChunkSizeInvalid",
                "Maximum chunk length must be positive."));
        }

        if (chunkOverlap < 0 || chunkOverlap >= maxChunkLength)
        {
            return Result.Failure<IReadOnlyList<string>>(new Error(
                "Knowledge.ChunkOverlapInvalid",
                "Chunk overlap must be at least zero and less than the maximum chunk length."));
        }

        if (maxChunkCount <= 0)
        {
            return Result.Failure<IReadOnlyList<string>>(new Error(
                "Knowledge.ChunkCountLimitInvalid",
                "Maximum chunk count must be positive."));
        }

        var normalized = Normalize(text);
        if (normalized.Length == 0)
        {
            return Result.Success<IReadOnlyList<string>>([]);
        }

        var chunks = new List<string>();
        var start = 0;
        var previousEnd = -1;

        while (start < normalized.Length)
        {
            var hardEnd = Math.Min(start + maxChunkLength, normalized.Length);
            var minimumEnd = previousEnd < 0
                ? start + 1
                : Math.Min(hardEnd, previousEnd + 1);
            var end = hardEnd == normalized.Length
                ? hardEnd
                : FindPreferredEnd(normalized, start, hardEnd, minimumEnd);

            if (end <= start)
            {
                end = hardEnd;
            }

            var chunk = normalized[start..end].Trim();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
                if (chunks.Count > maxChunkCount)
                {
                    return Result.Failure<IReadOnlyList<string>>(new Error(
                        "Knowledge.ChunkCountExceeded",
                        $"Vectorization would create more than {maxChunkCount} chunks. Increase the chunk size or use a smaller document."));
                }
            }

            if (end >= normalized.Length)
            {
                break;
            }

            previousEnd = end;
            start = Math.Max(start + 1, end - chunkOverlap);
        }

        return Result.Success<IReadOnlyList<string>>(chunks);
    }

    internal static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalizedLineEndings = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var builder = new StringBuilder(normalizedLineEndings.Length);
        var pendingBlankLine = false;

        foreach (var sourceLine in normalizedLineEndings.Split('\n'))
        {
            var line = HorizontalWhitespaceRegex().Replace(sourceLine.Trim(), " ");
            if (line.Length == 0)
            {
                if (builder.Length > 0)
                {
                    pendingBlankLine = true;
                }
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(pendingBlankLine ? "\n\n" : "\n");
            }

            builder.Append(line);
            pendingBlankLine = false;
        }

        return builder.ToString();
    }

    private static int FindPreferredEnd(string text, int start, int hardEnd, int minimumEnd)
    {
        var usefulMinimum = Math.Max(minimumEnd, start + Math.Max(1, (hardEnd - start) / 4));

        var paragraphEnd = text.LastIndexOf("\n\n", hardEnd - 1, hardEnd - start, StringComparison.Ordinal);
        if (paragraphEnd >= usefulMinimum)
        {
            return paragraphEnd;
        }

        for (var index = hardEnd - 1; index >= usefulMinimum; index--)
        {
            if (text[index] is '.' or '!' or '?' &&
                index + 1 < text.Length &&
                char.IsWhiteSpace(text[index + 1]))
            {
                return index + 1;
            }
        }

        for (var index = hardEnd - 1; index >= usefulMinimum; index--)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                return index;
            }
        }

        return hardEnd;
    }

    [GeneratedRegex("[\\t\\f\\v ]+")]
    private static partial Regex HorizontalWhitespaceRegex();
}
