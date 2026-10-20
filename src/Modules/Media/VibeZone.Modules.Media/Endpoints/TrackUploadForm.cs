using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Media.Application;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Endpoints;

/// <summary>Documents the multipart body of <c>POST /track-uploads</c> for OpenAPI; the endpoint streams it itself.</summary>
public sealed class TrackUploadForm
{
    /// <summary>1–120 characters.</summary>
    public required string Title { get; init; }

    /// <summary>Optional, up to 2000 characters.</summary>
    public string? Description { get; init; }

    /// <summary>Optional genre slug from <c>GET /genres</c>.</summary>
    public string? GenreSlug { get; init; }

    /// <summary>Public, Unlisted or Private.</summary>
    public required string Visibility { get; init; }

    /// <summary>The original audio file.</summary>
    public required IFormFile Audio { get; init; }

    /// <summary>Optional cover image: JPEG, PNG or WebP.</summary>
    public IFormFile? Artwork { get; init; }
}

/// <summary>
/// Reads a <c>multipart/form-data</c> track upload as a stream (never <c>Request.Form</c>, which would buffer
/// it): text fields with a small cap, the cover up to the artwork limit, the audio spooled to a temp file.
/// The caller disposes the result (deletes the temp file).
/// </summary>
internal static class TrackUploadFormReader
{
    private const int MaxFieldBytes = 16 * 1024;

    private static readonly DomainError Malformed = DomainError.Validation("multipart_invalid", "The upload form is malformed.");

    public static async Task<Result<NewTrackUpload>> ReadAsync(HttpRequest request, Guid userId, string tempDirectory, MediaOptions settings, CancellationToken cancellationToken)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType) ||
            !contentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase) ||
            HeaderUtilities.RemoveQuotes(contentType.Boundary).Value is not { Length: > 0 and <= 70 } boundary)
        {
            return DomainError.Validation("multipart_required", "Send the track as multipart/form-data.");
        }

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        SpooledUpload? audio = null;
        string? fileName = null;
        string? audioType = null;
        byte[]? artwork = null;
        try
        {
            var reader = new MultipartReader(boundary, request.Body);
            while (await reader.ReadNextSectionAsync(cancellationToken) is { } section)
            {
                // IsFormDisposition() would mean "a field without a file name", so check the type itself.
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) ||
                    !disposition.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase))
                {
                    return Malformed;
                }

                var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value ?? string.Empty;
                if (disposition.IsFileDisposition())
                {
                    switch (name)
                    {
                        case "audio" when audio is null:
                            audio = await SpooledUpload.CreateAsync(section.Body, tempDirectory, settings.MaxUploadBytes, cancellationToken);
                            if (audio.TooLarge)
                            {
                                return UploadService.FileTooLarge(settings.MaxUploadBytes, "audio");
                            }

                            fileName = HeaderUtilities.RemoveQuotes(disposition.FileNameStar.HasValue ? disposition.FileNameStar : disposition.FileName).Value;
                            audioType = section.ContentType;
                            break;
                        case "artwork" when artwork is null:
                            artwork = await ArtworkService.ReadLimitedAsync(section.Body, settings.MaxArtworkBytes, cancellationToken);
                            if (artwork is null)
                            {
                                return ArtworkService.TooLarge(settings.MaxArtworkBytes);
                            }

                            // An empty file part means "no cover" (a browser form with nothing chosen).
                            artwork = artwork.Length == 0 ? null : artwork;
                            break;
                        default:
                            return Malformed;
                    }
                }
                else if (name is "title" or "description" or "genreSlug" or "visibility" && !fields.ContainsKey(name))
                {
                    var bytes = await ArtworkService.ReadLimitedAsync(section.Body, MaxFieldBytes, cancellationToken);
                    if (bytes is null)
                    {
                        return Malformed;
                    }

                    fields[name] = Encoding.UTF8.GetString(bytes);
                }
                else
                {
                    return Malformed;
                }
            }

            var draft = new NewTrackDraft(fields.GetValueOrDefault("title"), fields.GetValueOrDefault("description"), fields.GetValueOrDefault("genreSlug"), fields.GetValueOrDefault("visibility"));
            var upload = new NewTrackUpload(userId, draft, audio, fileName, audioType, artwork);
            audio = null; // ownership moves to the caller
            return upload;
        }
        catch (InvalidDataException)
        {
            // MultipartReader: bad boundary/headers or a header limit exceeded.
            return Malformed;
        }
        finally
        {
            audio?.Dispose();
        }
    }
}
