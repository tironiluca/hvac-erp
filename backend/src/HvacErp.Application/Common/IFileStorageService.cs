namespace HvacErp.Application.Common;

/// <summary>
/// File storage with two areas. <b>Documents</b> — generated PDFs, quote photos — are private: they
/// are addressed by path, never by a lasting URL, and reach a browser only through a signed link
/// that expires. <b>Public assets</b> — the portal logo, favicon, login background — have no access
/// restriction by nature and get a permanent public URL.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Uploads (upserting) <paramref name="content"/> to <paramref name="path"/> among the private documents.</summary>
    /// <returns>The path to store on the owning row; it is all that is needed to sign or delete the object.</returns>
    Task<string> UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// A link to the private document at <paramref name="path"/> that works for <paramref name="lifetime"/>
    /// and for anyone holding it — hand it only to a caller already allowed to read the owning record.
    /// </summary>
    /// <exception cref="NotFoundException">No object exists at that path.</exception>
    Task<string> CreateSignedUrlAsync(string path, TimeSpan lifetime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the private document at <paramref name="path"/>. Best effort: returns false, without
    /// throwing, when the delete fails — a file left behind must not fail the request that removed its row.
    /// </summary>
    Task<bool> TryDeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>The bytes and content type of the private document at <paramref name="path"/>, or null when it cannot be read.</summary>
    Task<StoredFile?> TryDownloadDocumentAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Uploads (upserting) a public asset.</summary>
    /// <returns>Its permanent public URL.</returns>
    Task<string> UploadPublicAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Best-effort fetch of a public asset's bytes by its full URL (used to embed the brand logo
    /// in generated PDFs). Returns null on any failure — a missing/broken logo must never fail a PDF.
    /// </summary>
    Task<byte[]?> TryDownloadAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>A file read back from storage.</summary>
/// <param name="Content">Its bytes.</param>
/// <param name="ContentType">The content type it was stored with.</param>
public sealed record StoredFile(byte[] Content, string ContentType);

/// <summary>How long a link to a private document works.</summary>
public static class DocumentLinks
{
    /// <summary>
    /// Long enough to open or download the file right after asking for it, short enough that a link
    /// pasted into a chat or left in a browser history is dead soon after.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
}
