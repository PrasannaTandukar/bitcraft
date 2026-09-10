using System.Buffers.Binary;
using System.Text;
using Bitcraft.Application.Common.Models;
using Bitcraft.Application.Features.Chat;
using Bitcraft.Infrastructure.Chat;
using Bitcraft.UnitTests.Application.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bitcraft.UnitTests.Infrastructure.Chat;

public sealed class ChatAttachmentTests
{
    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public async Task ValidImageIsDecodedResizedAndMetadataStripped(string mimeType)
    {
        var normalizer = new ChatImageNormalizer(new ChatAttachmentLimits(MaxDimension: 64));
        var bytes = await FixtureAsync(mimeType, 200, 100);
        var result = await normalizer.NormalizeAsync(bytes, mimeType);
        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal("image/png", result.Value.MimeType);
        Assert.Equal(64, result.Value.Width);
        Assert.Equal(32, result.Value.Height);
        using var decoded = Image.Load(result.Value.Data.Span);
        Assert.Null(decoded.Metadata.ExifProfile);
        Assert.Null(decoded.Metadata.XmpProfile);
        Assert.Null(decoded.Metadata.IccProfile);
        Assert.Empty(decoded.Metadata.GetPngMetadata().TextData);
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("application/octet-stream")]
    public async Task UnsupportedMimeIsRejected(string mimeType)
    {
        var result = await new ChatImageNormalizer(new()).NormalizeAsync(await FixtureAsync("image/png"), mimeType);
        Assert.Equal("ChatAttachment.UnsupportedType", result.Error.Code);
    }

    [Fact]
    public async Task ForgedCorruptedEmptyOversizedAndDimensionBombAreRejected()
    {
        var normalizer = new ChatImageNormalizer(new());
        var fake = await normalizer.NormalizeAsync(Encoding.UTF8.GetBytes("Not an image"), "image/png");
        Assert.Equal("ChatAttachment.InvalidImage", fake.Error.Code);
        var png = await FixtureAsync("image/png");
        var corrupted = await normalizer.NormalizeAsync(png[..(png.Length / 2)], "image/png");
        Assert.Equal("ChatAttachment.InvalidImage", corrupted.Error.Code);
        var mismatch = await normalizer.NormalizeAsync(png, "image/jpeg");
        Assert.Equal("ChatAttachment.UnsupportedType", mismatch.Error.Code);
        Assert.Equal("ChatAttachment.InvalidImage", (await normalizer.NormalizeAsync(Array.Empty<byte>(), "image/png")).Error.Code);
        var smallLimit = new ChatImageNormalizer(new ChatAttachmentLimits(MaxFileBytes: png.Length - 1));
        Assert.Equal("ChatAttachment.FileTooLarge", (await smallLimit.NormalizeAsync(png, "image/png")).Error.Code);
        var pixelLimit = new ChatImageNormalizer(new ChatAttachmentLimits(MaxDecodedPixels: 10));
        Assert.Equal("ChatAttachment.InvalidImage", (await pixelLimit.NormalizeAsync(png, "image/png")).Error.Code);
    }

    [Fact]
    public async Task AnimatedPngIsRejected()
    {
        using var source = new Image<Rgba32>(4, 4);
        source.Frames.AddFrame(source.Frames.RootFrame);
        using var output = new MemoryStream();
        await source.SaveAsPngAsync(output);
        Assert.Contains("acTL", Encoding.ASCII.GetString(output.ToArray()));
        var result = await new ChatImageNormalizer(new()).NormalizeAsync(output.ToArray(), "image/png");
        Assert.Equal("ChatAttachment.InvalidImage", result.Error.Code);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public async Task TruncatedContainersAreRejectedEvenIfDecoderCanRecover(string mimeType)
    {
        var bytes = await FixtureAsync(mimeType);
        var normalizer = new ChatImageNormalizer(new());
        foreach (var truncated in new[] { bytes[..(bytes.Length / 2)], bytes[..^2] })
        {
            var result = await normalizer.NormalizeAsync(truncated, mimeType);
            Assert.Equal("ChatAttachment.InvalidImage", result.Error.Code);
        }
    }

    [Fact]
    public async Task InvalidAncillaryPngCrcIsRejected()
    {
        var bytes = await FixtureAsync("image/png");
        var found = false;
        for (var offset = 8; offset < bytes.Length;)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
            if (Encoding.ASCII.GetString(bytes, offset + 4, 4) == "eXIf")
            {
                bytes[offset + 8 + length] ^= 1; // Damage metadata CRC without changing decoded pixels.
                found = true;
                break;
            }
            offset += length + 12;
        }
        Assert.True(found);
        var result = await new ChatImageNormalizer(new()).NormalizeAsync(bytes, "image/png");
        Assert.Equal("ChatAttachment.InvalidImage", result.Error.Code);
    }

    [Fact]
    public async Task AnimatedWebpIsRejected()
    {
        using var source = new Image<Rgba32>(4, 4);
        source.Frames.AddFrame(source.Frames.RootFrame);
        using var output = new MemoryStream();
        await source.SaveAsWebpAsync(output);
        var result = await new ChatImageNormalizer(new()).NormalizeAsync(output.ToArray(), "image/webp");
        Assert.Equal("ChatAttachment.InvalidImage", result.Error.Code);
    }

