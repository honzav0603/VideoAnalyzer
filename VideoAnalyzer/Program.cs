using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace VideoAnalyzer
{
    // Hlavní třída programu - řídí uživatelské rozhraní a tok aplikace
    internal class Program
    {
        // Hlavní vstupní bod aplikace
        static void Main(string[] args)
        {
            Console.Clear();

            // Hlavní smyčka - opakuje se, dokud uživatel neukončí program
            while (true)
            {
                // Získá cestu k video souboru od uživatele
                string? videoPath = GetVideoPath();
                if (string.IsNullOrEmpty(videoPath))
                {
                    Console.WriteLine("Program ukončen.");
                    return;
                }

                // Ověří, že soubor existuje
                if (!File.Exists(videoPath))
                {
                    Console.WriteLine("Video soubor nenalezen!");
                    Console.ReadLine();
                    Console.Clear();
                    continue;
                }

                // Vytvoří analyzátor videa a provede analýzu
                var analyzer = new VideoAnalyzer(videoPath);
                if (!analyzer.AnalyzeVideo())
                {
                    Console.WriteLine("Chyba při analýze videa. Ujistěte se, že máte nainstalován FFmpeg.");
                    Console.ReadLine();
                    Console.Clear();
                    continue;
                }

                // Zobrazí menu s dostupnými příkazy
                // Pokud uživatel zvolí "Načíst nové video", vrátí false a smyčka pokračuje
                if (!DisplayMenu(analyzer))
                {
                    Console.Clear();
                    continue;
                }
            }
        }

        // Bezpečně načítá cestu k videu od uživatele znak po znaku
        // Umožňuje uživateli použít klávesy Escape (zrušit), Backspace (smazat) a Enter (potvrdit)
        static string? GetVideoPath()
        {
            Console.Write("Zadejte cestu k video souboru (mkv, mp4, avi, atd.): ");
            string? path = null;

            while (true)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                // Escape - zrušit zadávání
                if (keyInfo.Key == ConsoleKey.Escape)
                {
                    return null;
                }
                // Enter - potvrdit cestu
                else if (keyInfo.Key == ConsoleKey.Enter)
                {
                    break;
                }
                // Backspace - smazat poslední znak
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (path?.Length > 0)
                    {
                        path = path[..^1];
                        Console.Write("\b \b");
                    }
                }
                // Přidej běžný znak k cestě
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    path += keyInfo.KeyChar;
                    Console.Write(keyInfo.KeyChar);
                }
            }

            Console.WriteLine();
            // Odstraní uvozovky ze začátku a konce cesty (v případě, že je uživatel zadá)
            return path?.Trim('"');
        }

        // Zobrazuje interaktivní menu s dostupnými příkazy
        // Vrátí false, pokud uživatel zvolí načtení nového videa
        // Vrátí true pro všechny ostatní volby (zpracovává je interně)
        static bool DisplayMenu(VideoAnalyzer analyzer)
        {
            while (true)
            {
                // Zobrazí informace o právě načteném videu
                analyzer.DisplayVideoInfo();

                // Zobrazí menu příkazů
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

                // Zpracuje příkaz
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
                        // Signalizuje, že se má načíst nové video
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

        // Zobrazí detailní informace o videu na celou obrazovku
        static void DisplayDetailedInfo(VideoAnalyzer analyzer)
        {
            Console.Clear();
            analyzer.DisplayDetailedInfo();
            Console.WriteLine("\nStiskněte Enter pro návrat do menu...");
            Console.ReadLine();
        }
    }

    // Hlavní třída pro analýzu videa - pracuje s FFmpeg a FFprobe
    // Hlavní třída pro analýzu videa - pracuje s FFmpeg a FFprobe
    class VideoAnalyzer
    {
        // Cesta k video souboru
        private readonly string videoPath;
        // Informace o videu (získané z FFprobe)
        private VideoInfo? videoInfo;
        // Cesty k FFmpeg a FFprobe nástrojům
        private string? ffmpegPath;
        private string? ffprobePath;

        // Výchozí cesty k nástrojům (nainstalované přes Chocolatey)
        private const string FFMPEG_PATH = @"C:\ProgramData\chocolatey\bin\ffmpeg.exe";
        private const string FFPROBE_PATH = @"C:\ProgramData\chocolatey\bin\ffprobe.exe";

        // Konstruktor - inicializuje cestu k videu a hledá FFmpeg/FFprobe
        public VideoAnalyzer(string path)
        {
            videoPath = path;
            InitializeFFmpegPaths();
        }

        // Najde správné cesty k FFmpeg a FFprobe (nejdřív zkusí Chocolatey cestu, pak PATH)
        private void InitializeFFmpegPaths()
        {
            ffprobePath = File.Exists(FFPROBE_PATH) ? FFPROBE_PATH : "ffprobe";
            ffmpegPath = File.Exists(FFMPEG_PATH) ? FFMPEG_PATH : "ffmpeg";
        }

        // Analyzuje video soubor pomocí FFprobe a parsuje JSON výstup
        // Vrátí true, pokud byla analýza úspěšná
        public bool AnalyzeVideo()
        {
            if (string.IsNullOrEmpty(ffprobePath))
            {
                Console.WriteLine("FFprobe není dostupný. Nelze analyzovat video.");
                return false;
            }

            try
            {
                // Spustí FFprobe s JSON výstupem
                string ffprobeOutput = RunFFprobe("-v error -show_format -show_streams -of json");
                if (string.IsNullOrEmpty(ffprobeOutput))
                {
                    Console.WriteLine("FFprobe vrátil prázdný výstup.");
                    return false;
                }

                // Parsuje JSON a ukládá informace o videu
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

        // [GENEROVÁNO AI - Claude Opus 4.7] Zobrazuje přehled videa v elegantní tabulce s barvami
        // Kalkuluje bitrate pro streamy, které ho nemají definovaný
        // Tabulka je formátována pomocí Spectre.Console
        public void DisplayVideoInfo()
        {
            if (videoInfo == null) return;

            long totalBitrate = CalculateTotalBitrate();

            // Kalkuluj bitrate pro video streamy, které ho nemají
            // Pokud stream nemá bitrate, vypočítá ho z délky videa a velikosti souboru
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

            // [GENEROVÁNO AI - Claude Opus 4.7] Vytváří a formátuje tabulku se čtyřmi sloupci
            // Tabulka obsahuje: Základní informace, Video stopy, Audio stopy, Titulky
            var table = new Table();
            table.Title = new TableTitle("[bold yellow]VIDEO ANALYZER - DETAILNÍ PŘEHLED[/]");
            table.Border = TableBorder.Rounded;
            table.AddColumn("[bold cyan]Vlastnost[/]");
            table.AddColumn("[bold cyan]Hodnota[/]");
            table.AddColumn("[bold cyan]Vlastnost[/]");
            table.AddColumn("[bold cyan]Hodnota[/]");

            // Základní informace o videu
            table.AddRow("[bold yellow]ZÁKLADNÍ INFORMACE[/]", "", "", "");
            table.AddRow("Soubor", $"[green]{fileName}[/]", "Velikost", $"[green]{fileSize}[/]");
            table.AddRow("Délka", $"[green]{videoInfo.Duration}[/]", "Bitrate celkem", $"[green]{totalBitrateStr}[/]");
            table.AddRow(new Markup(""), new Markup(""), new Markup(""), new Markup(""));

            // Informace o VIDEO stopách
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

            // Informace o AUDIO stopách
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

            // Informace o TITULEK
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

            // Vykreslí tabulku do konzole
            AnsiConsole.Write(table);
            Console.WriteLine();
        }

        // Konvertuje řetězec s časem (HH:MM:SS) na počet sekund
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

        // Sčítá bitrate všech video a audio streamů
        private long CalculateTotalBitrate()
        {
            long total = 0;
            foreach (var video in videoInfo?.VideoStreams ?? new())
                total += video.BitrateRaw;
            foreach (var audio in videoInfo?.AudioStreams ?? new())
                total += audio.BitrateRaw;
            return total;
        }

        // Formátuje bitrate do čitelné formy (bps, kbps, Mbps)
        private string FormatBitrate(long bitrate)
        {
            if (bitrate >= 1000000)
                return $"{bitrate / 1000000.0:0.00} Mbps";
            else if (bitrate >= 1000)
                return $"{bitrate / 1000.0:0.00} kbps";
            else
                return $"{bitrate} bps";
        }

        // Zobrazí detailní informace o videu včetně surového JSON
        public void DisplayDetailedInfo()
        {
            if (videoInfo == null) return;

            Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");
            DisplayVideoInfo();

            Console.WriteLine("\nVŠECHNY INFORMACE:\n");
            // Vypsání surových dat z FFprobe jako JSON
            Console.WriteLine(JsonSerializer.Serialize(videoInfo, new JsonSerializerOptions { WriteIndented = true }));
        }

        // Extrahuje video stopu z videa na základě výběru uživatele
        // Uživatel si vybere jednu ze stop a zadá název výstupního souboru
        public void ExtractVideoTrack()
        {
            // Ověří, že má video alespoň jednu video stopu
            if (videoInfo?.VideoStreams.Count == 0)
            {
                Console.WriteLine("Žádné video stopy!");
                Console.ReadLine();
                return;
            }

            Console.Clear();
            Console.WriteLine("EXTRAKCE VIDEO STOPY\n");

            // Zobrazí seznam dostupných video stop
            for (int i = 0; i < videoInfo.VideoStreams.Count; i++)
            {
                Console.WriteLine($"{i}: Stream #{videoInfo.VideoStreams[i].Index} - " +
                    $"{videoInfo.VideoStreams[i].Width}x{videoInfo.VideoStreams[i].Height} ({videoInfo.VideoStreams[i].Codec})");
            }

            // Nechá uživatele vybrat stopu
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

            // Připraví FFmpeg příkaz pro extrakci (bez překódování)
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

        // Extrahuje audio stopu v původním formátu (bez překódování)
        public void ExtractAudioTrack()
        {
            // Ověří, že má video alespoň jednu audio stopu
            if (videoInfo?.AudioStreams.Count == 0)
            {
                Console.WriteLine("Žádné audio stopy!");
                Console.ReadLine();
                return;
            }

            Console.Clear();
            Console.WriteLine("EXTRAKCE AUDIO STOPY\n");

            // Zobrazí seznam dostupných audio stop s informacemi
            for (int i = 0; i < videoInfo.AudioStreams.Count; i++)
            {
                Console.WriteLine($"{i}: Stream #{videoInfo.AudioStreams[i].Index} - " +
                    $"{videoInfo.AudioStreams[i].Language} ({videoInfo.AudioStreams[i].Codec}, {videoInfo.AudioStreams[i].Channels}ch)");
            }

            // Nechá uživatele vybrat stopu
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

            // Připraví FFmpeg příkaz pro extrakci audio (bez překódování)
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

        // Extrahuje titulky ze zvoleného streamu
        public void ExtractSubtitles()
        {
            // Ověří, že má video alespoň jednu titulkovou stopu
            if (videoInfo == null || videoInfo.SubtitleStreams.Count == 0)
            {
                Console.WriteLine("Žádné titulky!");
                Console.ReadLine();
                return;
            }

            Console.Clear();
            Console.WriteLine("EXTRAKCE TITULKŮ\n");

            // Zobrazí seznam dostupných titulkových stop
            for (int i = 0; i < videoInfo.SubtitleStreams.Count; i++)
            {
                Console.WriteLine($"{i}: Stream #{videoInfo.SubtitleStreams[i].Index} - " +
                    $"{videoInfo.SubtitleStreams[i].Language} ({videoInfo.SubtitleStreams[i].Codec})");
            }

            // Nechá uživatele vybrat titulkovou stopu
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

            // Připraví FFmpeg příkaz pro extrakci titulků
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

        // [GENEROVÁNO AI - Claude Opus 4.7] Parsuje JSON výstup z FFprobe a vytváří objekty VideoInfo
        // Odděluje streamy na video, audio a titulky
        // Přeskakuje obrázky připojené k videu (attached pictures)
        private VideoInfo ParseVideoInfo(JsonElement root)
        {
            var info = new VideoInfo();

            // Parsuje formát videa (délka, velikost)
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

            // Parsuje všechny streamy z videa
            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    if (stream.TryGetProperty("codec_type", out var codecType))
                    {
                        string codecTypeStr = codecType.GetString() ?? "";

                        // Přeskočí obrázky připojené k videu (například covery)
                        if (stream.TryGetProperty("disposition", out var disposition) && 
                            disposition.TryGetProperty("attached_pic", out var attachedPic) &&
                            attachedPic.GetInt32() == 1)
                        {
                            continue;
                        }

                        // Rozdělí streamy podle typu
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

        // Extrahuje stringovou hodnotu z JSON prvku (převádí i čísla na string)
        private string GetStringValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? "Unknown",
                JsonValueKind.Number => element.GetInt32().ToString(),
                _ => "Unknown"
            };
        }

        // Extrahuje celočíselnou hodnotu z JSON prvku
        private int GetIntValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => int.TryParse(element.GetString(), out var i) ? i : 0,
                JsonValueKind.Number => element.GetInt32(),
                _ => 0
            };
        }

        // Extrahuje dlouhé celočíselné (long) hodnoty z JSON prvku
        private long GetLongValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => long.TryParse(element.GetString(), out var l) ? l : 0,
                JsonValueKind.Number => element.GetInt64(),
                _ => 0
            };
        }

        // Extrahuje desítková (double) hodnoty z JSON prvku
        private double GetDoubleValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => double.TryParse(element.GetString(), out var d) ? d : 0,
                JsonValueKind.Number => element.GetDouble(),
                _ => 0
            };
        }

        // [GENEROVÁNO AI - Claude Opus 4.7] Parsuje video stream z JSON prvku a vytváří VideoStream objekt
        // Extrahuje informace o rozlišení, kodeku, bitrate, barvách, HDR, atd.
        private VideoStream ParseVideoStream(JsonElement stream)
         {
             var vs = new VideoStream();

             // Základní informace o streamu
             if (stream.TryGetProperty("index", out var idx))
                 vs.Index = GetIntValue(idx);

             if (stream.TryGetProperty("codec_name", out var codec))
                 vs.Codec = codec.GetString() ?? "Unknown";

             // Rozlišení videa
             if (stream.TryGetProperty("width", out var width))
                 vs.Width = GetIntValue(width);

             if (stream.TryGetProperty("height", out var height))
                 vs.Height = GetIntValue(height);

             // FPS videa
             if (stream.TryGetProperty("r_frame_rate", out var fps))
                 vs.FrameRate = fps.GetString() ?? "Unknown";

             // Bitrate videa
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

             // Informace o barvách
             if (stream.TryGetProperty("color_space", out var color))
                 vs.ColorSpace = color.GetString() ?? "Unknown";

             // Přenosová funkce a HDR detekce
             if (stream.TryGetProperty("color_transfer", out var transfer))
             {
                 string? transferStr = transfer.GetString() ?? "";
                 vs.ColorTransfer = transferStr;
                 // Detekuje HDR na základě přenosové funkce
                 vs.IsHDR = transferStr.Contains("smpte2084") || transferStr.Contains("arib-std-b67");
             }

             if (stream.TryGetProperty("color_primaries", out var primaries))
                 vs.ColorPrimaries = primaries.GetString() ?? "Unknown";

             // Profil a level kodeku
             if (stream.TryGetProperty("profile", out var profile))
                 vs.Profile = GetStringValue(profile);

             if (stream.TryGetProperty("level", out var level))
                 vs.Level = GetStringValue(level);

             // Bitová hloubka pixelu
             if (stream.TryGetProperty("bits_per_raw_sample", out var bpp))
             {
                 int bppValue = GetIntValue(bpp);
                 if (bppValue > 0)
                     vs.ColorDepth = bppValue;
             }

             // Poměr stran videa
             if (stream.TryGetProperty("display_aspect_ratio", out var dar))
                 vs.AspectRatio = dar.GetString() ?? "Unknown";
             else if (vs.Width > 0 && vs.Height > 0)
                 vs.AspectRatio = $"{vs.Width}:{vs.Height}";

             // Pixel formát
             if (stream.TryGetProperty("pix_fmt", out var pixfmt))
                 vs.PixelFormat = pixfmt.GetString() ?? "Unknown";

             // Pokud bitová hloubka není explicitně zadána, pokusí se ji extrahovat z pixel formátu
             if (vs.ColorDepth == 0 && !string.IsNullOrEmpty(vs.PixelFormat))
             {
                 var match = Regex.Match(vs.PixelFormat, @"p(\d+)");
                 if (match.Success && int.TryParse(match.Groups[1].Value, out int bits))
                     vs.ColorDepth = bits;
             }

             // Barevný rozsah
             if (stream.TryGetProperty("color_range", out var colorrange))
                 vs.ColorRange = colorrange.GetString() ?? "Unknown";

             // B-Frames
             if (stream.TryGetProperty("has_b_frames", out var bframes))
                 vs.HasBFrames = GetIntValue(bframes);

             return vs;
         }

        // Parsuje audio stream z JSON prvku a vytváří AudioStream objekt
        private AudioStream ParseAudioStream(JsonElement stream)
        {
            var audio = new AudioStream();

            if (stream.TryGetProperty("index", out var idx))
                audio.Index = GetIntValue(idx);

            if (stream.TryGetProperty("codec_name", out var codec))
                audio.Codec = codec.GetString() ?? "Unknown";

            // Extrahuje jazyk z tagů
            if (stream.TryGetProperty("tags", out var tags) && tags.TryGetProperty("language", out var lang))
                audio.Language = lang.GetString() ?? "Unknown";

            if (stream.TryGetProperty("channels", out var channels))
                audio.Channels = GetIntValue(channels);

            if (stream.TryGetProperty("channel_layout", out var layout))
                audio.ChannelLayout = layout.GetString() ?? "Unknown";

            if (stream.TryGetProperty("sample_rate", out var sample))
                audio.SampleRate = GetIntValue(sample);

            // Bitrate audio
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

        // Parsuje titulkový stream z JSON prvku a vytváří SubtitleStream objekt
        private SubtitleStream ParseSubtitleStream(JsonElement stream)
        {
            var sub = new SubtitleStream();

            if (stream.TryGetProperty("index", out var idx))
                sub.Index = GetIntValue(idx);

            if (stream.TryGetProperty("codec_name", out var codec))
                sub.Codec = codec.GetString() ?? "Unknown";

            // Extrahuje jazyk z tagů
            if (stream.TryGetProperty("tags", out var tags) && tags.TryGetProperty("language", out var lang))
                sub.Language = lang.GetString() ?? "Unknown";

            return sub;
        }

        // Spustí FFprobe a získá JSON informace o videu
        private string RunFFprobe(string args)
        {
            if (string.IsNullOrEmpty(ffprobePath))
            {
                Console.WriteLine("FFprobe není dostupný!");
                return "";
            }

            // Vytvoří nový proces pro spuštění FFprobe
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

        // [GENEROVÁNO AI - Claude Opus 4.7] Spustí FFmpeg příkaz a zobrazuje progress během zpracování
        // Čte standardní chybu a extrahuje informace o průběhu (čas, FPS, rychlost)
        private bool RunFFmpeg(string args)
         {
             if (string.IsNullOrEmpty(ffmpegPath))
             {
                 Console.WriteLine("FFmpeg není dostupný!");
                 return false;
             }

             // Vytvoří nový proces pro spuštění FFmpeg
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

                // [GENEROVÁNO AI - Claude Opus 4.7] Spouští asynchronní úlohu pro čtení FFmpeg výstupu
                // Umožňuje paralelní zobrazování progress informací
                System.Threading.Tasks.Task.Run(() =>
                 {
                     try
                     {
                         string? line;
                         while ((line = process.StandardError.ReadLine()) != null)
                         {
                             // Hledá linky s informacemi o průběhu
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

        // [GENEROVÁNO AI - Claude Opus 4.7] Extrahuje progress informace z FFmpeg výstupu pomocí regulárních výrazů
        // Parsuje: čas zpracování, FPS a rychlost zpracování
        private string ExtractFFmpegProgress(string ffmpegOutput)
        {
            string progress = "";

            // Extrahuje čas (format: HH:MM:SS)
            if (ffmpegOutput.Contains("time="))
            {
                var match = System.Text.RegularExpressions.Regex.Match(ffmpegOutput, @"time=([\d:]+)");
                if (match.Success)
                    progress += $"Čas: {match.Groups[1].Value} ";
            }

            // Extrahuje počet snímků za sekundu
            if (ffmpegOutput.Contains("fps="))
            {
                var match = System.Text.RegularExpressions.Regex.Match(ffmpegOutput, @"fps=\s*(\d+)");
                if (match.Success)
                    progress += $"| FPS: {match.Groups[1].Value} ";
            }

            // Extrahuje rychlost zpracování (například 1.5x znamená 1,5x rychleji než real-time)
            if (ffmpegOutput.Contains("speed="))
            {
                var match = System.Text.RegularExpressions.Regex.Match(ffmpegOutput, @"speed=([\d.]+)x");
                if (match.Success)
                    progress += $"| Rychlost: {match.Groups[1].Value}x";
            }

            return progress;
        }

        // Formátuje počet bajtů do čitelného formátu (B, KB, MB, GB)
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

    // Třída pro uložení informací o videu (obecné info, video/audio/titulky streamy)
    // Třída pro uložení informací o videu (obecné info, video/audio/titulky streamy)
    class VideoInfo
    {
        // Délka videa ve formátu HH:MM:SS
        public string Duration { get; set; } = "00:00:00";
        // Velikost souboru v bajtech
        public long FileSize { get; set; }
        // Seznam video streamů v souboru
        public List<VideoStream> VideoStreams { get; set; } = new();
        // Seznam audio streamů v souboru
        public List<AudioStream> AudioStreams { get; set; } = new();
        // Seznam titulkových streamů v souboru
        public List<SubtitleStream> SubtitleStreams { get; set; } = new();
    }

    // Třída pro uložení informací o video streamu
    // Třída pro uložení informací o video streamu
    class VideoStream
    {
        // Index streamu v souboru
        public int Index { get; set; }
        // Název kodeku (h264, h265, vp9, atd.)
        public string Codec { get; set; } = "";
        // Šířka videa v pixelech
        public int Width { get; set; }
        // Výška videa v pixelech
        public int Height { get; set; }
        // Počet snímků za sekundu (FPS)
        public string FrameRate { get; set; } = "";
        // Bitrate formátovaný jako string (kbps, Mbps)
        public string Bitrate { get; set; } = "";
        // Bitrate v bitech za sekundu (surová hodnota)
        public long BitrateRaw { get; set; }
        // Je-li video HDR?
        public bool IsHDR { get; set; }
        // Bitová hloubka pixelu (10, 12 bitů, atd.)
        public int ColorDepth { get; set; }
        // Barevný prostor (YUV, RGB, atd.)
        public string ColorSpace { get; set; } = "";
        // Přenosová funkce (linear, smpte2084 pro HDR, atd.)
        public string ColorTransfer { get; set; } = "";
        // Barevné primárky
        public string ColorPrimaries { get; set; } = "";
        // Profil kodeku (main, main10, atd.)
        public string Profile { get; set; } = "";
        // Level kodeku
        public string Level { get; set; } = "";
        // Poměr stran (16:9, 4:3, atd.)
        public string AspectRatio { get; set; } = "";
        // Formát pixelu (yuv420p, yuv420p10le, atd.)
        public string PixelFormat { get; set; } = "";
        // Rozsah barev (tv, pc)
        public string ColorRange { get; set; } = "";
        // Má video B-frames?
        public int HasBFrames { get; set; }
    }

    // Třída pro uložení informací o audio streamu
    // Třída pro uložení informací o audio streamu
    class AudioStream
    {
        // Index streamu v souboru
        public int Index { get; set; }
        // Název kodeku (aac, mp3, flac, opus, atd.)
        public string Codec { get; set; } = "";
        // Jazyk zvuku (cs, en, de, atd.)
        public string Language { get; set; } = "Unknown";
        // Počet kanálů (1 = mono, 2 = stereo, 6 = 5.1, atd.)
        public int Channels { get; set; }
        // Vzorkovací frekvence v Hz (44100, 48000, atd.)
        public int SampleRate { get; set; }
        // Bitrate formátovaný jako string (kbps, Mbps)
        public string Bitrate { get; set; } = "";
        // Bitrate v bitech za sekundu (surová hodnota)
        public long BitrateRaw { get; set; }
        // Rozmístění kanálů (stereo, 5.1, 7.1, atd.)
        public string ChannelLayout { get; set; } = "";
    }

    // Třída pro uložení informací o titulkovém streamu
    class SubtitleStream
    {
        // Index streamu v souboru
        public int Index { get; set; }
        // Typ titulků (subrip, ass, dvb_subtitle, atd.)
        public string Codec { get; set; } = "";
        // Jazyk titulků
        public string Language { get; set; } = "Unknown";
    }
}
