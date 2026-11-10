namespace HvacErp.Application.Common;

/// <summary>
/// Decides what an uploaded file is from its first bytes. The content type a client sends is a
/// claim, not a fact: storing it as given would let anyone publish HTML or script from the storage
/// host under an image's name.
/// </summary>
public static class UploadedFileInspector
{
    /// <summary>How many leading bytes <see cref="DetectAttachmentType"/> needs.</summary>
    public const int HeaderLength = 12;

    /// <summary>What may be attached to a quote, as told to the user.</summary>
    public const string AllowedAttachmentsDescription = "photos (JPEG, PNG, WebP, GIF, HEIC) and PDF files";

    /// <summary>
    /// The content type of a photo or PDF recognised by its signature, or null for anything else.
    /// </summary>
    public static string? DetectAttachmentType(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith<byte>([0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (header.StartsWith<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (header.StartsWith("%PDF-"u8))
        {
            return "application/pdf";
        }

        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
        {
            return "image/gif";
        }

        if (header.Length >= 12 && header.StartsWith("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        // ISO base media: bytes 4-7 are "ftyp", 8-11 the brand. Phones save photos as HEIC / HEIF.
        if (header.Length >= 12 && header[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = header[8..12];
            if (brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8) || brand.SequenceEqual("hevc"u8))
            {
                return "image/heic";
            }

            if (brand.SequenceEqual("mif1"u8) || brand.SequenceEqual("heif"u8) || brand.SequenceEqual("msf1"u8))
            {
                return "image/heif";
            }
        }

        return null;
    }
}
