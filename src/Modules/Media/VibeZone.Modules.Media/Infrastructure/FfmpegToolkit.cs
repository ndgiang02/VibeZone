using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeZone.Modules.Media.Application;

namespace VibeZone.Modules.Media.Infrastructure;

/// <summary>
/// FFmpeg/ffprobe via <see cref="ProcessStartInfo.ArgumentList"/> — never a shell, never user input in
/// arguments (only server-generated temp paths). Cancellation kills the whole process tree.
/// </summary>
internal sealed partial class FfmpegToolkit(IOptions<MediaOptions> options, ILogger<FfmpegToolkit> logger) : IMediaToolkit
{
    private const int PeakSampleRate = 8000;
    private const int MaxStderrChars = 4000;

    public async Task<ProbeResult> ProbeAsync(string inputPath, CancellationToken cancellationToken)
    {
        var stdout = new MemoryStream();
        var (exitCode, stderr) = await RunAsync(
            options.Value.FfprobePath,
            ["-v", "error", "-print_format", "json", "-show_format", "-show_streams", inputPath],
            stdout,
            cancellationToken);
        if (exitCode != 0)
        {
            LogToolFailed(logger, "ffprobe", exitCode, stderr);
            throw new MediaProcessingException("corrupt_file", true, "The file could not be read as media.");
        }

        try
        {
            using var json = JsonDocument.Parse(stdout.ToArray());
            var format = json.RootElement.GetProperty("format");
            var formatName = format.TryGetProperty("format_name", out var fn) ? fn.GetString() ?? string.Empty : string.Empty;
            var duration = format.TryGetProperty("duration", out var d) && double.TryParse(d.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : double.NaN;

            AudioStreamInfo? audio = null;
            if (json.RootElement.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    if (stream.TryGetProperty("codec_type", out var type) && type.GetString() == "audio")
                    {
                        audio = new AudioStreamInfo(
                            stream.TryGetProperty("codec_name", out var codec) ? codec.GetString() ?? "unknown" : "unknown",
                            ReadInt(stream, "sample_rate"),
                            stream.TryGetProperty("channels", out var ch) && ch.TryGetInt32(out var channels) ? channels : null,
                            ReadInt(stream, "bit_rate"));
                        break;
                    }
                }
            }

            return new ProbeResult(formatName, duration, audio);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new MediaProcessingException("corrupt_file", true, "Unexpected probe output.", ex);
        }
    }

    public async Task TranscodeToAacAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        var (exitCode, stderr) = await RunAsync(
            options.Value.FfmpegPath,
            [
                "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
                "-i", inputPath,
                "-map", "0:a:0", "-vn", "-sn", "-dn", "-map_metadata", "-1",
                "-c:a", "aac", "-b:a", "256k", "-ar", "44100", "-ac", "2",
                "-movflags", "+faststart", "-f", "mp4", outputPath,
            ],
            null,
            cancellationToken);
        if (exitCode != 0)
        {
            LogToolFailed(logger, "ffmpeg", exitCode, stderr);
            throw new MediaProcessingException("transcode_failed", true, "The audio could not be converted.");
        }
    }

    public async Task<float[]> ComputePeaksAsync(string inputPath, double durationSeconds, int buckets, CancellationToken cancellationToken)
    {
        // Decode to 8 kHz mono 16-bit PCM on stdout and fold samples into fixed-size buckets as they stream.
        var expectedSamples = Math.Max(1, (long)Math.Ceiling(durationSeconds * PeakSampleRate));
        var samplesPerBucket = Math.Max(1, (long)Math.Ceiling(expectedSamples / (double)buckets));
        var sink = new PeakSink(samplesPerBucket, buckets);

        var (exitCode, stderr) = await RunAsync(
            options.Value.FfmpegPath,
            ["-nostdin", "-hide_banner", "-loglevel", "error", "-i", inputPath, "-ac", "1", "-ar", PeakSampleRate.ToString(CultureInfo.InvariantCulture), "-f", "s16le", "-"],
            sink,
            cancellationToken);
        if (exitCode != 0)
        {
            LogToolFailed(logger, "ffmpeg (peaks)", exitCode, stderr);
            throw new MediaProcessingException("transcode_failed", true, "The waveform could not be computed.");
        }

        return sink.Peaks();
    }

    private static int? ReadInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && int.TryParse(value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static async Task<(int ExitCode, string Stderr)> RunAsync(string fileName, IReadOnlyList<string> arguments, Stream? stdout, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = info };
        process.Start();
        await using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
        });

        var stderrTask = ReadBoundedAsync(process.StandardError);
        var stdoutTask = stdout is null
            ? process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, CancellationToken.None)
            : process.StandardOutput.BaseStream.CopyToAsync(stdout, CancellationToken.None);

        await process.WaitForExitAsync(CancellationToken.None);
        await Task.WhenAll(stdoutTask, stderrTask);
        cancellationToken.ThrowIfCancellationRequested();
        return (process.ExitCode, await stderrTask);
    }

    /// <summary>Keeps only the tail of stderr so a chatty or hostile file can't exhaust memory.</summary>
    private static async Task<string> ReadBoundedAsync(StreamReader reader)
    {
        var tail = new StringBuilder();
        var buffer = new char[1024];
        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
        {
            tail.Append(buffer, 0, read);
            if (tail.Length > MaxStderrChars)
            {
                tail.Remove(0, tail.Length - MaxStderrChars);
            }
        }

        return tail.ToString();
    }

    /// <summary>Write-only stream that turns s16le samples into per-bucket peak amplitudes.</summary>
    private sealed class PeakSink(long samplesPerBucket, int buckets) : Stream
    {
        private readonly List<float> _peaks = new(buckets + 1);
        private int _currentMax;
        private long _inBucket;
        private int _carry = -1;

        public float[] Peaks()
        {
            if (_inBucket > 0)
            {
                _peaks.Add(_currentMax / 32768f);
            }

            return [.. _peaks];
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> data)
        {
            var i = 0;
            if (_carry >= 0 && data.Length > 0)
            {
                Accept((short)(_carry | (data[0] << 8)));
                _carry = -1;
                i = 1;
            }

            for (; i + 1 < data.Length; i += 2)
            {
                Accept((short)(data[i] | (data[i + 1] << 8)));
            }

            if (i < data.Length)
            {
                _carry = data[i];
            }
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer.AsSpan(offset, count));
            return Task.CompletedTask;
        }

        private void Accept(short sample)
        {
            var amplitude = sample == short.MinValue ? 32768 : Math.Abs((int)sample);
            if (amplitude > _currentMax)
            {
                _currentMax = amplitude;
            }

            if (++_inBucket >= samplesPerBucket)
            {
                _peaks.Add(_currentMax / 32768f);
                _currentMax = 0;
                _inBucket = 0;
            }
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Tool} exited with {ExitCode}: {Stderr}")]
    private static partial void LogToolFailed(ILogger logger, string tool, int exitCode, string stderr);
}
