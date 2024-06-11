using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Flower_Orange_Cloud_Downloader.windows
{
    /// <summary>
    /// DownloadUrlWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadUrlWindow : Window
    {
        public string DownloadUrl { get; private set; }
        public bool UseTorrent { get; private set; }

        public DownloadUrlWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadUrl = UrlTextBox.Text;
            UseTorrent = UseTorrentCheckBox.IsChecked ?? false;
            if (string.IsNullOrEmpty(DownloadUrl))
            {
                MessageBox.Show("下载链接不能为空！");
            }
            else
            {
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Border_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (System.IO.Path.GetExtension(file).Equals(".torrent", StringComparison.OrdinalIgnoreCase))
                    {
                        UrlTextBox.Text = file;
                        UseTorrentCheckBox.IsChecked = true;
                        break;
                    }
                }
            }
        }
    }
}
