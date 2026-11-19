using System.Diagnostics;
using Noetix.Sdk.Bumi;

// Media path check: microphone capture, speaker playback and camera frames through MediaController.
// Nothing here moves the robot; it only subscribes to media topics and publishes PCM or a tone.
// Usage: MediaTest [path-to-dds.xml] [status|record|play|playfile|playstdin|video|all|volume] [args] [output-dir]
var ddsConfig = args.Length > 0 ? args[0] : Path.Combine("config", "dds.xml");
var command = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
var known = new[] { "status", "record", "play", "playfile", "playstdin", "loopback", "video", "all", "volume" };
if (!known.Contains(command))
{
    Console.Error.WriteLine($"Unknown command '{command}'. Known: {string.Join(", ", known)}");
    return 64;
}

// playfile takes a WAV path where the other commands take a duration.
var seconds = 5.0;
string? inputFile = null;
string outDir;
if (command == "playfile")
{
    inputFile = args.Length > 2 ? args[2] : null;
    if (string.IsNullOrEmpty(inputFile) || !File.Exists(inputFile))
    {
        Console.Error.WriteLine($"playfile needs an existing 16-bit PCM WAV path, got '{inputFile}'.");
        return 64;
    }

    outDir = args.Length > 3 ? args[3] : "media-out";
}
else if (command == "playstdin")
{
    // args[2] is the sample rate and args[3] the channel count.
    outDir = args.Length > 4 ? args[4] : "media-out";
}
else
{
    seconds = args.Length > 2 && double.TryParse(args[2], out var parsedSeconds) ? parsedSeconds : 5;
    outDir = args.Length > 3 ? args[3] : "media-out";
}

Directory.CreateDirectory(outDir);

Console.WriteLine($"Native SDK version: {NoetixSdk.NativeSdkVersion}");
NoetixSdk.CallbackError += (_, ex) => Console.Error.WriteLine($"callback error: {ex}");
NoetixSdk.SetDdsConfig(ddsConfig);

var media = MediaController.Instance;
if (!media.Init())
{
    Console.Error.WriteLine("MediaController.Init() returned false.");
    return 1;
}

Console.WriteLine($"Volume={media.GetVolume()} timeout={media.GetTimeout()}ms cue={media.GetAudioCueEnable()}");

if (command == "volume")
{
    var current = media.GetVolume();
    Console.WriteLine($"Volume={current} (range 0-200)");
    if (args.Length > 2 && int.TryParse(args[2], out var target))
    {
        media.SetVolume(target);
        Console.WriteLine($"Volume set to {media.GetVolume()} (requested {target}).");
    }

    return 0;
}

// A nearly muted robot (the volume range is 0-200) would make the playback tests meaningless, so raise
// it for the test only and put it back afterwards.
var volumeBefore = media.GetVolume();
var volumeChanged = false;

var gate = new object();
var statusEvents = new List<string>();
var systemErrors = new List<string>();
var audioFrames = 0;
var audioSamples = 0;
uint audioChannels = 0;
uint audioRate = 0;
uint audioFormat = 0;
var audioPeak = 0;
var captured = new List<short>();
var playbackFrames = 0;
var playbackSamples = 0;
uint playbackChannels = 0;
uint playbackRate = 0;
uint playbackFormat = 0;
var captureFrames = 0;
uint videoWidth = 0;
uint videoHeight = 0;
uint videoFormat = 0;
uint videoFps = 0;
var videoBytes = 0;
var desensedFrames = 0;
uint desensedWidth = 0;
uint desensedHeight = 0;
uint desensedFormat = 0;
uint desensedFps = 0;
var desensedBytes = 0;
byte[]? firstFrame = null;
byte[]? firstDesensedFrame = null;

media.SubscribeSystemStatus((SystemStatus status) =>
{
    lock (gate)
    {
        statusEvents.Add($"{status.Value} reason={status.Reason} sn='{status.Header.Sn}'");
    }
});

media.SubscribeSystemError((SystemError error) =>
{
    lock (gate)
    {
        systemErrors.Add($"code={error.Code} message='{error.Message}'");
    }
});

media.SubscribeInternalAudioCapture((in AudioStream stream) =>
{
    lock (gate)
    {
        audioFrames++;
        audioSamples += stream.AudioData.Length;
        audioChannels = stream.Channels;
        audioRate = stream.SampleRate;
        audioFormat = stream.Format;
        foreach (var sample in stream.AudioData)
        {
            if (Math.Abs((int)sample) > audioPeak)
            {
                audioPeak = Math.Abs((int)sample);
            }

            captured.Add(sample);
        }
    }
});

