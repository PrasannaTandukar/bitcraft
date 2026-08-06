using System.Runtime.InteropServices;
using System.Text;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Bitcraft.Infrastructure.ExternalServices.Documents;

public sealed class DocumentTextExtractionService(
    DocumentIngestionLimits limits,
    ILogger<DocumentTextExtractionService> logger) : IDocumentTextExtractionService
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly UnicodeEncoding StrictUtf16Le = new(false, true, true);
    private static readonly UnicodeEncoding StrictUtf16Be = new(true, true, true);

    public Task<Result<DocumentTextExtractionResult>> ExtractAsync(
        DocumentTextExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!DocumentFileTypes.SupportedExtensions.Contains(extension))
        {
            return Task.FromResult(Result.Failure<DocumentTextExtractionResult>(new Error(
                "Document.UnsupportedFileType",
                "Supported file types are TXT, Markdown, CSV, JSON, PDF, and DOCX.")));
        }

        try
        {
            var result = extension switch
            {
                ".txt" => ExtractPlainText(request.Content, "text/plain", "plain-text"),
                ".md" => ExtractPlainText(request.Content, "text/markdown", "plain-text"),
                ".csv" => ExtractPlainText(request.Content, "text/csv", "plain-text"),
                ".json" => ExtractPlainText(request.Content, "application/json", "plain-text"),
                ".pdf" => ExtractPdf(request.Content, cancellationToken),
                ".docx" => ExtractDocx(request.Content, cancellationToken),
                _ => throw new InvalidOperationException("Unsupported extraction strategy.")
            };

            if (result.Text.Length > limits.MaxExtractedCharacters)
            {
                return Task.FromResult(Result.Failure<DocumentTextExtractionResult>(new Error(
                    "Document.ExtractedTextTooLarge",
                    $"Extracted text exceeds the {limits.MaxExtractedCharacters} character limit.")));
            }

            return Task.FromResult(Result.Success(result));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ExtractedTextTooLargeException)
        {
            return Task.FromResult(Result.Failure<DocumentTextExtractionResult>(new Error(
                "Document.ExtractedTextTooLarge",
                $"Extracted text exceeds the {limits.MaxExtractedCharacters} character limit.")));
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Document text extraction failed for extension {FileExtension}. ErrorType={ErrorType}",
                extension,
                ex.GetType().Name);
            return Task.FromResult(Result.Failure<DocumentTextExtractionResult>(new Error(
                "Document.TextExtractionFailed",
                "Text could not be extracted from this file. The file may be corrupt, encrypted, or unreadable.")));
        }
    }

    private static DocumentTextExtractionResult ExtractPlainText(
        ReadOnlyMemory<byte> content,
        string contentType,
        string method)
    {
        var span = content.Span;
        string text;

        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
        {
            text = StrictUtf8.GetString(span[3..]);
        }
        else if (span.Length >= 2 && span[0] == 0xFF && span[1] == 0xFE)
        {
            text = StrictUtf16Le.GetString(span[2..]);
        }
        else if (span.Length >= 2 && span[0] == 0xFE && span[1] == 0xFF)
        {
            text = StrictUtf16Be.GetString(span[2..]);
        }
        else
        {
            text = StrictUtf8.GetString(span);
        }

        if (text.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t'))
        {
            throw new InvalidDataException("Plain-text input contains unsupported control characters.");
        }

        return new DocumentTextExtractionResult(
            NormalizeLineEndings(text),
            contentType,
            method);
    }

    private DocumentTextExtractionResult ExtractPdf(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        using var stream = CreateReadOnlyStream(content);
        using var pdf = PdfDocument.Open(stream);
        var pages = new List<string>();

        foreach (var page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageText = NormalizeLineEndings(ContentOrderTextExtractor.GetText(page)).Trim();
            if (pageText.Length > 0)
            {
                pages.Add(pageText);
                EnsureWithinExtractedTextLimit(pages.Sum(value => value.Length) + ((pages.Count - 1) * 2));
            }
        }

        return new DocumentTextExtractionResult(
            string.Join("\n\n", pages),
            "application/pdf",
            "pdf-text");
    }

    private DocumentTextExtractionResult ExtractDocx(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        using var stream = CreateReadOnlyStream(content);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body
            ?? throw new InvalidDataException("DOCX main document body is missing.");
        var blocks = new List<string>();

        foreach (var element in body.ChildElements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (element)
            {
                case Paragraph paragraph:
                    AddNonEmpty(blocks, ReadParagraph(paragraph));
                    EnsureWithinExtractedTextLimit(blocks.Sum(value => value.Length) + ((blocks.Count - 1) * 2));
                    break;
                case Table table:
                    AddNonEmpty(blocks, ReadTable(table));
                    EnsureWithinExtractedTextLimit(blocks.Sum(value => value.Length) + ((blocks.Count - 1) * 2));
                    break;
            }
        }

        return new DocumentTextExtractionResult(
            NormalizeLineEndings(string.Join("\n\n", blocks)),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "docx-openxml");
    }

    private static string ReadParagraph(Paragraph paragraph)
    {
        var builder = new StringBuilder();
        foreach (var node in paragraph.Descendants<OpenXmlElement>())
        {
            switch (node)
            {
                case Text text:
                    builder.Append(text.Text);
                    break;
                case TabChar:
                    builder.Append('\t');
                    break;
                case Break:
                case CarriageReturn:
                    builder.Append('\n');
                    break;
            }
        }

        return builder.ToString().Trim();
    }

    private static string ReadTable(Table table)
    {
        var rows = new List<string>();
        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>()
                .Select(cell => string.Join(" ", cell.Elements<Paragraph>()
                    .Select(ReadParagraph)
                    .Where(text => text.Length > 0)))
                .ToList();
            if (cells.Any(cell => cell.Length > 0))
            {
                rows.Add(string.Join("\t", cells));
            }
        }

        return string.Join("\n", rows);
    }

    private static MemoryStream CreateReadOnlyStream(ReadOnlyMemory<byte> content)
    {
        if (MemoryMarshal.TryGetArray(content, out var segment) && segment.Array is not null)
        {
            return new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false, publiclyVisible: true);
        }

        return new MemoryStream(content.ToArray(), writable: false);
    }

    private static void AddNonEmpty(ICollection<string> target, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target.Add(value.Trim());
        }
    }

    private static string NormalizeLineEndings(string text) => text
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .Trim();

    private void EnsureWithinExtractedTextLimit(int length)
    {
        if (length > limits.MaxExtractedCharacters)
        {
            throw new ExtractedTextTooLargeException();
        }
    }

    private sealed class ExtractedTextTooLargeException : Exception;
}
