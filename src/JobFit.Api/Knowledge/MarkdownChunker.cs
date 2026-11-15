using System.Text.RegularExpressions;

namespace JobFit.Api.Knowledge;

public sealed record TextChunk(int Index, string Section, string Text);

// Splits Markdown or plain text into chunks that are verbatim slices of the content. Chunks follow
// headings (their path becomes the section) and pack whole paragraphs; a paragraph longer than the
// limit is split at word boundaries with overlap.
public static partial class MarkdownChunker
{
    public const int MaxChunkLength = 1_500;
    public const int Overlap = 200;
    public const string SectionSeparator = " > ";

    public static IReadOnlyList<TextChunk> Chunk(string content)
    {
        var chunks = new List<TextChunk>();
        foreach (var (section, paragraphs) in ReadSections(content))
        {
            PackParagraphs(content, section, paragraphs, chunks);
        }

        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(content))
        {
            // Only headings: keep the text itself rather than dropping the document.
            var start = content.Length - content.TrimStart().Length;
            var end = content.TrimEnd().Length;
            PackParagraphs(content, "", [(start, end)], chunks);
        }

        return chunks;
    }

    private static List<(string Section, List<(int Start, int End)> Paragraphs)> ReadSections(string content)
    {
        var sections = new List<(string, List<(int, int)>)>();
        var headings = new List<(int Level, string Title)>();
        var paragraphs = new List<(int, int)>();
        var section = "";
        int? paragraphStart = null;
        var paragraphEnd = 0;
        var inFence = false;

        void EndParagraph()
        {
            if (paragraphStart is { } start)
            {
                paragraphs.Add((start, paragraphEnd));
                paragraphStart = null;
            }
        }

        void EndSection()
        {
            EndParagraph();
            if (paragraphs.Count > 0)
            {
                sections.Add((section, paragraphs));
                paragraphs = [];
            }
        }

        var position = 0;
        while (position < content.Length)
        {
            var lineEnd = content.IndexOf('\n', position);
            if (lineEnd < 0)
            {
                lineEnd = content.Length;
            }

            var line = content[position..lineEnd].TrimEnd('\r');
            var trimmed = line.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
            }

            var heading = inFence ? Match.Empty : HeadingPattern().Match(line);
            if (heading.Success)
            {
                EndSection();
                var level = heading.Groups[1].Length;
                headings.RemoveAll(h => h.Level >= level);
                headings.Add((level, heading.Groups[2].Value.Trim()));
                section = string.Join(SectionSeparator, headings.Select(h => h.Title));
            }
            else if (trimmed.Length == 0 && !inFence)
            {
                EndParagraph();
            }
            else
            {
                var start = position + (line.Length - line.TrimStart().Length);
                paragraphStart ??= start;
                paragraphEnd = position + line.TrimEnd().Length;
            }

            position = lineEnd + 1;
        }

        EndSection();
        return sections;
    }

    private static void PackParagraphs(
        string content, string section, List<(int Start, int End)> paragraphs, List<TextChunk> chunks)
    {
        int? chunkStart = null;
        var chunkEnd = 0;

        void Flush()
        {
            if (chunkStart is { } start)
            {
                chunks.Add(new TextChunk(chunks.Count, section, content[start..chunkEnd]));
                chunkStart = null;
            }
        }

        foreach (var (start, end) in paragraphs)
        {
            if (end - start > MaxChunkLength)
            {
                Flush();
                foreach (var (pieceStart, pieceEnd) in SplitLongParagraph(content, start, end))
                {
                    chunks.Add(new TextChunk(chunks.Count, section, content[pieceStart..pieceEnd]));
                }
                continue;
            }

            if (chunkStart is { } current && end - current > MaxChunkLength)
            {
                Flush();
            }

            chunkStart ??= start;
            chunkEnd = end;
        }

        Flush();
    }

    private static IEnumerable<(int Start, int End)> SplitLongParagraph(string content, int start, int end)
    {
        var pieceStart = start;
        while (true)
        {
            if (end - pieceStart <= MaxChunkLength)
            {
                yield return (pieceStart, end);
                yield break;
            }

            var limit = pieceStart + MaxChunkLength;
            var pieceEnd = limit;
            // Cut at the last whitespace before the limit, unless the piece is one huge word.
            var lastSpace = content.LastIndexOfAny([' ', '\t', '\n', '\r'], limit, limit - pieceStart);
            if (lastSpace > pieceStart + Overlap)
            {
                pieceEnd = lastSpace;
            }

            var trimmedEnd = pieceEnd;
            while (trimmedEnd > pieceStart && char.IsWhiteSpace(content[trimmedEnd - 1]))
            {
                trimmedEnd--;
            }
            yield return (pieceStart, trimmedEnd);

            // Next piece starts about Overlap characters back, at the start of a word.
            var next = Math.Max(pieceEnd - Overlap, pieceStart + 1);
            while (next < pieceEnd && !char.IsWhiteSpace(content[next - 1]))
            {
                next++;
            }
            while (next < end && char.IsWhiteSpace(content[next]))
            {
                next++;
            }
            pieceStart = next;
        }
    }

    [GeneratedRegex(@"^\s{0,3}(#{1,6})\s+(.+?)\s*#*\s*$")]
    private static partial Regex HeadingPattern();
}