media.SubscribeInternalAudioPlayback((in AudioStream stream) =>
{
    lock (gate)
    {
        playbackFrames++;
        playbackSamples += stream.AudioData.Length;
        playbackChannels = stream.Channels;
        playbackRate = stream.SampleRate;
        playbackFormat = stream.Format;
    }
});

media.SubscribeInternalVideoCapture((in VideoStream stream) =>
{
    lock (gate)
    {
        captureFrames++;
        videoWidth = stream.Width;
        videoHeight = stream.Height;
        videoFormat = stream.Format;
        videoFps = stream.Fps;
        videoBytes += stream.VideoData.Length;
        firstFrame ??= stream.VideoData.ToArray();
    }
});

media.SubscribeInternalVideoDesensed((in VideoStream stream) =>
{
    lock (gate)
    {
        desensedFrames++;
        desensedWidth = stream.Width;
        desensedHeight = stream.Height;
        desensedFormat = stream.Format;
        desensedFps = stream.Fps;
        desensedBytes += stream.VideoData.Length;
        firstDesensedFrame ??= stream.VideoData.ToArray();
    }
});

Console.WriteLine(command switch
{
    "status" => $"Subscribing to system status/errors for {seconds}s (no audio or video).",
    "record" => $"Recording {seconds}s of internal microphone audio into '{outDir}'.",
    "play" => "Publishing a 1s 440 Hz tone to the robot speaker.",
    "playfile" => $"Playing '{inputFile}' on the robot speaker.",
    "playstdin" => "Playing raw 16-bit PCM from stdin on the robot speaker.",
    "video" => $"Subscribing to camera frames for {seconds}s.",
    _ => $"Collecting status, microphone audio and camera frames for {seconds}s, then playing a tone.",
});

if (command == "loopback")
{
    lock (gate)
    {
        captured.Clear();
        audioFrames = 0;
        audioPeak = 0;
    }

    Console.WriteLine($"Recording {seconds}s from the microphone array, then playing it back on the speaker.");
    await Task.Delay(TimeSpan.FromSeconds(seconds));

    int frames;
    short[] stereo;
    int capturedChannels;
    int capturedRate;
    int peak;
    lock (gate)
    {
        capturedChannels = (int)audioChannels;
        capturedRate = (int)(audioRate == 0 ? 16000 : audioRate);
        peak = audioPeak;
        stereo = DownmixToStereo(captured, capturedChannels, out frames);
    }

    Console.WriteLine($"Captured {frames} frames ({frames / (double)capturedRate:F1}s), peak={peak}.");
    if (frames == 0)
    {
        Console.Error.WriteLine("Nothing was captured.");
        return 2;
    }

    if (volumeBefore < 100)
    {
        media.SetVolume(100);
        volumeChanged = true;
        Console.WriteLine($"Volume {volumeBefore} -> 100 for playback (restored afterwards).");
    }

    var published = PublishPcm(media, stereo, capturedRate, 2, 2);
    Console.WriteLine($"Playing it back: published {published} frames.");

    var loopStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    var loopPath = Path.Combine(outDir, $"loopback-{loopStamp}.wav");
    WriteWav(loopPath, new List<short>(stereo), 2, capturedRate);
    Console.WriteLine($"Saved the downmix to {loopPath}");

    await Task.Delay(TimeSpan.FromSeconds(2));
    if (volumeChanged)
    {
        media.SetVolume(volumeBefore);
        for (var i = 0; i < 20 && media.GetVolume() != volumeBefore; i++)
        {
            await Task.Delay(100);
        }

        Console.WriteLine($"Volume set back to {volumeBefore}.");
    }

    return 0;
}

if (command == "playstdin")
{
    // Decode on the board and stream it in:
    //   mpg123 -q -s -r 16000 -m file.mp3 | MediaTest <dds.xml> playstdin 16000 1
    var stdinRate = args.Length > 2 && int.TryParse(args[2], out var parsedRate) ? parsedRate : 16000;
    var stdinChannels = args.Length > 3 && int.TryParse(args[3], out var parsedChannels) ? parsedChannels : 2;
    Console.WriteLine($"Reading raw 16-bit PCM from stdin: {stdinRate} Hz, {stdinChannels} ch.");

    if (volumeBefore < 100)
    {
        media.SetVolume(100);
        volumeChanged = true;
        Console.WriteLine($"Volume {volumeBefore} -> 100 for the test (restored afterwards).");
    }

    var stdinFrameMs = args.Length > 4 && int.TryParse(args[4], out var parsedFrameMs) ? Math.Clamp(parsedFrameMs, 10, 500) : 20;
    Console.WriteLine($"Frame size: {stdinFrameMs} ms.");
    var frames = PublishStream(media, Console.OpenStandardInput(), stdinRate, stdinChannels, 2, stdinFrameMs);
    Console.WriteLine($"Published {frames} audio frames.");
    seconds = 1;
}

