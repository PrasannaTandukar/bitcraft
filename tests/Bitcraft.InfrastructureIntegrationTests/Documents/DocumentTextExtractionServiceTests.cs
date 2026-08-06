using System.Text;
using Bitcraft.Application.Common.Models;
using Bitcraft.Infrastructure.ExternalServices.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bitcraft.InfrastructureIntegrationTests.Documents;

public class DocumentTextExtractionServiceTests
{
    private readonly DocumentTextExtractionService service = new(
        DocumentIngestionLimits.Default,
        NullLogger<DocumentTextExtractionService>.Instance);

    [Theory]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("notes.md", "text/markdown")]
    [InlineData("notes.csv", "text/csv")]
    [InlineData("notes.json", "application/json")]
    public async Task ExtractAsync_PlainTextFormatsReadUtf8AndNormalizeLineEndings(string fileName, string contentType)
    {
        var result = await ExtractAsync(fileName, Encoding.UTF8.GetBytes("first\r\nsecond\rthird"));

        Assert.True(result.IsSuccess);
        Assert.Equal("first\nsecond\nthird", result.Value.Text);
        Assert.Equal(contentType, result.Value.DetectedContentType);
    }

    [Fact]
    public async Task ExtractAsync_HandlesUtf8Bom()
    {
        var payload = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("BOM text")).ToArray();
        var result = await ExtractAsync("bom.txt", payload);

        Assert.True(result.IsSuccess);
        Assert.Equal("BOM text", result.Value.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExtractAsync_HandlesUtf16Bom(bool bigEndian)
    {
        var encoding = new UnicodeEncoding(bigEndian, true);
        var payload = encoding.GetPreamble().Concat(encoding.GetBytes("Unicode text नेपाल")).ToArray();
        var result = await ExtractAsync("utf16.txt", payload);

        Assert.True(result.IsSuccess);
        Assert.Equal("Unicode text नेपाल", result.Value.Text);
    }

    [Fact]
    public async Task ExtractAsync_PdfReadsText()
    {
        var result = await ExtractAsync("fixture.pdf", BuildPdf("Hello from PDF"));

        Assert.True(result.IsSuccess);
        Assert.Contains("Hello from PDF", result.Value.Text, StringComparison.Ordinal);
        Assert.Equal("pdf-text", result.Value.ExtractionMethod);
    }

    [Fact]
    public async Task ExtractAsync_DocxReadsParagraphsAndTablesInOrder()
    {
        var result = await ExtractAsync("fixture.docx", BuildDocx(includeText: true));

        Assert.True(result.IsSuccess);
        Assert.Contains("First paragraph", result.Value.Text, StringComparison.Ordinal);
        Assert.Contains("Cell one\tCell two", result.Value.Text, StringComparison.Ordinal);
        Assert.True(result.Value.Text.IndexOf("First paragraph", StringComparison.Ordinal) <
                    result.Value.Text.IndexOf("Cell one", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExtractAsync_EmptyPdfAndDocxReturnEmptyTextForApplicationValidation()
    {
        var pdf = await ExtractAsync("empty.pdf", BuildPdf(string.Empty));
        var docx = await ExtractAsync("empty.docx", BuildDocx(includeText: false));

        Assert.True(pdf.IsSuccess);
        Assert.True(docx.IsSuccess);
        Assert.Equal(string.Empty, pdf.Value.Text);
        Assert.Equal(string.Empty, docx.Value.Text);
    }

    [Theory]
    [InlineData("corrupt.pdf")]
    [InlineData("corrupt.docx")]
    public async Task ExtractAsync_CorruptStructuredFileReturnsSafeFailure(string fileName)
    {
        var result = await ExtractAsync(fileName, "not a valid file"u8.ToArray());

        Assert.True(result.IsFailure);
        Assert.Equal("Document.TextExtractionFailed", result.Error.Code);
        Assert.DoesNotContain("exception", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_UnsupportedBinaryTypeIsRejected()
    {
        var result = await ExtractAsync("photo.png", [0x89, 0x50, 0x4E, 0x47]);

        Assert.True(result.IsFailure);
        Assert.Equal("Document.UnsupportedFileType", result.Error.Code);
    }

    [Fact]
    public async Task ExtractAsync_BinaryPayloadRenamedAsTextIsRejected()
    {
        var result = await ExtractAsync("renamed.txt", [0x00, 0x01, 0x02, 0x03]);

        Assert.True(result.IsFailure);
        Assert.Equal("Document.TextExtractionFailed", result.Error.Code);
    }

    [Fact]
    public async Task ExtractAsync_EnforcesExtractedCharacterLimit()
    {
        var limitedService = new DocumentTextExtractionService(
            new DocumentIngestionLimits(100, 3, 10),
            NullLogger<DocumentTextExtractionService>.Instance);

        var result = await limitedService.ExtractAsync(new DocumentTextExtractionRequest(
            "notes.txt",
            "text/plain",
            "four"u8.ToArray()));

        Assert.True(result.IsFailure);
        Assert.Equal("Document.ExtractedTextTooLarge", result.Error.Code);
    }

    [Fact]
    public async Task ExtractAsync_RespectsCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ExtractAsync(
            new DocumentTextExtractionRequest("notes.txt", "text/plain", "text"u8.ToArray()),
            source.Token));
    }

    private Task<Result<DocumentTextExtractionResult>> ExtractAsync(string fileName, byte[] content) =>
        service.ExtractAsync(new DocumentTextExtractionRequest(fileName, null, content));

    private static byte[] BuildDocx(bool includeText)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            var body = new Body();
            if (includeText)
            {
                body.Append(new Paragraph(new Run(new Text("First paragraph"))));
                body.Append(new Table(
                    new TableRow(
                        new TableCell(new Paragraph(new Run(new Text("Cell one")))),
                        new TableCell(new Paragraph(new Run(new Text("Cell two")))))));
            }
            mainPart.Document = new Document(body);
            mainPart.Document.Save();
        }
        return stream.ToArray();
    }

    private static byte[] BuildPdf(string text)
    {
        var escaped = text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
        var content = $"BT /F1 12 Tf 72 720 Td ({escaped}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 ").Append(objects.Length + 1).Append("\n");
        builder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            builder.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }
        builder.Append("trailer\n<< /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\n");
        builder.Append("startxref\n").Append(xrefOffset).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
