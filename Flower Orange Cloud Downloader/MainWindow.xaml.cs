using Flower_Orange_Cloud_Downloader.windows;
using MonoTorrent.Client;
using MonoTorrent;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace Flower_Orange_Cloud_Downloader
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<DownloadItem> Downloads { get; set; }
        private ClientEngine engine;
        private Dictionary<DownloadItem, TorrentManager> downloadManagers;

        public MainWindow()
        {
            InitializeComponent();
            Downloads = new ObservableCollection<DownloadItem>();
            DownloadListView.ItemsSource = Downloads;
            engine = new ClientEngine(new EngineSettings());
            downloadManagers = new Dictionary<DownloadItem, TorrentManager>();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var downloadUrlWindow = new DownloadUrlWindow();
            if (downloadUrlWindow.ShowDialog() == true)
            {
                string url = downloadUrlWindow.DownloadUrl;
                bool useTorrent = downloadUrlWindow.UseTorrent;
                string fileName = Path.GetFileName(url);
                var downloadItem = new DownloadItem
                {
                    FileName = fileName,
                    Size = "0 MB",
                    Status = "正在下载",
                    Bandwidth = "0 KB/s",
                    RemainingTime = "未知",
                    LastAttempt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                Downloads.Add(downloadItem);

                try
                {
                    if (useTorrent)
                    {
                        await DownloadTorrentFileAsync(url, downloadItem);
                    }
                    else
                    {
                        await DownloadFileAsync(url, downloadItem);
                    }
                    downloadItem.Status = "下载完成";
                }
                catch (Exception ex)
                {
                    downloadItem.Status = "下载失败";
                    MessageBox.Show($"下载失败: {ex.Message}");
                }
            }
        }

        private async Task DownloadFileAsync(string url, DownloadItem downloadItem)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;
                downloadItem.Size = totalBytes.HasValue ? $"{totalBytes.Value / (1024 * 1024)} MB" : "未知";

                using (Stream contentStream = await response.Content.ReadAsStreamAsync(), fileStream = new FileStream(downloadItem.FileName, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    byte[] buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        // 更新下载进度
                        if (totalBytes.HasValue)
                        {
                            downloadItem.Size = $"{totalRead / (1024 * 1024)} MB / {totalBytes.Value / (1024 * 1024)} MB";
                        }

                        // 更新带宽和剩余时间
                        var elapsed = stopwatch.Elapsed.TotalSeconds;
                        if (elapsed > 0)
                        {
                            var speed = totalRead / elapsed;
                            downloadItem.Bandwidth = $"{speed / 1024:0.00} KB/s";
                            if (totalBytes.HasValue)
                            {
                                var remainingBytes = totalBytes.Value - totalRead;
                                if (speed > 0)
                                {
                                    var remainingTime = remainingBytes / speed;
                                    downloadItem.RemainingTime = $"{remainingTime / 60:0} 分钟 {remainingTime % 60:0} 秒";
                                }
                                else
                                {
                                    downloadItem.RemainingTime = "未知";
                                }
                            }
                        }
                    }
                }
            }
        }

        private async Task DownloadTorrentFileAsync(string torrentFilePath, DownloadItem downloadItem)
        {
            var torrent = await Torrent.LoadAsync(torrentFilePath);
            var manager = await engine.AddAsync(torrent, "下载目录路径");
            downloadManagers[downloadItem] = manager;

            manager.TorrentStateChanged += (sender, args) =>
            {
                downloadItem.Status = args.NewState.ToString();
            };

            manager.PeersFound += (sender, args) =>
            {
                var speed = manager.Monitor.DownloadSpeed;
                downloadItem.Bandwidth = $"{speed / 1024:0.00} KB/s";
                downloadItem.Size = $"{manager.Monitor.DataBytesDownloaded / (1024 * 1024)} MB / {torrent.Size / (1024 * 1024)} MB";
                if (speed > 0)
                {
                    var remainingTime = (torrent.Size - manager.Monitor.DataBytesDownloaded) / speed;
                    downloadItem.RemainingTime = $"{remainingTime / 60:0} 分钟 {remainingTime % 60:0} 秒";
                }
                else
                {
                    downloadItem.RemainingTime = "未知";
                }
            };

            await manager.StartAsync();

            // 轮询检查下载状态
            while (manager.State != TorrentState.Seeding && manager.State != TorrentState.Stopped)
            {
                await Task.Delay(1000);
                var speed = manager.Monitor.DownloadSpeed;
                downloadItem.Bandwidth = $"{speed / 1024:0.00} KB/s";
                downloadItem.Size = $"{manager.Monitor.DataBytesDownloaded / (1024 * 1024)} MB / {torrent.Size / (1024 * 1024)} MB";
                if (speed > 0)
                {
                    var remainingTime = (torrent.Size - manager.Monitor.DataBytesDownloaded) / speed;
                    downloadItem.RemainingTime = $"{remainingTime / 60:0} 分钟 {remainingTime % 60:0} 秒";
                }
                else
                {
                    downloadItem.RemainingTime = "未知";
                }
            }
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (Path.GetExtension(file).Equals(".torrent", StringComparison.OrdinalIgnoreCase))
                    {
                        string fileName = Path.GetFileName(file);
                        var downloadItem = new DownloadItem
                        {
                            FileName = fileName,
                            Size = "0 MB",
                            Status = "正在下载",
                            Bandwidth = "0 KB/s",
                            RemainingTime = "未知",
                            LastAttempt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        };
                        Downloads.Add(downloadItem);

                        try
                        {
                            await DownloadTorrentFileAsync(file, downloadItem);
                            downloadItem.Status = "下载完成";
                        }
                        catch (Exception ex)
                        {
                            downloadItem.Status = "下载失败";
                            MessageBox.Show($"下载失败: {ex.Message}");
                        }
                    }
                }
            }
        }

        private async void PauseDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadListView.SelectedItem is DownloadItem selectedItem && downloadManagers.TryGetValue(selectedItem, out var manager))
            {
                if (manager.State == TorrentState.Downloading)
                {
                    await manager.StopAsync();
                    selectedItem.Status = "已暂停";
                }
            }
        }

        private async void ResumeDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadListView.SelectedItem is DownloadItem selectedItem && downloadManagers.TryGetValue(selectedItem, out var manager))
            {
                if (manager.State == TorrentState.Stopped)
                {
                    await manager.StartAsync();
                    selectedItem.Status = "正在下载";
                }
            }
        }

        private async void DeleteDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadListView.SelectedItem is DownloadItem selectedItem && downloadManagers.TryGetValue(selectedItem, out var manager))
            {
                if (manager.State == TorrentState.Downloading || manager.State == TorrentState.Paused)
                {
                    await manager.StopAsync();
                }
                downloadManagers.Remove(selectedItem);
                Downloads.Remove(selectedItem);
            }
        }
    }

    public class DownloadItem : INotifyPropertyChanged
    {
        private string fileName;
        private string size;
        private string status;
        private string bandwidth;
        private string remainingTime;
        private string lastAttempt;

        public string FileName
        {
            get { return fileName; }
            set { fileName = value; OnPropertyChanged(nameof(FileName)); }
        }

        public string Size
        {
            get { return size; }
            set { size = value; OnPropertyChanged(nameof(Size)); }
        }

        public string Status
        {
            get { return status; }
            set { status = value; OnPropertyChanged(nameof(Status)); }
        }

        public string Bandwidth
        {
            get { return bandwidth; }
            set { bandwidth = value; OnPropertyChanged(nameof(Bandwidth)); }
        }

        public string RemainingTime
        {
            get { return remainingTime; }
            set { remainingTime = value; OnPropertyChanged(nameof(RemainingTime)); }
        }

        public string LastAttempt
        {
            get { return lastAttempt; }
            set { lastAttempt = value; OnPropertyChanged(nameof(LastAttempt)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