if (command == "playfile")
{
    var (pcm, rate, channels) = ReadWav(inputFile!);
    var lengthSeconds = pcm.Length / (double)channels / rate;
    Console.WriteLine($"Loaded {pcm.Length} samples: {rate} Hz, {channels} ch, {lengthSeconds:F1}s.");

    // The official playback format is 2 channels at 16 kHz (16-bit LE), interleaved; upmix mono.
    if (channels == 1)
    {
        var stereo = new short[pcm.Length * 2];
        for (var i = 0; i < pcm.Length; i++)
        {
            stereo[i * 2] = pcm[i];
            stereo[(i * 2) + 1] = pcm[i];
        }

        pcm = stereo;
        channels = 2;
        Console.WriteLine("Upmixed mono -> stereo; the playback topic expects 2 channels.");
    }

    if (volumeBefore < 100)
    {
        media.SetVolume(100);
        volumeChanged = true;
        Console.WriteLine($"Volume {volumeBefore} -> 100 for the test (restored afterwards).");
    }

    var frameMs = args.Length > 4 && int.TryParse(args[4], out var parsedFrameMs) ? Math.Clamp(parsedFrameMs, 10, 500) : 20;
    Console.WriteLine($"Frame size: {frameMs} ms.");
    var frames = PublishPcm(media, pcm, rate, channels, 2, frameMs);
    Console.WriteLine($"Published {frames} audio frames ({lengthSeconds:F1}s) to the playback topic.");
    seconds = 2;
}

if (command == "play")
{
    if (volumeBefore < 100)
    {
        media.SetVolume(100);
        volumeChanged = true;
        Console.WriteLine($"Volume {volumeBefore} -> 100 for the test (restored afterwards).");
    }

    var played = PlayTone(media, 1.0, 440, 16000, 2, 2);
    Console.WriteLine($"Published {played} audio frames to the playback topic; listening for {seconds}s.");
}

await Task.Delay(TimeSpan.FromSeconds(seconds));

if (command == "all")
{
    if (volumeBefore < 100)
    {
        media.SetVolume(100);
        volumeChanged = true;
        Console.WriteLine($"Volume {volumeBefore} -> 100 for the test (restored afterwards).");
    }

    var played = PlayTone(media, 1.0, 440, 16000, 2, 2);
    Console.WriteLine($"Published {played} audio frames to the playback topic.");
    await Task.Delay(TimeSpan.FromSeconds(3));
}

if (volumeChanged)
{
    // SetVolume is fire-and-forget over DDS; poll so the value really lands before we exit.
    media.SetVolume(volumeBefore);
    var restored = false;
    for (var i = 0; i < 20; i++)
    {
        if (media.GetVolume() == volumeBefore)
        {
            restored = true;
            break;
        }

        await Task.Delay(100);
    }

    Console.WriteLine(restored
        ? $"Volume restored to {volumeBefore}."
        : $"Volume restore requested ({volumeBefore}); read-back still {media.GetVolume()}.");
}

media.SubscribeInternalAudioCapture(null);
media.SubscribeInternalAudioPlayback(null);
media.SubscribeInternalVideoCapture(null);
media.SubscribeInternalVideoDesensed(null);

lock (gate)
{
    Console.WriteLine($"System status events: {statusEvents.Count}");
    foreach (var e in statusEvents.Take(5))
    {
        Console.WriteLine($"  {e}");
    }

    Console.WriteLine($"System errors: {systemErrors.Count}");
    foreach (var e in systemErrors.Take(5))
    {
        Console.WriteLine($"  {e}");
    }

    Console.WriteLine(
        $"Internal audio capture: frames={audioFrames} samples={audioSamples} " +
        $"channels={audioChannels} rate={audioRate} format={audioFormat} peak={audioPeak}");
    Console.WriteLine(
        $"Internal audio playback: frames={playbackFrames} samples={playbackSamples} " +
        $"channels={playbackChannels} rate={playbackRate} format={playbackFormat}");
    Console.WriteLine(
        $"Internal video capture: frames={captureFrames} {videoWidth}x{videoHeight} " +
        $"format={videoFormat} fps={videoFps} bytes={videoBytes}");
    Console.WriteLine(
        $"Internal video de-sensed: frames={desensedFrames} {desensedWidth}x{desensedHeight} " +
        $"format={desensedFormat} fps={desensedFps} bytes={desensedBytes}");
}

