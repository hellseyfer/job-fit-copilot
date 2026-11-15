using JobFit.Api.Knowledge;

namespace JobFit.Api.Tests;

public class MarkdownChunkerTests
{
    [Fact]
    public void Headings_become_section_paths()
    {
        const string content = """
            # Experience

            ## Acme

            Built APIs in C#.

            ## Globex

            Ran Kubernetes clusters.

            # Education

            BSc in Computer Science.
            """;

        var chunks = MarkdownChunker.Chunk(content);

        Assert.Collection(
            chunks,
            c => Assert.Equal(("Experience > Acme", "Built APIs in C#."), (c.Section, c.Text)),
            c => Assert.Equal(("Experience > Globex", "Ran Kubernetes clusters."), (c.Section, c.Text)),
            c => Assert.Equal(("Education", "BSc in Computer Science."), (c.Section, c.Text)));
    }

    [Fact]
    public void Document_without_headings_has_empty_section_and_packs_paragraphs()
    {
        const string content = "First paragraph.\n\nSecond paragraph.\n";

        var chunk = Assert.Single(MarkdownChunker.Chunk(content));

        Assert.Equal("", chunk.Section);
        Assert.Equal("First paragraph.\n\nSecond paragraph.", chunk.Text);
    }

    [Fact]
    public void Paragraphs_are_packed_up_to_the_maximum_length()
    {
        var paragraph = string.Join(' ', Enumerable.Repeat("word", 140)); // 699 characters
        var content = string.Join("\n\n", Enumerable.Repeat(paragraph, 5));

        var chunks = MarkdownChunker.Chunk(content);

        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, c => Assert.True(c.Text.Length <= MarkdownChunker.MaxChunkLength));
        Assert.Equal(paragraph + "\n\n" + paragraph, chunks[0].Text);
    }

    [Fact]
    public void Long_paragraph_is_split_at_word_boundaries_with_overlap()
    {
        var words = Enumerable.Range(0, 800).Select(i => $"w{i:D3}");
        var content = string.Join(' ', words); // 800 * 5 - 1 = 3,999 characters

        var chunks = MarkdownChunker.Chunk(content);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c =>
        {
            Assert.True(c.Text.Length <= MarkdownChunker.MaxChunkLength);
            Assert.Contains(c.Text, content);
            Assert.Matches(@"^w\d{3}( w\d{3})*$", c.Text);
        });
        for (var i = 1; i < chunks.Count; i++)
        {
            var lastWordOfPrevious = chunks[i - 1].Text.Split(' ')[^1];
            Assert.Contains(lastWordOfPrevious, chunks[i].Text);
        }
        Assert.EndsWith("w799", chunks[^1].Text);
    }

    [Fact]
    public void Chunk_text_is_verbatim_and_indexes_are_sequential()
    {
        var long1 = string.Join(' ', Enumerable.Repeat("alpha", 500));
        var content = $"# Projects\r\n\r\n## Job Fit Copilot\r\n\r\n- RAG with pgvector\r\n- Evals\r\n\r\n{long1}\r\n\r\nTail.";

        var chunks = MarkdownChunker.Chunk(content);

        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.Index));
        Assert.All(chunks, c => Assert.Contains(c.Text, content));
        Assert.Equal("- RAG with pgvector\r\n- Evals", chunks[0].Text);
        Assert.All(chunks, c => Assert.Equal("Projects > Job Fit Copilot", c.Section));
    }

    [Fact]
    public void Hash_lines_inside_code_fences_are_not_headings()
    {
        const string content = "# Notes\n\n```bash\n# install\ndotnet build\n```\n";

        var chunk = Assert.Single(MarkdownChunker.Chunk(content));

        Assert.Equal("Notes", chunk.Section);
        Assert.Equal("```bash\n# install\ndotnet build\n```", chunk.Text);
    }

    [Fact]
    public void Headings_only_document_is_kept_as_text()
    {
        var chunk = Assert.Single(MarkdownChunker.Chunk("# Just a title\n"));

        Assert.Equal("# Just a title", chunk.Text);
    }
}
