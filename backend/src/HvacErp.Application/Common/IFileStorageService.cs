namespace HvacErp.Application.Common;

/// <summary>Uploads/deletes files (quote photos, floor plans, generated PDFs) in Supabase Storage.</summary>
public interface IFileStorageService
{
    /// <summary>Uploads (upserting) <paramref name="content"/> to <paramref name="path"/> in the documents bucket.</summary>
    /// <returns>The public URL of the uploaded object.</returns>
    Task<string> UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Deletes the object at <paramref name="path"/>; missing objects are not an error.</summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the object behind a URL previously returned by <see cref="UploadAsync"/>. Best effort:
    /// returns false, without throwing, when the URL is not one of this storage's or the delete fails.
    /// </summary>
    Task<bool> TryDeleteByUrlAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Best-effort fetch of a public object's bytes by its full URL (used to embed the brand logo
    /// in generated PDFs). Returns null on any failure — a missing/broken logo must never fail a PDF.
    /// </summary>
    Task<byte[]?> TryDownloadAsync(string url, CancellationToken cancellationToken = default);
}