var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
if (captured.Count > 0)
{
    var wav = Path.Combine(outDir, $"record-{stamp}.wav");
    WriteWav(wav, captured, (int)(audioChannels == 0 ? 1 : audioChannels), (int)(audioRate == 0 ? 16000 : audioRate));
    Console.WriteLine($"Saved {captured.Count} samples to {wav}");
    Console.WriteLine(audioPeak == 0
        ? "WARNING: the captured audio is pure silence (all zeros)."
        : "The captured audio contains signal (non-zero samples).");
}

if (firstFrame is { Length: > 0 })
{
    var path = Path.Combine(outDir, $"frame-capture-{stamp}.bin");
    File.WriteAllBytes(path, firstFrame);
    Console.WriteLine($"Saved first capture frame ({firstFrame.Length} bytes, {Magic(firstFrame)}) to {path}");
}

if (firstDesensedFrame is { Length: > 0 })
{
    var path = Path.Combine(outDir, $"frame-desensed-{stamp}.bin");
    File.WriteAllBytes(path, firstDesensedFrame);
    Console.WriteLine($"Saved first de-sensed frame ({firstDesensedFrame.Length} bytes, {Magic(firstDesensedFrame)}) to {path}");
}

var ok = statusEvents.Count > 0 || audioFrames > 0 || captureFrames > 0 || desensedFrames > 0 ||
         playbackFrames > 0 || playbackSamples > 0;
return ok ? 0 : 2;

// Sends a tone as 20 ms frames, the frame size the official audio example uses.
static int PlayTone(MediaController controller, double toneSeconds, double frequency, uint sampleRate, uint channels, uint format)
{
    const int FrameMs = 20;
    var samplesPerFrame = (int)(sampleRate * FrameMs / 1000);
    var totalFrames = (int)(toneSeconds * 1000 / FrameMs);
    var buffer = new short[samplesPerFrame * channels];
    for (var frame = 0; frame < totalFrames; frame++)
    {
        for (var i = 0; i < samplesPerFrame; i++)
        {
            var t = (frame * samplesPerFrame + i) / (double)sampleRate;
            var value = (short)(short.MaxValue * 0.2 * Math.Sin(2 * Math.PI * frequency * t));
            for (var c = 0; c < channels; c++)
            {
                buffer[(i * channels) + c] = value;
            }
        }

        PublishFrame(controller, buffer, (ulong)frame + 1, (ulong)frame * FrameMs * 1000, channels, sampleRate, format, FrameMs);
    }

    return totalFrames;
}

// Publishes 16-bit PCM in 20 ms frames, paced against a wall clock so it plays in real time.
static int PublishPcm(MediaController controller, short[] pcm, int sampleRate, int channels, uint format, int frameMs = 20)
{
    var frameMsValue = frameMs;
    var samplesPerFrame = sampleRate * frameMsValue / 1000 * channels;
    var clock = Stopwatch.StartNew();
    var frames = 0;
    for (var offset = 0; offset < pcm.Length; offset += samplesPerFrame)
    {
        var count = Math.Min(samplesPerFrame, pcm.Length - offset);
        var buffer = new short[count];
        Array.Copy(pcm, offset, buffer, 0, count);
        PublishFrame(
            controller, buffer, (ulong)frames + 1, ((ulong)frames * (ulong)frameMsValue * 1000),
            (uint)channels, (uint)sampleRate, format, (uint)frameMsValue);
        frames++;

        if (frames % 250 == 0)
        {
            Console.WriteLine($"  ... {(frames * frameMsValue / 1000.0):F0}s published");
        }

        var target = TimeSpan.FromMilliseconds(frames * (double)frameMsValue);
        var wait = target - clock.Elapsed;
        if (wait > TimeSpan.Zero)
        {
            Thread.Sleep(wait);
        }
    }

    return frames;
}

// Averages the four head microphones (channels 0-3) into one signal and duplicates it to stereo,
// skipping the reserved channels and the speaker echo so the playback does not feed back.
static short[] DownmixToStereo(List<short> interleaved, int channels, out int frames)
{
    frames = channels == 0 ? 0 : interleaved.Count / channels;
    var stereo = new short[frames * 2];
    var used = Math.Clamp(channels, 1, 4);
    for (var i = 0; i < frames; i++)
    {
        var sum = 0;
        for (var c = 0; c < used; c++)
        {
            sum += interleaved[(i * channels) + c];
        }

        var mixed = (short)(sum / used);
        stereo[i * 2] = mixed;
        stereo[(i * 2) + 1] = mixed;
    }

    return stereo;
}

