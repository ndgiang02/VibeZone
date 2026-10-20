using System.Buffers.Binary;

namespace VibeZone.IntegrationTests.Infrastructure;

/// <summary>
/// Real audio generated in code (16-bit PCM WAV sine tones), so the tests need no binary fixtures and no
/// FFmpeg on the host. The worker container decodes and transcodes these like any user upload.
/// </summary>
public static class TestAudio
{
    public static byte[] Wav(TimeSpan duration, int sampleRate = 44_100, short channels = 2, double frequency = 440)
    {
        var frames = (int)(duration.TotalSeconds * sampleRate);
        var dataLength = frames * channels * 2;
        var wav = new byte[44 + dataLength];
        var span = wav.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + dataLength);
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], sampleRate * channels * 2);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], (short)(channels * 2));
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], 16);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], dataLength);

        var offset = 44;
        for (var i = 0; i < frames; i++)
        {
            // Slow amplitude swell so the waveform isn't flat.
            var envelope = 0.3 + (0.6 * Math.Abs(Math.Sin(Math.PI * i / sampleRate)));
            var sample = (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * envelope * short.MaxValue);
            for (var c = 0; c < channels; c++, offset += 2)
            {
                BinaryPrimitives.WriteInt16LittleEndian(span[offset..], sample);
            }
        }

        return wav;
    }

    /// <summary>A WAV header (passes the upload sniff) followed by bytes no decoder accepts.</summary>
    public static byte[] CorruptWav()
    {
        var bytes = new byte[64 * 1024];
        new Random(42).NextBytes(bytes);
        "RIFF"u8.CopyTo(bytes);
        "WAVE"u8.CopyTo(bytes.AsSpan(8));
        "JUNK"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), int.MaxValue);
        return bytes;
    }

    /// <summary>A PNG image: what a user gets when they rename a picture to <c>song.mp3</c>.</summary>
    public static byte[] RenamedPng()
    {
        var bytes = new byte[4096];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52 }.CopyTo(bytes, 0);
        return bytes;
    }
}
