using iu5nt.Kostyan_level;
using System;
using System.IO;
using System.IO.Ports;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Threading;
using wf = System.Windows.Forms;

namespace iu5nt
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private wf.OpenFileDialog fileDialog = new wf.OpenFileDialog();
        private wf.FolderBrowserDialog folderDialog = new wf.FolderBrowserDialog();

        private bool folderReady = false;
        private bool sendReady = false;
        private bool? sending = null;

        private Stream fileStream;
        private DispatcherTimer timer = new DispatcherTimer() {
            Interval = new TimeSpan(0, 0, 1) // 1 second
        };

        public MainWindow()
        {
            InitializeComponent();
            PortsList.ItemsSource = SerialPort.GetPortNames();
            DataLink.onRecieve += ReceiveMessage;
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var port = PortsList.SelectedItem;
            if (port == null)
            {
                MessageBox.Show("Сначала необходимо выбрать порт.");
            }
            else
            {
                //try
                //{
                    Physical.Connect((string)port);
                    OpenButton.IsEnabled = false;
                    CloseButton.IsEnabled = true;
                    PortsList.IsEnabled = false;
                    FileBox.IsEnabled = true;
                    DirectoryBox.IsEnabled = true;
                    StatusText.Text = "Физическое соединение открыто.";
                //}
                //catch (Exception er)
                //{
                //    MessageBox.Show(er.Message);
                //}
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Physical.Disconnect();
                OpenButton.IsEnabled = true;
                CloseButton.IsEnabled = false;
                PortsList.IsEnabled = true;
                FileBox.IsEnabled = false;
                DirectoryBox.IsEnabled = false;
                StatusText.Text = "Физическое соединение закрыто.";
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
            }
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            var result = fileDialog.ShowDialog();
            if (result == wf.DialogResult.OK)
            {
                FileName.Text = fileDialog.FileName;
                SendFile.IsEnabled = true;
                sendReady = false;
                sending = null;
            }
        }

        private void SelectDirectory_Click(object sender, RoutedEventArgs e)
        {
            var result = folderDialog.ShowDialog();
            if (result == wf.DialogResult.OK)
            {
                DirectoryName.Text = folderDialog.SelectedPath;
                folderReady = true;
                sending = null;
                StatusText.Text = "Ожидаем логического соединения...";
            }
        }

        private void SendFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                fileStream = fileDialog.OpenFile();
                var hash = new SHA512CryptoServiceProvider().ComputeHash(fileStream);
                fileStream.Seek(0, SeekOrigin.Begin);

                var stream = new MemoryStream();
                var writer = new BinaryWriter(stream);

                writer.Write((byte)MessageType.FileName);
                writer.Write(fileDialog.SafeFileName);
                writer.Write(fileStream.Length);
                writer.Write(hash); // 64 bytes for security

                DataLink.SendPacket(stream.ToArray());
                sending = true;
                CloseButton.IsEnabled = false;
                FileBox.IsEnabled = false;
                DirectoryBox.IsEnabled = false;
                StatusText.Text = "Установка логического соединения...";

                timer.Tick += FileNameSending_Timeout;
                timer.Start();
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
            }
        }

        private void FileNameSending_Timeout(object sender, EventArgs e)
        {
            CloseButton.IsEnabled = true;
            FileBox.IsEnabled = true;
            DirectoryBox.IsEnabled = true;
            StatusText.Text = "Физическое соединение открыто.";
            MessageBox.Show("Принимающая сторона не готова к логическому соединению.");

            timer.Tick -= FileNameSending_Timeout;
        }

        private void ReceiveMessage(byte[] packet, bool check)
        {
            if (!check)
            {
                MessageBox.Show("Получен повреждённый пакет.");
                return;
            }

            var stream = new MemoryStream(packet);
            var reader = new BinaryReader(stream);
            switch ((MessageType)reader.ReadByte())
            {
                case MessageType.FileName:
                    ParseFileName(reader);
                    break;
                case MessageType.FileNameReceived:
                    timer.Stop();
                    StatusText.Text = "Логическое соединение установлено.";
                    break;
            }
        }

        private void ParseFileName(BinaryReader reader)
        {
            try
            {
                DataLink.SendPacket(new byte[] { (byte)MessageType.FileNameReceived });
                sending = false;

                var fileName = reader.ReadString();
                var length = reader.ReadInt64();
                var hash = reader.ReadBytes(64);

                var hashName = "";
                foreach (var b in hash)
                {
                    hashName += b.ToString("x2");
                }

                //MessageBox.Show(fileName + ", " + length + ", " + hashName);
                //StatusText.Text = "Логическое соединение установлено.";
            }
            catch (Exception er)
            {
                MessageBox.Show(er.Message);
            }
        }

        private enum MessageType:byte
        {
            FileName, FileNameReceived, FileRequest
        }
    }
}
