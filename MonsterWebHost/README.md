# MonsterWebHost

MonsterWebHost is a Windows 11 WPF app for selecting a website folder and hosting it locally with Kestrel, Cloudflared, live metrics, folder monitoring, JSONL logging, and SQLite-backed analytics.

## Notes

- Build target: .NET 8 WPF on Windows.
- Uses `System.Windows.Forms.FolderBrowserDialog` for folder selection.
- Stores logs in `%LOCALAPPDATA%\MonsterWebHost\Logs`.
- Stores analytics in `%LOCALAPPDATA%\MonsterWebHost\Data\analytics.db`.
