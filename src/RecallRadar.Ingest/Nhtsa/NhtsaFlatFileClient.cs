// Downloads NHTSA's investigations flat file. GET only (FR-002).
namespace RecallRadar.Ingest.Nhtsa;

/// <summary>
/// Fetches <c>FLAT_INV.zip</c> as bytes. The archive is a few megabytes and is parsed in memory by
/// <see cref="InvestigationFlatFileParser"/>; nothing is written to disk.
/// </summary>
public sealed class NhtsaFlatFileClient(HttpClient httpClient)
{
    /// <summary>Downloads the archive at the given absolute address.</summary>
    public async Task<byte[]> DownloadAsync(Uri archiveUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(archiveUri);
        if (!archiveUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The flat file address must be absolute.", nameof(archiveUri));
        }

        using var response = await httpClient.GetAsync(archiveUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