    [Fact]
    public async Task CancellationDoesNotConsumeTheNormalizationSlot()
    {
        var normalizer = new ChatImageNormalizer(new());
        var bytes = await FixtureAsync("image/png");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => normalizer.NormalizeAsync(bytes, "image/png", cancellation.Token));
        Assert.True((await normalizer.NormalizeAsync(bytes, "image/png")).IsSuccess);
    }

    [Fact]
    public async Task ServiceEnforcesOwnershipExpiryDeletionAndBoundedStreams()
    {
        var limits = new ChatAttachmentLimits(MaxDimension: 64);
        var repository = new FakeChatAttachmentRepository();
        var store = new FakeChatAttachmentStore();
        var time = new FakeAttachmentTimeProvider(DateTimeOffset.UtcNow);
        var service = new ChatAttachmentService(repository, store, new ChatImageNormalizer(limits), limits, time, NullLogger<ChatAttachmentService>.Instance);
        var owner = Guid.NewGuid();
        using var stream = new MemoryStream(await FixtureAsync("image/png"));
        var upload = await service.UploadAsync(owner, "../../circuit.png", "image/png", stream);
        Assert.True(upload.IsSuccess, upload.Error.Description);
        Assert.Equal("circuit.png", upload.Value.FileName);
        var id = upload.Value.Id;
        Assert.Equal(time.UtcNow.AddHours(24).UtcDateTime, upload.Value.ExpiresUtc);
        Assert.Equal(owner, Assert.Single(repository.Items).CreatedById);
        Assert.True((await service.ReadAsync(id, owner)).IsSuccess);
        Assert.Equal("ChatAttachment.NotFound", (await service.ReadAsync(id, Guid.NewGuid())).Error.Code);
        Assert.Equal("ChatAttachment.NotFound", (await service.DeleteAsync(id, Guid.NewGuid())).Error.Code);
        var reads = store.ReadCount;
        time.UtcNow = time.UtcNow.AddHours(24);
        Assert.Equal("ChatAttachment.Expired", (await service.ReadAsync(id, owner)).Error.Code);
        Assert.Equal(reads, store.ReadCount);
        Assert.True((await service.DeleteAsync(id, owner)).IsSuccess);
        Assert.Empty(repository.Items);
        Assert.Empty(store.Data);
        Assert.Equal("ChatAttachment.NotFound", (await service.ReadAsync(id, owner)).Error.Code);
        using var oversized = new MemoryStream(new byte[limits.MaxFileBytes + 1]);
        Assert.Equal("ChatAttachment.FileTooLarge", (await service.UploadAsync(owner, "big.png", "image/png", oversized)).Error.Code);
    }

    [Fact]
    public async Task CleanupRemovesExpiredMetadataAndBytesButKeepsLiveAttachments()
    {
        var limits = new ChatAttachmentLimits();
        var repository = new FakeChatAttachmentRepository();
        var store = new FakeChatAttachmentStore();
        var time = new FakeAttachmentTimeProvider(DateTimeOffset.UtcNow);
        var service = new ChatAttachmentService(repository, store, new ChatImageNormalizer(limits), limits, time, NullLogger<ChatAttachmentService>.Instance);
        var owner = Guid.NewGuid();
        using var first = new MemoryStream(await FixtureAsync("image/png"));
        var expired = await service.UploadAsync(owner, "old.png", "image/png", first);
        time.UtcNow = time.UtcNow.AddHours(23);
        using var second = new MemoryStream(await FixtureAsync("image/png"));
        var live = await service.UploadAsync(owner, "new.png", "image/png", second);
        time.UtcNow = time.UtcNow.AddHours(2);
        await service.CleanupExpiredAsync();
        Assert.Equal(live.Value.Id, Assert.Single(repository.Items).Id);
        Assert.Single(store.Data);
        Assert.Equal("ChatAttachment.NotFound", (await service.ReadAsync(expired.Value.Id, owner)).Error.Code);
    }

    [Fact]
    public async Task FileStoreRejectsTraversalAndSweepsExpiredOrphanAndPartialFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bitcraft-attachment-test-{Guid.NewGuid():N}");
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ChatAttachments:RootPath"] = root }).Build();
            var store = new FileSystemChatAttachmentStore(configuration);
            var live = $"{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}.{Guid.NewGuid():N}";
            var expired = $"{DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds()}.{Guid.NewGuid():N}";
            await store.SaveAsync(live, new byte[] { 1, 2 });
            await store.SaveAsync(expired, new byte[] { 3 });
            await File.WriteAllBytesAsync(Path.Combine(root, expired + ".tmp"), [4]);
            await store.CleanupExpiredAsync(DateTime.UtcNow);
            Assert.Single(Directory.EnumerateFiles(root));
            await using var content = await store.OpenReadAsync(live);
            Assert.Equal(2, content!.Length);
            await Assert.ThrowsAsync<ArgumentException>(() => store.OpenReadAsync("../escape"));
            await store.DeleteAsync(live);
            Assert.Null(await store.OpenReadAsync(live));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static async Task<byte[]> FixtureAsync(string mimeType, int width = 12, int height = 8)
    {
        using var image = new Image<Rgba32>(width, height, Color.CornflowerBlue.ToPixel<Rgba32>());
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.ImageDescription, "private metadata");
        using var stream = new MemoryStream();
        IImageEncoder encoder = mimeType switch { "image/jpeg" => new JpegEncoder(), "image/webp" => new WebpEncoder(), _ => new PngEncoder() };
        await image.SaveAsync(stream, encoder);
        return stream.ToArray();
    }
}
