# 📹 Video Analyzer

A powerful console application for analyzing and extracting video, audio, and subtitle streams from multimedia files. Built with C# and .NET 10, featuring FFmpeg/FFprobe integration and beautiful formatted output using Spectre.Console.

## ✨ Features

### Video Analysis
- **Detailed Video Information**: Codec, profile, level, resolution, FPS, bitrate
- **Advanced Color Data**: Bit depth, pixel format, color range, primaries, transfer function
- **HDR Detection**: Automatic detection of HDR content (smpte2084, arib-std-b67)
- **Aspect Ratio**: Display and parse aspect ratios from video metadata

### Audio Tracks
- Codec, channels, sample rate information
- Language identification
- Bitrate calculation
- Channel layout details

### Subtitles
- Subtitle track detection and listing
- Language and codec information
- Easy extraction to SRT format

### Stream Extraction
- 🎬 **Extract Video Tracks**: Export video streams to MP4 format
- 🔊 **Extract Audio Tracks**: Save audio in original format (MKA)
- 📝 **Extract Subtitles**: Convert and save subtitle files (SRT)
- ⏱️ **Progress Indicators**: Real-time processing status with FFmpeg output

### UI/UX
- Interactive console interface with formatted tables
- Color-coded output for better readability
- Intuitive menu system
- Comprehensive error handling

## 🛠️ Technology Stack

| Technology | Purpose |
|-----------|---------|
| **C# 14.0** | Language |
| **.NET 10** | Runtime Framework |
| **FFmpeg** | Video/Audio encoding & analysis |
| **FFprobe** | Media file inspection |
| **System.Text.Json** | JSON parsing and data serialization |
| **Spectre.Console** | Beautiful console tables and formatting |
| **System.Text.RegularExpressions** | Metadata pattern extraction |

## 📋 Requirements

- **.NET 10 SDK** or later
- **FFmpeg** installed and accessible via system PATH or at:
  - `C:\ProgramData\chocolatey\bin\ffmpeg.exe`
  - `C:\ProgramData\chocolatey\bin\ffprobe.exe`

### Install FFmpeg

**Windows (via Chocolatey):**
```powershell
choco install ffmpeg
```

**Windows (Manual):**
1. Download from https://ffmpeg.org/download.html
2. Extract to a known location
3. Add to system PATH or update `FFMPEG_PATH` and `FFPROBE_PATH` constants in the code

**Linux:**
```bash
sudo apt-get install ffmpeg
```

**macOS:**
```bash
brew install ffmpeg
```

## 🚀 Installation

1. **Clone the repository:**
```bash
git clone https://github.com/yourusername/VideoAnalyzer.git
cd VideoAnalyzer
```

2. **Install dependencies:**
```bash
dotnet restore
```

3. **Build the project:**
```bash
dotnet build
```

4. **Run the application:**
```bash
dotnet run
```

## 📖 Usage

### Basic Workflow

1. **Launch the application:**
```bash
dotnet run
```

2. **Enter video file path:**
```
Enter path to video file (mkv, mp4, avi, etc.): /path/to/video.mkv
```

3. **View video information** - The application displays a comprehensive table with:
   - File information (name, size, duration, total bitrate)
   - Video stream details
   - Audio track information
   - Subtitle information

4. **Choose an action:**
```
╔════════════════════════════════════════╗
║            AVAILABLE COMMANDS          ║
╠════════════════════════════════════════╣
║  V - Extract VIDEO track               ║
║  A - Extract AUDIO track               ║
║  S - Extract SUBTITLES                 ║
║  I - Display detailed INFORMATION      ║
║  L - Load NEW VIDEO                    ║
║  Q - Quit program                      ║
╚════════════════════════════════════════╝
```

### Command Examples

#### Extract Video Track
```
Enter command: V
Select track (number): 0
Enter output file name (without extension): output_video
```
Output: `output_video.mp4`

#### Extract Audio Track
```
Enter command: A
Select track (number): 0
Enter output file name (without extension): output_audio
```
Output: `output_audio.mka`

#### Extract Subtitles
```
Enter command: S
Select track (number): 0
Enter output file name (without extension): output_subs
```
Output: `output_subs.srt`

#### View Detailed Information
```
Enter command: I
```
Displays all metadata including raw JSON output.

## 📊 Supported Video Formats

- **Containers**: MKV, MP4, AVI, MOV, FLV, WebM, and more (FFmpeg supported)
- **Video Codecs**: H.264, H.265, VP8, VP9, AV1, and others
- **Audio Codecs**: AAC, MP3, OPUS, FLAC, DTS, and others
- **Subtitle Formats**: SRT, ASS, VTT, and others

## 📁 Project Structure

```
ConsoleApp1/
├── Program.cs                 # Main application file
├── ConsoleApp1.csproj        # Project configuration
├── README.md                 # This file
├── .gitignore                # Git ignore rules
└── bin/                       # Build output
```

### Core Classes

- **VideoAnalyzer**: Main analyzer class handling FFmpeg/FFprobe integration
- **VideoInfo**: Container for all video metadata
- **VideoStream**: Video stream properties
- **AudioStream**: Audio stream properties
- **SubtitleStream**: Subtitle stream properties

## 🔍 Data Extraction

### Video Stream Information
- Index, codec name, profile, level
- Width, height, FPS, bitrate
- Aspect ratio, pixel format
- Color space, range, primaries, transfer function
- HDR status, B-frames count
- Color depth (with fallback regex extraction)

### Audio Stream Information
- Index, codec, language
- Channels, channel layout
- Sample rate, bitrate

### Subtitle Stream Information
- Index, codec, language

## ⚙️ Configuration

### FFmpeg Paths
Edit `VideoAnalyzer` class constants if FFmpeg is installed at a different location:

```csharp
private const string FFMPEG_PATH = @"C:\ProgramData\chocolatey\bin\ffmpeg.exe";
private const string FFPROBE_PATH = @"C:\ProgramData\chocolatey\bin\ffprobe.exe";
```

## 🐛 Troubleshooting

### FFmpeg Not Found
**Error:** "FFprobe is not available"

**Solution:**
1. Verify FFmpeg installation: `ffmpeg -version`
2. Add FFmpeg to system PATH
3. Update constants in code with correct path

### Invalid Video File
**Error:** "Video file not found!"

**Solution:**
1. Check file path spelling
2. Use full/absolute path instead of relative path
3. Verify file permissions

### JSON Parsing Error
**Error:** "FFprobe returned empty output"

**Solution:**
1. Ensure FFmpeg is properly installed
2. Try with different video file
3. Update FFmpeg to latest version

## 📈 Future Enhancements

- [ ] Batch processing multiple files
- [ ] GUI interface with WinForms or WPF
- [ ] Output format conversion
- [ ] Video transcoding support
- [ ] Thumbnail generation
- [ ] Metadata editing
- [ ] File comparison tool
- [ ] Database export functionality

## 📝 License

This project is provided as-is for educational and personal use.

## 👤 Author

Created by Honza (honza)

## 🤝 Contributing

Contributions are welcome! Feel free to:
1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Open a Pull Request

## 📧 Support

For issues, questions, or suggestions, please open an issue on GitHub.

---

**Note:** This application requires FFmpeg to be installed separately. FFmpeg is a free, open-source multimedia framework. Learn more at [ffmpeg.org](https://ffmpeg.org/)
