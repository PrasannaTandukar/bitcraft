using System.Buffers.Binary;
using Bitcraft.Application.Common.Interfaces;
using Bitcraft.Application.Common.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Bitcraft.Infrastructure.Chat;

public sealed class ChatImageNormalizer(ChatAttachmentLimits limits) : IChatImageNormalizer
{
    // v3 has no aggregate allocator budget. Serialize normalization so concurrent
    // decodes cannot multiply the per-allocation and decoded-pixel budgets.
    private static readonly SemaphoreSlim NormalizationGate = new(1, 1);
    private static readonly Configuration ImageConfiguration = new(
        new PngConfigurationModule(), new JpegConfigurationModule(), new WebpConfigurationModule())
    {
        MaxDegreeOfParallelism = 2,
        MemoryAllocator = MemoryAllocator.Create(new MemoryAllocatorOptions
        {
            AllocationLimitMegabytes = 256,
            MaximumPoolSizeMegabytes = 64
        })
    };

    public async Task<Result<NormalizedChatImage>> NormalizeAsync(ReadOnlyMemory<byte> data, string mimeType,
        CancellationToken cancellationToken = default)
    {
        if (mimeType is not ("image/png" or "image/jpeg" or "image/webp"))
            return Failure("ChatAttachment.UnsupportedType", "Choose a JPEG, PNG, or WebP image.");
        if (data.Length == 0) return Failure("ChatAttachment.InvalidImage", "The selected image is empty.");
        if (data.Length > limits.MaxFileBytes)
            return Failure("ChatAttachment.FileTooLarge", $"Images must be at most {limits.MaxFileBytes} bytes.");
        await NormalizationGate.WaitAsync(cancellationToken);
        try
        {
            var options = new DecoderOptions
            {
                Configuration = ImageConfiguration,
                MaxFrames = 2
            };
            using var input = new MemoryStream(data.ToArray(), writable: false);
            var info = await Image.IdentifyAsync(options, input, cancellationToken);
            if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > limits.MaxDecodedPixels)
                return Failure("ChatAttachment.InvalidImage", $"Image dimensions exceed the {limits.MaxDecodedPixels} pixel decode limit.");
            if (info.FrameMetadataCollection.Count > 1)
                return Failure("ChatAttachment.InvalidImage", "Animated images are not supported. Choose a still image.");
            if (info.Metadata.DecodedImageFormat?.DefaultMimeType != mimeType)
                return Failure("ChatAttachment.UnsupportedType", "The image contents do not match the supplied JPEG, PNG, or WebP type.");

            if (!HasCompleteContainer(data.Span, mimeType))
                return Failure("ChatAttachment.InvalidImage", "This image is truncated, animated, or has an invalid container.");
            input.Position = 0;
            // v3 exposes PNG CRC validation on its specialized decoder options.
            using var decoded = mimeType == "image/png"
                ? await PngDecoder.Instance.DecodeAsync<Rgba32>(new PngDecoderOptions
                {
                    GeneralOptions = options,
                    PngCrcChunkHandling = PngCrcChunkHandling.IgnoreNone,
                    MaxUncompressedAncillaryChunkSizeBytes = 1024 * 1024
                }, input, cancellationToken)
                : await Image.LoadAsync<Rgba32>(options, input, cancellationToken);
            if (decoded.Frames.Count != 1)
                return Failure("ChatAttachment.InvalidImage", "Animated images are not supported. Choose a still image.");
            decoded.Mutate(context =>
            {
                context.AutoOrient();
                if (decoded.Width > limits.MaxDimension || decoded.Height > limits.MaxDimension)
                    context.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(limits.MaxDimension, limits.MaxDimension) });
            });
            // A fresh pixel-only image strips all image/frame metadata, including EXIF/GPS and PNG text.
            using var clean = new Image<Rgba32>(ImageConfiguration, decoded.Width, decoded.Height);
            decoded.ProcessPixelRows(clean, (source, destination) =>
            {
                for (var row = 0; row < source.Height; row++)
                    source.GetRowSpan(row).CopyTo(destination.GetRowSpan(row));
            });
            using var output = new MemoryStream();
            // Lossless PNG retains diagram text, transparency, and avoids format-specific metadata.
            await clean.SaveAsync(output, new PngEncoder(), cancellationToken);
            if (output.Length > limits.MaxFileBytes)
                return Failure("ChatAttachment.FileTooLarge", "The normalized image is too large. Choose a smaller image.");
            return Result.Success(new NormalizedChatImage("image/png", output.ToArray(), clean.Width, clean.Height));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or InvalidMemoryOperationException or NotSupportedException or ArgumentException or OverflowException or IOException)
        {
            return Failure("ChatAttachment.InvalidImage", "This image is corrupted or cannot be decoded. Choose another JPEG, PNG, or WebP image.");
        }
        finally
        {
            NormalizationGate.Release();
        }
    }

    // Some decoders recover truncated streams. Require the format's terminal
    // marker or declared container length as well as a successful full decode.
    private static bool HasCompleteContainer(ReadOnlySpan<byte> data, string mimeType)
    {
        if (mimeType == "image/jpeg")
            return data.Length >= 4 && data[^2] == 0xff && data[^1] == 0xd9;
        if (mimeType == "image/png")
        {
            if (!data.EndsWith(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 })) return false;
            for (var offset = 8; offset < data.Length;)
            {
                if (data.Length - offset < 12) return false;
                var length = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
                if (length > data.Length - offset - 12) return false;
                var type = data.Slice(offset + 4, 4);
                // Reject APNG containers even if identical frames were coalesced
                // by the encoder or the decoder reports only the default image.
                if (type.SequenceEqual("acTL"u8) || type.SequenceEqual("fcTL"u8) || type.SequenceEqual("fdAT"u8)) return false;
                offset += (int)length + 12;
            }
            return true;
        }
        if (mimeType == "image/webp")
        {
            if (data.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian(data[4..8]) != data.Length - 8) return false;
            for (var offset = 12; offset < data.Length;)
            {
                if (data.Length - offset < 8) return false;
                var type = data.Slice(offset, 4);
                var length = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4));
                var paddedLength = (long)length + (length & 1);
                if (paddedLength > data.Length - offset - 8) return false;
                if (type.SequenceEqual("ANIM"u8) || type.SequenceEqual("ANMF"u8)) return false;
                if (type.SequenceEqual("VP8X"u8) && length > 0 && (data[offset + 8] & 2) != 0) return false;
                offset += (int)paddedLength + 8;
            }
            return true;
        }
        return false;
    }

    private static Result<NormalizedChatImage> Failure(string code, string description) =>
        Result.Failure<NormalizedChatImage>(new Error(code, description));
}