// Reads raw 16-bit PCM from a stream and publishes it in 20 ms frames, paced in real time.
static int PublishStream(MediaController controller, Stream input, int sampleRate, int channels, uint format, int frameMs = 20)
{
    var frameMsValue = frameMs;
    var bytesPerFrame = sampleRate / 1000 * frameMsValue * channels * 2;
    var buffer = new byte[bytesPerFrame];
    var clock = Stopwatch.StartNew();
    var frames = 0;
    while (true)
    {
        var read = 0;
        while (read < bytesPerFrame)
        {
            var n = input.Read(buffer, read, bytesPerFrame - read);
            if (n <= 0)
            {
                break;
            }

            read += n;
        }

        if (read == 0)
        {
            break;
        }

        var samples = new short[(read + 1) / 2];
        Buffer.BlockCopy(buffer, 0, samples, 0, read - (read % 2));
        PublishFrame(
            controller, samples, (ulong)frames + 1, ((ulong)frames * (ulong)frameMsValue * 1000),
            (uint)channels, (uint)sampleRate, format, (uint)frameMsValue);
        frames++;

        if (frames % 250 == 0)
        {
            Console.WriteLine($"  ... {(frames * frameMsValue / 1000.0):F0}s published");
        }

        var target = TimeSpan.FromMilliseconds(frames * (double)frameMsValue);
        var wait = target - clock.Elapsed;
        if (wait > TimeSpan.Zero)
        {
            Thread.Sleep(wait);
        }

        if (read < bytesPerFrame)
        {
            break;
        }
    }

    return frames;
}

static void PublishFrame(
    MediaController controller, short[] buffer, ulong messageId, ulong timestampUs,
    uint channels, uint sampleRate, uint format, uint durationMs)
{
    var stream = new AudioStream
    {
        Header = new MediaHeader(messageId, timestampUs, string.Empty),
        TimestampUs = timestampUs,
        Channels = channels,
        SampleRate = sampleRate,
        Format = format,
        DurationMs = durationMs,
        AudioData = buffer,
    };
    controller.PublishExternalAudioPlaybackStream(in stream);
}

// Minimal RIFF/WAVE reader for 16-bit PCM, which is what the SDK stream uses.
static (short[] Samples, int Rate, int Channels) ReadWav(string path)
{
    using var reader = new BinaryReader(File.OpenRead(path));
    var riff = new string(reader.ReadChars(4));
    reader.ReadInt32();
    var wave = new string(reader.ReadChars(4));
    if (riff != "RIFF" || wave != "WAVE")
    {
        throw new InvalidOperationException($"{path} is not a RIFF/WAVE file.");
    }

    var rate = 0;
    var channels = 0;
    var bits = 0;
    while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
    {
        var id = new string(reader.ReadChars(4));
        var size = reader.ReadInt32();
        var padded = size + (size % 2);
        if (id == "fmt ")
        {
            reader.ReadInt16();
            channels = reader.ReadInt16();
            rate = reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt16();
            bits = reader.ReadInt16();
            reader.BaseStream.Seek(padded - 16, SeekOrigin.Current);
        }
        else if (id == "data")
        {
            if (bits != 16)
            {
                throw new InvalidOperationException($"{path} has {bits}-bit samples; convert it to 16-bit PCM first.");
            }

            var bytes = reader.ReadBytes(size);
            var samples = new short[bytes.Length / 2];
            Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
            return (samples, rate, channels);
        }
        else
        {
            reader.BaseStream.Seek(padded, SeekOrigin.Current);
        }
    }

    throw new InvalidOperationException($"{path} has no data chunk.");
}

static string Magic(byte[] data) => data.Length >= 2 && data[0] == 0xFF && data[1] == 0xD8
    ? "looks like JPEG"
    : data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50
        ? "looks like PNG"
        : $"first bytes {data[0]:X2} {data[1]:X2} {data[2]:X2} {data[3]:X2}";

// 16-bit PCM WAV, interleaved, as delivered by the SDK.
static void WriteWav(string path, List<short> samples, int channels, int sampleRate)
{
    using var writer = new BinaryWriter(File.Create(path));
    var dataBytes = samples.Count * 2;
    writer.Write("RIFF"u8);
    writer.Write(36 + dataBytes);
    writer.Write("WAVE"u8);
    writer.Write("fmt "u8);
    writer.Write(16);
    writer.Write((short)1);
    writer.Write((short)channels);
    writer.Write(sampleRate);
    writer.Write(sampleRate * channels * 2);
    writer.Write((short)(channels * 2));
    writer.Write((short)16);
    writer.Write("data"u8);
    writer.Write(dataBytes);
    foreach (var sample in samples)
    {
        writer.Write(sample);
    }
}
