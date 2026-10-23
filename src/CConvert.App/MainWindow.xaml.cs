using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using CConvert.Core;
using Microsoft.Win32;

namespace CConvert.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<FileItem> _items = new();
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        FileGrid.ItemsSource = _items;
        TargetBox.ItemsSource = new[] { CorelVersion.V2019 }.Select(v => $"CorelDRAW {v.Name}");
        // File/thư mục truyền qua dòng lệnh (vd từ "Send To" của Windows) được thêm sẵn.
        AddPaths(Environment.GetCommandLineArgs().Skip(1));
        UpdateStatus();
        SourceInitialized += (_, _) => UseDarkTitleBar();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Thanh tiêu đề Windows màu tối cho hợp tông (Windows 10 2004+ / 11; bản cũ hơn bỏ qua).</summary>
    private void UseDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int on = 1;
        if (DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0)
            DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
    }

    // ---------- Thêm file ----------

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) && _cts is null ? DragDropEffects.Copy : DragDropEffects.None;
        DropBorder.Opacity = e.Effects == DragDropEffects.Copy ? 1 : 0.55;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e) => DropBorder.Opacity = 0.55;

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropBorder.Opacity = 0.55;
        if (_cts is null && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            AddPaths(paths);
    }

    private void OnAddFiles(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "CorelDRAW (*.cdr)|*.cdr", Multiselect = true, Title = "Chọn file CorelDRAW" };
        if (dlg.ShowDialog(this) == true) AddPaths(dlg.FileNames);
    }

    private void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục chứa file .cdr (gồm cả thư mục con)" };
        if (dlg.ShowDialog(this) == true) AddPaths([dlg.FolderName]);
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        var known = new HashSet<string>(_items.Select(i => i.Path), StringComparer.OrdinalIgnoreCase);
        foreach (string p in paths)
        {
            IEnumerable<string> files = Directory.Exists(p)
                ? Directory.EnumerateFiles(p, "*.cdr", SearchOption.AllDirectories)
                : string.Equals(Path.GetExtension(p), ".cdr", StringComparison.OrdinalIgnoreCase) ? [p] : [];
            foreach (string f in files)
            {
                if (!known.Add(f) || f.EndsWith(".c_converter.tmp", StringComparison.OrdinalIgnoreCase)) continue;
                var (ver, desc) = CdrInfo.Probe(f);
                var item = new FileItem(f) { SourceVersion = ver is { IsExperimental: true } ? $"CorelDRAW {ver.Name} · thử nghiệm" : desc };
                if (ver is null) item.SetState(FileState.Skipped, "Bỏ qua");
                else if (ver.Vrsn <= CorelVersion.V2019.Vrsn) item.SetState(FileState.Skipped, "Đã là 2019 trở xuống");
                _items.Add(item);
            }
        }
        UpdateStatus();
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        _items.Clear();
        LogBox.Clear();
        UpdateStatus();
    }

    private void OnPickOutDir(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục lưu kết quả" };
        if (dlg.ShowDialog(this) == true) OutDirBox.Text = dlg.FolderName;
    }

    // ---------- Chạy ----------

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        var todo = _items.Where(i => i.State is FileState.Pending or FileState.Failed).ToList();
        if (todo.Count == 0)
        {
            MessageBox.Show(this, "Không có file nào cần chuyển. Hãy thêm file .cdr bản 2020–2024 (2025/2026: thử nghiệm).", "C_Converter");
            return;
        }
        string? outDir = null;
        if (ToFolder.IsChecked == true)
        {
            outDir = OutDirBox.Text.Trim();
            if (outDir.Length == 0)
            {
                MessageBox.Show(this, "Hãy chọn thư mục lưu kết quả.", "C_Converter");
                return;
            }
            Directory.CreateDirectory(outDir);
        }

        var target = CorelVersion.V2019;
        _cts = new CancellationTokenSource();
        SetRunning(true);
        Progress.Maximum = todo.Count;
        Progress.Value = 0;
        try
        {
            foreach (var item in todo)
            {
                if (_cts.IsCancellationRequested) break;
                item.SetState(FileState.Running, "Đang chuyển…");
                string output = CdrInfo.OutputPathFor(item.Path, target, outDir);
                // Mỗi file chạy độc lập: lỗi của một file không làm dừng cả lô.
                var result = await Task.Run(() => Downgrader.Run(item.Path, output, target));
                item.Log = string.Join(Environment.NewLine, result.Log.Select(l => l.ToString()));
                item.WarningCount = result.Log.Count(l => l.Level == Core.LogLevel.Warning);
                if (result.Success)
                {
                    item.OutputPath = output;
                    if (result.Experimental)
                        item.SetState(FileState.DoneExperimental, "Xong (thử nghiệm)");
                    else
                        item.SetState(result.HasWarnings ? FileState.DoneWithWarnings : FileState.Done,
                            result.HasWarnings ? "Xong (có cảnh báo)" : "Xong");
                }
                else
                {
                    item.SetState(FileState.Failed, "Lỗi");
                }
                Progress.Value++;
                UpdateStatus();
                if (FileGrid.SelectedItem == item || FileGrid.SelectedItem is null) ShowLog();
            }
        }
        finally
        {
            foreach (var item in _items.Where(i => i.State == FileState.Running))
                item.SetState(FileState.Pending, "Chờ");
            _cts = null;
            SetRunning(false);
            UpdateStatus();
            ShowLog();
        }
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        StopButton.IsEnabled = false;
        StatusText.Text = "Đang dừng sau file hiện tại…";
    }

    private void SetRunning(bool running)
    {
        StartButton.IsEnabled = !running;
        StopButton.IsEnabled = running;
        ClearButton.IsEnabled = !running;
        TargetBox.IsEnabled = !running;
    }

    private void UpdateStatus()
    {
        int total = _items.Count;
        int done = _items.Count(i => i.State is FileState.Done or FileState.DoneWithWarnings or FileState.DoneExperimental);
        int warn = _items.Count(i => i.State is FileState.DoneWithWarnings or FileState.DoneExperimental);
        int fail = _items.Count(i => i.State == FileState.Failed);
        int skip = _items.Count(i => i.State == FileState.Skipped);
        StatTotal.Text = total.ToString();
        StatDone.Text = done.ToString();
        StatWarn.Text = warn.ToString();
        StatFail.Text = fail.ToString();
        DropHint.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = total == 0
            ? "Chưa có file nào."
            : skip > 0 ? $"{done}/{total - skip} file · {skip} bỏ qua" : $"{done}/{total} file";
    }

    // ---------- Nhật ký ----------

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => ShowLog();

    private void ShowLog()
    {
        if (FileGrid.SelectedItem is FileItem one)
        {
            LogTitle.Text = "NHẬT KÝ · " + one.FileName;
            LogBox.Text = $"{one.Path}{Environment.NewLine}{one.Log}";
            LogBox.ScrollToHome();
            return;
        }
        LogTitle.Text = "NHẬT KÝ";
        LogBox.Text = BuildFullLog();
        LogBox.ScrollToEnd();
    }

    private string BuildFullLog()
    {
        var sb = new StringBuilder();
        foreach (var i in _items.Where(i => i.Log.Length > 0 || i.State == FileState.Skipped))
        {
            sb.AppendLine($"=== {i.Path}  [{i.StatusText}]");
            if (i.State == FileState.Skipped) sb.AppendLine($"  Bỏ qua: {i.SourceVersion}");
            foreach (string line in i.Log.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
                sb.AppendLine("  " + line);
        }
        return sb.ToString();
    }

    private void OnSaveLog(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "Văn bản (*.txt)|*.txt", FileName = $"C_Converter_log_{DateTime.Now:yyyyMMdd_HHmm}.txt" };
        if (dlg.ShowDialog(this) == true)
            File.WriteAllText(dlg.FileName, BuildFullLog(), new UTF8Encoding(true));
    }

    private void OnOpenOutput(object sender, RoutedEventArgs e)
    {
        string? dir = (FileGrid.SelectedItem as FileItem)?.OutputPath is { Length: > 0 } p
            ? Path.GetDirectoryName(p)
            : ToFolder.IsChecked == true && OutDirBox.Text.Length > 0 ? OutDirBox.Text
            : _items.FirstOrDefault(i => i.OutputPath.Length > 0)?.OutputPath is { } q ? Path.GetDirectoryName(q) : null;
        if (dir is not null && Directory.Exists(dir))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }
}

