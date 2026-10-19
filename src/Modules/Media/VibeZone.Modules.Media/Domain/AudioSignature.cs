namespace VibeZone.Modules.Media.Domain;

/// <summary>
/// Cheap first-bytes check at upload time so obviously non-audio files (renamed documents, executables,
/// images) are rejected before storage. Real validation is ffprobe in the worker; the client's file name
/// and MIME type are never trusted.
/// </summary>
public static class AudioSignature
{
    public const int BytesNeeded = 12;

    public static bool LooksLikeAudio(ReadOnlySpan<byte> head)
    {
        if (head.Length < BytesNeeded)
        {
            return false;
        }

        return StartsWith(head, "ID3"u8)                                   // MP3 with ID3v2 tag
            || (head[0] == 0xFF && (head[1] & 0xE0) == 0xE0)               // MPEG audio frame sync (MP3/ADTS AAC)
            || StartsWith(head, "fLaC"u8)                                  // FLAC
            || (StartsWith(head, "RIFF"u8) && head[8..12].SequenceEqual("WAVE"u8))  // WAV
            || StartsWith(head, "OggS"u8)                                  // Ogg Vorbis / Opus
            || head[4..8].SequenceEqual("ftyp"u8)                          // MP4 / M4A
            || (StartsWith(head, "FORM"u8) && (head[8..12].SequenceEqual("AIFF"u8) || head[8..12].SequenceEqual("AIFC"u8)))
            || (head[0] == 0x1A && head[1] == 0x45 && head[2] == 0xDF && head[3] == 0xA3); // Matroska / WebM audio
    }

    private static bool StartsWith(ReadOnlySpan<byte> data, ReadOnlySpan<byte> prefix) => data.StartsWith(prefix);
}
