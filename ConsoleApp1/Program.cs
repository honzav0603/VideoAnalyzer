using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace ConsoleApp1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.Clear();

            while (true)
            {
                string? videoPath = GetVideoPath();
                if (string.IsNullOrEmpty(videoPath))
                {
                    Console.WriteLine("Program ukončen.");
                    return;
                }

                if (!File.Exists(videoPath))
                {
                    Console.WriteLine("Video soubor nenalezen!");
                    Console.ReadLine();
                    Console.Clear();
                    continue;
                }

                var analyzer = new VideoAnalyzer(videoPath);
                if (!analyzer.AnalyzeVideo())
                {
                    Console.WriteLine("Chyba při analýze videa. Ujistěte se, že máte nainstalován FFmpeg.");
                    Console.ReadLine();
                    Console.Clear();
                    continue;
                }

                if (!DisplayMenu(analyzer))
                {
                    Console.Clear();
                    continue;
                }
            }
        }

        static string? GetVideoPath()
        {
            Console.Write("Zadejte cestu k video souboru (mkv, mp4, avi, atd.): ");
            string? path = null;

            while (true)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                if (keyInfo.Key == ConsoleKey.Escape)
                {
                    return null;
                }
                else if (keyInfo.Key == ConsoleKey.Enter)
                {
                    break;
                }
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (path?.Length > 0)
                    {
                        path = path[..^1];
                        Console.Write("\b \b");
                    }
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    path += keyInfo.KeyChar;
                    Console.Write(keyInfo.KeyChar);
                }
            }

            Console.WriteLine();
            return path?.Trim('"');
        }

        static bool DisplayMenu(VideoAnalyzer analyzer)
        {
            while (true)
            {

                analyzer.DisplayVideoInfo();

                Console.WriteLine("\n╔════════════════════════════════════════╗");
                Console.WriteLine("║            DOSTUPNÉ PŘÍKAZY            ║");
                Console.WriteLine("╠════════════════════════════════════════╣");
                Console.WriteLine("║  V - Extrahovat VIDEO stopu            ║");
                Console.WriteLine("║  A - Extrahovat AUDIO stopu            ║");
                Console.WriteLine("║  S - Extrahovat TITULKY                ║");
                Console.WriteLine("║  I - Zobrazit detailní INFORMACE       ║");
                Console.WriteLine("║  L - Načíst NOVÉ VIDEO                 ║");
                Console.WriteLine("║  Q - Ukončit program                   ║");
                Console.WriteLine("╚════════════════════════════════════════╝\n");

                Console.Write("Zadejte příkaz: ");
                ConsoleKeyInfo keyInfo = Console.ReadKey();
                string? command = keyInfo.KeyChar.ToString().ToUpper().Trim();
                Console.WriteLine();

                switch (command)
                {
                    case "V":
                        analyzer.ExtractVideoTrack();
                        break;
                    case "A":
                        analyzer.ExtractAudioTrack();
                        break;
                    case "S":
                        analyzer.ExtractSubtitles();
                        break;
                    case "I":
                        DisplayDetailedInfo(analyzer);
                        break;
                    case "L":
                        return false;
                    case "Q":
                        Console.WriteLine("\nNa shledanou!");
                        Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine("Neznámý příkaz. Stiskněte Enter...");
                        Console.ReadLine();
                        break;
                }
                Console.Clear();
            }
        }

        static void DisplayDetailedInfo(VideoAnalyzer analyzer)
        {
            Console.Clear();
            analyzer.DisplayDetailedInfo();
            Console.WriteLine("\nStiskněte Enter pro návrat do menu...");
            Console.ReadLine();
        }
    }

    class VideoAnalyzer
    {
        private readonly string videoPath;
        private VideoInfo? videoInfo;
        private string? ffmpegPath;
        private string? ffprobePath;

        private const string FFMPEG_PATH = @"C:\ProgramData\chocolatey\bin\ffmpeg.exe";
        private const string FFPROBE_PATH = @"C:\ProgramData\chocolatey\bin\ffprobe.exe";

        public VideoAnalyzer(string path)
        {
            videoPath = path;
            InitializeFFmpegPaths();
        }

        private void InitializeFFmpegPaths()
        {
            ffprobePath = File.Exists(FFPROBE_PATH) ? FFPROBE_PATH : "ffprobe";
            ffmpegPath = File.Exists(FFMPEG_PATH) ? FFMPEG_PATH : "ffmpeg";
        }

        public bool AnalyzeVideo()
        {
            if (string.IsNullOrEmpty(ffprobePath))
            {
                Console.WriteLine("FFprobe není dostupný. Nelze analyzovat video.");
                return false;
            }

            try
            {
                string ffprobeOutput = RunFFprobe("-v error -show_format -show_streams -of json");
                if (string.IsNullOrEmpty(ffprobeOutput))
                {
                    Console.WriteLine("FFprobe vrátil prázdný výstup.");
                    return false;
                }

                using (JsonDocument doc = JsonDocument.Parse(ffprobeOutput))
                {
                    videoInfo = ParseVideoInfo(doc.RootElement);
                    return videoInfo != null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při analýze videa: {ex.Message}");
                return false;
            }
        }

        public void DisplayVideoInfo()
        {
            if (videoInfo == null) return;

            long totalBitrate = CalculateTotalBitrate();

            // Kalkuluj bitrate pro video streamy, které ho nemají
            if (videoInfo.VideoStreams.Count > 0)
            {
                for (int i = 0; i < videoInfo.VideoStreams.Count; i++)
                {
                    if (videoInfo.VideoStreams[i].BitrateRaw == 0 && videoInfo.Duration != "00:00:00")
                    {
                        double durationSeconds = GetDurationInSeconds(videoInfo.Duration);
                        if (durationSeconds > 0)
                        {
                            long calculatedBitrate = (long)((videoInfo.FileSize * 8) / durationSeconds);
                            videoInfo.VideoStreams[i].BitrateRaw = calculatedBitrate;
                            videoInfo.VideoStreams[i].Bitrate = FormatBitrate(calculatedBitrate);
                        }
                    }
                }
            }

            long totalBitrateRecalc = CalculateTotalBitrate();
            string fileName = Path.GetFileName(videoPath);
            string fileSize = FormatBytes(videoInfo.FileSize);
            string totalBitrateStr = totalBitrateRecalc > 0 ? FormatBitrate(totalBitrateRecalc) : "Unknown";

            // Tabulka se 4 sloupci a barvami
            var table = new Table();
            table.Title = new TableTitle("[bold yellow]VIDEO ANALYZER - DETAILNÍ PŘEHLED[/]");
            table.Border = TableBorder.Rounded;
            table.AddColumn("[bold cyan]Vlastnost[/]");
            table.AddColumn("[bold cyan]Hodnota[/]");
            table.AddColumn("[bold cyan]Vlastnost[/]");
            table.AddColumn("[bold cyan]Hodnota[/]");

            // Základní informace
            table.AddRow("[bold yellow]ZÁKLADNÍ INFORMACE[/]", "", "", "");
            table.AddRow("Soubor", $"[green]{fileName}[/]", "Velikost", $"[green]{fileSize}[/]");
            table.AddRow("Délka", $"[green]{videoInfo.Duration}[/]", "Bitrate celkem", $"[green]{totalBitrateStr}[/]");
            table.AddRow(new Markup(""), new Markup(""), new Markup(""), new Markup(""));

            // VIDEO STOPY
            if (videoInfo.VideoStreams.Count > 0)
            {
                table.AddRow("[bold yellow]VIDEO STOPY[/]", "", "", "");
                for (int i = 0; i < videoInfo.VideoStreams.Count; i++)
                {
                    var stream = videoInfo.VideoStreams[i];
                    table.AddRow($"[bold cyan]Stream #{stream.Index}[/]", "", "", "");
                    table.AddRow("Kodek", $"[green]{stream.Codec}[/]", "Profil", $"[green]{stream.Profile}[/]");
                    table.AddRow("Level", $"[green]{stream.Level}[/]", "Bitová hloubka", $"[green]{(stream.ColorDepth > 0 ? $"{stream.ColorDepth} bitů" : "Unknown")}[/]");
                    table.AddRow("Rozlišení", $"[green]{stream.Width}x{stream.Height}[/]", "Poměr stran", $"[green]{stream.AspectRatio}[/]");
                    table.AddRow("FPS", $"[green]{stream.FrameRate}[/]", "Bitrate", $"[green]{stream.Bitrate}[/]");
                    table.AddRow("Pixel formát", $"[green]{stream.PixelFormat}[/]", "Barevný rozsah", $"[green]{stream.ColorRange}[/]");
                    table.AddRow("Barevné primárky", $"[green]{stream.ColorPrimaries}[/]", "Přenosová funkce", $"[green]{stream.ColorTransfer}[/]");
                    table.AddRow("HDR", $"[green]{(stream.IsHDR ? "ANO" : "NE")}[/]", "B-Frames", $"[green]{stream.HasBFrames}[/]");
                    if (i < videoInfo.VideoStreams.Count - 1)
                        table.AddRow(new Markup(""), new Markup(""), new Markup(""), new Markup(""));
                }
                table.AddRow(new Markup(""), new Markup(""), new Markup(""), new Markup(""));
            }

            // AUDIO STOPY
            if (videoInfo.AudioStreams.Count > 0)
            {
                table.AddRow("[bold yellow]AUDIO STOPY[/]", "", "", "");
                for (int i = 0; i < videoInfo.AudioStreams.Count; i++)
                {
                    var stream = videoInfo.AudioStreams[i];
                    table.AddRow($"[bold cyan]Stream #{stream.Index}[/]", $"[green]{stream.Language}[/]", "", "");
                    table.AddRow("Kodek", $"[green]{stream.Codec}[/]", "Kanály", $"[green]{stream.Channels}[/]");
                    table.AddRow("Rozmístění", $"[green]{stream.ChannelLayout}[/]", "Vzorkovací", $"[green]{stream.SampleRate} Hz[/]");
                    table.AddRow("Bitrate", $"[green]{stream.Bitrate}[/]", "", "");
                    if (i < videoInfo.AudioStreams.Count - 1)
                        table.AddRow(new Markup(""), new Markup(""), new Markup(""), new Markup(""));
                }
                table.AddRow(new Markup(""), new Markup(""), new Markup(""), new Markup(""));
            }

            // TITULKY
            if (videoInfo.SubtitleStreams.Count > 0)
            {
                table.AddRow("[bold yellow]TITULKY[/]", "", "", "");
                for (int i = 0; i < videoInfo.SubtitleStreams.Count; i++)
                {
                    var stream = videoInfo.SubtitleStreams[i];
                    table.AddRow($"[bold cyan]Stream #{stream.Index}[/]", $"[green]{stream.Language}[/]", "Kodek", $"[green]{stream.Codec}[/]");
                    if (i < videoInfo.SubtitleStreams.Count - 1)
                        table.AddRow(new Markup(""), new Markup(""), new Markup(""), new Markup(""));
                }
                table.AddRow(new Markup(""), new Markup(""), new Markup(""), new Markup(""));
            }

            AnsiConsole.Write(table);
            Console.WriteLine();
        }

        private double GetDurationInSeconds(string duration)
        {
            try
            {
                if (TimeSpan.TryParse(duration, out var ts))
                    return ts.TotalSeconds;
            }
            catch { }
            return 0;
        }

        private long CalculateTotalBitrate()
        {
            long total = 0;
            foreach (var video in videoInfo?.VideoStreams ?? new())
                total += video.BitrateRaw;
            foreach (var audio in videoInfo?.AudioStreams ?? new())
                total += audio.BitrateRaw;
            return total;
        }

        private string FormatBitrate(long bitrate)
        {
            if (bitrate >= 1000000)
                return $"{bitrate / 1000000.0:0.00} Mbps";
            else if (bitrate >= 1000)
                return $"{bitrate / 1000.0:0.00} kbps";
            else
                return $"{bitrate} bps";
        }

        public void DisplayDetailedInfo()
        {
            if (videoInfo == null) return;

            Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");
            DisplayVideoInfo();

            Console.WriteLine("\nVŠECHNY INFORMACE:\n");
            Console.WriteLine(JsonSerializer.Serialize(videoInfo, new JsonSerializerOptions { WriteIndented = true }));
        }

        public void ExtractVideoTrack()
        {
            if (videoInfo?.VideoStreams.Count == 0)
            {
                Console.WriteLine("Žádné video stopy!");
                Console.ReadLine();
                return;
            }

            Console.Clear();
            Console.WriteLine("EXTRAKCE VIDEO STOPY\n");

            for (int i = 0; i < videoInfo.VideoStreams.Count; i++)
            {
                Console.WriteLine($"{i}: Stream #{videoInfo.VideoStreams[i].Index} - " +
                    $"{videoInfo.VideoStreams[i].Width}x{videoInfo.VideoStreams[i].Height} ({videoInfo.VideoStreams[i].Codec})");
            }

            Console.Write("\nVyberte stopu (číslo): ");
            if (!int.TryParse(Console.ReadLine(), out int streamIndex) || streamIndex < 0 || streamIndex >= videoInfo.VideoStreams.Count)
            {
                Console.WriteLine("Neplatná volba!");
                Console.ReadLine();
                return;
            }

            int streamId = videoInfo.VideoStreams[streamIndex].Index;
            Console.Write("Zadejte název výstupního souboru (bez přípony): ");
            string? outputName = Console.ReadLine();

            if (string.IsNullOrEmpty(outputName))
            {
                Console.WriteLine("Neplatný název!");
                Console.ReadLine();
                return;
            }

            string outputPath = Path.Combine(Path.GetDirectoryName(videoPath) ?? ".", $"{outputName}.mp4");
            string ffmpegArgs = $"-i \"{videoPath}\" -map 0:{streamId} -c copy \"{outputPath}\"";

            Console.WriteLine($"\nExtrahování video stopy...");
            if (RunFFmpeg(ffmpegArgs))
            {
                Console.WriteLine($"Video stopa úspěšně extrahována do: {outputPath}");
            }
            else
            {
                Console.WriteLine($"Chyba při extrakci!");
            }

            Console.ReadLine();
        }

        public void ExtractAudioTrack()
        {
            if (videoInfo?.AudioStreams.Count == 0)
            {
                Console.WriteLine("Žádné audio stopy!");
                Console.ReadLine();
                return;
            }

            Console.Clear();
            Console.WriteLine("EXTRAKCE AUDIO STOPY\n");

            for (int i = 0; i < videoInfo.AudioStreams.Count; i++)
            {
                Console.WriteLine($"{i}: Stream #{videoInfo.AudioStreams[i].Index} - " +
                    $"{videoInfo.AudioStreams[i].Language} ({videoInfo.AudioStreams[i].Codec}, {videoInfo.AudioStreams[i].Channels}ch)");
            }

            Console.Write("\nVyberte stopu (číslo): ");
            if (!int.TryParse(Console.ReadLine(), out int streamIndex) || streamIndex < 0 || streamIndex >= videoInfo.AudioStreams.Count)
            {
                Console.WriteLine("Neplatná volba!");
                Console.ReadLine();
                return;
            }

            int streamId = videoInfo.AudioStreams[streamIndex].Index;
            Console.Write("Zadejte název výstupního souboru (bez přípony): ");
            string? outputName = Console.ReadLine();

            if (string.IsNullOrEmpty(outputName))
            {
                Console.WriteLine("Neplatný název!");
                Console.ReadLine();
                return;
            }

            string outputPath = Path.Combine(Path.GetDirectoryName(videoPath) ?? ".", $"{outputName}.mka");
            string ffmpegArgs = $"-i \"{videoPath}\" -map 0:{streamId} -c:a copy \"{outputPath}\"";

            Console.WriteLine($"\nExtrahování audio stopy (původní formát)...");
            if (RunFFmpeg(ffmpegArgs))
            {
                Console.WriteLine($"Audio stopa úspěšně extrahována do: {outputPath}");
            }
            else
            {
                Console.WriteLine($"Chyba při extrakci!");
            }

            Console.ReadLine();
        }

        public void ExtractSubtitles()
        {
            if (videoInfo == null || videoInfo.SubtitleStreams.Count == 0)
            {
                Console.WriteLine("Žádné titulky!");
                Console.ReadLine();
                return;
            }

            Console.Clear();
            Console.WriteLine("EXTRAKCE TITULKŮ\n");

            for (int i = 0; i < videoInfo.SubtitleStreams.Count; i++)
            {
                Console.WriteLine($"{i}: Stream #{videoInfo.SubtitleStreams[i].Index} - " +
                    $"{videoInfo.SubtitleStreams[i].Language} ({videoInfo.SubtitleStreams[i].Codec})");
            }

            Console.Write("\nVyberte stopu (číslo): ");
            if (!int.TryParse(Console.ReadLine(), out int streamIndex) || streamIndex < 0 || streamIndex >= videoInfo.SubtitleStreams.Count)
            {
                Console.WriteLine("Neplatná volba!");
                Console.ReadLine();
                return;
            }

            int streamId = videoInfo.SubtitleStreams[streamIndex].Index;
            Console.Write("Zadejte název výstupního souboru (bez přípony): ");
            string? outputName = Console.ReadLine();

            if (string.IsNullOrEmpty(outputName))
            {
                Console.WriteLine("Neplatný název!");
                Console.ReadLine();
                return;
            }

            string outputPath = Path.Combine(Path.GetDirectoryName(videoPath) ?? ".", $"{outputName}.srt");
            string ffmpegArgs = $"-i \"{videoPath}\" -map 0:{streamId} \"{outputPath}\"";

            Console.WriteLine($"\nExtrahování titulků...");
            if (RunFFmpeg(ffmpegArgs))
            {
                Console.WriteLine($"Titulky úspěšně extrahovány do: {outputPath}");
            }
            else
            {
                Console.WriteLine($"Chyba při extrakci!");
            }

            Console.ReadLine();
        }

        private VideoInfo ParseVideoInfo(JsonElement root)
        {
            var info = new VideoInfo();

            if (root.TryGetProperty("format", out var format))
            {
                if (format.TryGetProperty("duration", out var duration))
                {
                    double seconds = GetDoubleValue(duration);
                    info.Duration = TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
                }

                if (format.TryGetProperty("size", out var size))
                    info.FileSize = GetLongValue(size);
            }

            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    if (stream.TryGetProperty("codec_type", out var codecType))
                    {
                        string codecTypeStr = codecType.GetString() ?? "";

                        if (stream.TryGetProperty("disposition", out var disposition) && 
                            disposition.TryGetProperty("attached_pic", out var attachedPic) &&
                            attachedPic.GetInt32() == 1)
                        {
                            continue;
                        }

                        switch (codecTypeStr)
                        {
                            case "video":
                                info.VideoStreams.Add(ParseVideoStream(stream));
                                break;
                            case "audio":
                                info.AudioStreams.Add(ParseAudioStream(stream));
                                break;
                            case "subtitle":
                                info.SubtitleStreams.Add(ParseSubtitleStream(stream));
                                break;
                        }
                    }
                }
            }

            return info;
        }

        private string GetStringValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? "Unknown",
                JsonValueKind.Number => element.GetInt32().ToString(),
                _ => "Unknown"
            };
        }

        private int GetIntValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => int.TryParse(element.GetString(), out var i) ? i : 0,
                JsonValueKind.Number => element.GetInt32(),
                _ => 0
            };
        }

        private long GetLongValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => long.TryParse(element.GetString(), out var l) ? l : 0,
                JsonValueKind.Number => element.GetInt64(),
                _ => 0
            };
        }

        private double GetDoubleValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => double.TryParse(element.GetString(), out var d) ? d : 0,
                JsonValueKind.Number => element.GetDouble(),
                _ => 0
            };
        }

         private VideoStream ParseVideoStream(JsonElement stream)
        {
            var vs = new VideoStream();

            if (stream.TryGetProperty("index", out var idx))
                vs.Index = GetIntValue(idx);

            if (stream.TryGetProperty("codec_name", out var codec))
                vs.Codec = codec.GetString() ?? "Unknown";

            if (stream.TryGetProperty("width", out var width))
                vs.Width = GetIntValue(width);

            if (stream.TryGetProperty("height", out var height))
                vs.Height = GetIntValue(height);

            if (stream.TryGetProperty("r_frame_rate", out var fps))
                vs.FrameRate = fps.GetString() ?? "Unknown";

            if (stream.TryGetProperty("bit_rate", out var bitrate))
            {
                long bitrateValue = GetLongValue(bitrate);
                vs.BitrateRaw = bitrateValue;
                if (bitrateValue > 0)
                    vs.Bitrate = FormatBitrate(bitrateValue);
                else
                    vs.Bitrate = "Unknown";
            }
            else
            {
                vs.Bitrate = "Unknown";
            }

            if (stream.TryGetProperty("color_space", out var color))
                vs.ColorSpace = color.GetString() ?? "Unknown";

            if (stream.TryGetProperty("color_transfer", out var transfer))
            {
                string? transferStr = transfer.GetString() ?? "";
                vs.ColorTransfer = transferStr;
                vs.IsHDR = transferStr.Contains("smpte2084") || transferStr.Contains("arib-std-b67");
            }

            if (stream.TryGetProperty("color_primaries", out var primaries))
                vs.ColorPrimaries = primaries.GetString() ?? "Unknown";

            if (stream.TryGetProperty("profile", out var profile))
                vs.Profile = GetStringValue(profile);

            if (stream.TryGetProperty("level", out var level))
                vs.Level = GetStringValue(level);

            if (stream.TryGetProperty("bits_per_raw_sample", out var bpp))
            {
                int bppValue = GetIntValue(bpp);
                if (bppValue > 0)
                    vs.ColorDepth = bppValue;
            }

            if (stream.TryGetProperty("display_aspect_ratio", out var dar))
                vs.AspectRatio = dar.GetString() ?? "Unknown";
            else if (vs.Width > 0 && vs.Height > 0)
                vs.AspectRatio = $"{vs.Width}:{vs.Height}";

            if (stream.TryGetProperty("pix_fmt", out var pixfmt))
                vs.PixelFormat = pixfmt.GetString() ?? "Unknown";

            if (vs.ColorDepth == 0 && !string.IsNullOrEmpty(vs.PixelFormat))
            {
                var match = Regex.Match(vs.PixelFormat, @"p(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int bits))
                    vs.ColorDepth = bits;
            }

            if (stream.TryGetProperty("color_range", out var colorrange))
                vs.ColorRange = colorrange.GetString() ?? "Unknown";

            if (stream.TryGetProperty("has_b_frames", out var bframes))
                vs.HasBFrames = GetIntValue(bframes);

            return vs;
        }

        private AudioStream ParseAudioStream(JsonElement stream)
        {
            var audio = new AudioStream();

            if (stream.TryGetProperty("index", out var idx))
                audio.Index = GetIntValue(idx);

            if (stream.TryGetProperty("codec_name", out var codec))
                audio.Codec = codec.GetString() ?? "Unknown";

            if (stream.TryGetProperty("tags", out var tags) && tags.TryGetProperty("language", out var lang))
                audio.Language = lang.GetString() ?? "Unknown";

            if (stream.TryGetProperty("channels", out var channels))
                audio.Channels = GetIntValue(channels);

            if (stream.TryGetProperty("channel_layout", out var layout))
                audio.ChannelLayout = layout.GetString() ?? "Unknown";

            if (stream.TryGetProperty("sample_rate", out var sample))
                audio.SampleRate = GetIntValue(sample);

            if (stream.TryGetProperty("bit_rate", out var bitrate))
            {
                long bitrateValue = GetLongValue(bitrate);
                audio.BitrateRaw = bitrateValue;
                if (bitrateValue > 0)
                    audio.Bitrate = FormatBitrate(bitrateValue);
                else
                    audio.Bitrate = "Unknown";
            }

            return audio;
        }

        private SubtitleStream ParseSubtitleStream(JsonElement stream)
        {
            var sub = new SubtitleStream();

            if (stream.TryGetProperty("index", out var idx))
                sub.Index = GetIntValue(idx);

            if (stream.TryGetProperty("codec_name", out var codec))
                sub.Codec = codec.GetString() ?? "Unknown";

            if (stream.TryGetProperty("tags", out var tags) && tags.TryGetProperty("language", out var lang))
                sub.Language = lang.GetString() ?? "Unknown";

            return sub;
        }

        private string RunFFprobe(string args)
        {
            if (string.IsNullOrEmpty(ffprobePath))
            {
                Console.WriteLine("FFprobe není dostupný!");
                return "";
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = $"{args} \"{videoPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error) && process.ExitCode != 0)
                {
                    Console.WriteLine($"Chyba FFprobe: {error}");
                    return "";
                }

                return output;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při spuštění FFprobe: {ex.Message}");
                return "";
            }
        }

         private bool RunFFmpeg(string args)
         {
             if (string.IsNullOrEmpty(ffmpegPath))
             {
                 Console.WriteLine("FFmpeg není dostupný!");
                 return false;
             }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();

                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        string? line;
                        while ((line = process.StandardError.ReadLine()) != null)
                        {
                            if (line.Contains("frame=") || line.Contains("time="))
                            {
                                string progress = ExtractFFmpegProgress(line);
                                if (!string.IsNullOrEmpty(progress))
                                {
                                    Console.Write($"\r{progress}");
                                }
                            }
                        }
                    }
                    catch { }
                });

                process.WaitForExit();
                Console.WriteLine();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při spuštění FFmpeg: {ex.Message}");
                return false;
            }
        }

        private string ExtractFFmpegProgress(string ffmpegOutput)
        {
            string progress = "";

            if (ffmpegOutput.Contains("time="))
            {
                var match = System.Text.RegularExpressions.Regex.Match(ffmpegOutput, @"time=([\d:]+)");
                if (match.Success)
                    progress += $"Čas: {match.Groups[1].Value} ";
            }

            if (ffmpegOutput.Contains("fps="))
            {
                var match = System.Text.RegularExpressions.Regex.Match(ffmpegOutput, @"fps=\s*(\d+)");
                if (match.Success)
                    progress += $"| FPS: {match.Groups[1].Value} ";
            }

            if (ffmpegOutput.Contains("speed="))
            {
                var match = System.Text.RegularExpressions.Regex.Match(ffmpegOutput, @"speed=([\d.]+)x");
                if (match.Success)
                    progress += $"| Rychlost: {match.Groups[1].Value}x";
            }

            return progress;
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.00} {sizes[order]}";
        }
    }

    class VideoInfo
    {
        public string Duration { get; set; } = "00:00:00";
        public long FileSize { get; set; }
        public List<VideoStream> VideoStreams { get; set; } = new();
        public List<AudioStream> AudioStreams { get; set; } = new();
        public List<SubtitleStream> SubtitleStreams { get; set; } = new();
    }

    class VideoStream
    {
        public int Index { get; set; }
        public string Codec { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public string FrameRate { get; set; } = "";
        public string Bitrate { get; set; } = "";
        public long BitrateRaw { get; set; }
        public bool IsHDR { get; set; }
        public int ColorDepth { get; set; }
        public string ColorSpace { get; set; } = "";
        public string ColorTransfer { get; set; } = "";
        public string ColorPrimaries { get; set; } = "";
        public string Profile { get; set; } = "";
        public string Level { get; set; } = "";
        public string AspectRatio { get; set; } = "";
        public string PixelFormat { get; set; } = "";
        public string ColorRange { get; set; } = "";
        public int HasBFrames { get; set; }
    }

    class AudioStream
    {
        public int Index { get; set; }
        public string Codec { get; set; } = "";
        public string Language { get; set; } = "Unknown";
        public int Channels { get; set; }
        public int SampleRate { get; set; }
        public string Bitrate { get; set; } = "";
        public long BitrateRaw { get; set; }
        public string ChannelLayout { get; set; } = "";
    }

    class SubtitleStream
    {
        public int Index { get; set; }
        public string Codec { get; set; } = "";
        public string Language { get; set; } = "Unknown";
    }
}