public enum FileState { Pending, Running, Done, DoneWithWarnings, DoneExperimental, Failed, Skipped }

public sealed class FileItem(string path) : INotifyPropertyChanged
{
    public string Path { get; } = path;
    public string FileName => System.IO.Path.GetFileName(Path);

    private string _sourceVersion = "";
    public string SourceVersion { get => _sourceVersion; set => Set(ref _sourceVersion, value); }

    private FileState _state = FileState.Pending;
    public FileState State => _state;

    private string _statusText = "Chờ";
    public string StatusText => _statusText;

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    public Brush StatusBrush => _state switch
    {
        FileState.Done => Res("Success"),
        FileState.DoneWithWarnings => Res("Warning"),
        FileState.DoneExperimental => Res("Info"),
        FileState.Failed => Res("Danger"),
        FileState.Running => Res("Info"),
        FileState.Skipped => Res("TextMuted"),
        _ => Res("Text"),
    };

    /// <summary>Nền mờ cùng màu cho "chip" trạng thái.</summary>
    public Brush StatusBackground
    {
        get
        {
            var c = ((SolidColorBrush)StatusBrush).Color;
            var b = new SolidColorBrush(Color.FromArgb(0x26, c.R, c.G, c.B));
            b.Freeze();
            return b;
        }
    }

    private int _warningCount;
    public int WarningCount { get => _warningCount; set => Set(ref _warningCount, value); }

    private string _outputPath = "";
    public string OutputPath { get => _outputPath; set => Set(ref _outputPath, value); }

    public string Log { get; set; } = "";

    public void SetState(FileState state, string text)
    {
        _state = state;
        _statusText = text;
        OnChanged(nameof(State));
        OnChanged(nameof(StatusText));
        OnChanged(nameof(StatusBrush));
        OnChanged(nameof(StatusBackground));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnChanged(name);
    }

    private void OnChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
