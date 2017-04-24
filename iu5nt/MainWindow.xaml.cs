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
    public partial class MainWindow : Window
    {
        private wf.OpenFileDialog fileDialog = new wf.OpenFileDialog();
        private wf.FolderBrowserDialog folderDialog = new wf.FolderBrowserDialog();

        private bool folderReady = false;
        private bool? sending = null;
        private Stream fileStream;
        private string fileName, hashName, filePath, tempPath;
        private long length;
        private const short chunkSize = 512;

        private DispatcherTimer timer = new DispatcherTimer() {
            Interval = new TimeSpan(0, 0, 2) // 2 seconds
        };
        private ushort retries = 0;
        private const ushort maxRetries = 3;
        private byte[] lastPacket;

        public MainWindow()
        {
            InitializeComponent();
            PortsList.ItemsSource = SerialPort.GetPortNames();
            timer.Tick += ResendPacket;
            DataLink.onRecieve += InvokeHandler;
            Physical.onCheck += PortCheck;
            Physical.UICheck += PortCheck;
            Dispatcher.UnhandledException += ExceptionHandler;
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
                Physical.Connect((string)port);
                if (folderReady)
                {
                    Physical.SetRts(true);
                }
                OpenButton.IsEnabled = false;
                CloseButton.IsEnabled = true;
                PortsList.IsEnabled = false;
                FileBox.IsEnabled = true;
                DirectoryBox.IsEnabled = true;
                StatusText.Text = "Физическое соединение открыто.";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Physical.Disconnect();
            DsrIndicator.IsChecked = false;
            CtsIndicator.IsChecked = false;
            OpenButton.IsEnabled = true;
            CloseButton.IsEnabled = false;
            PortsList.IsEnabled = true;
            FileBox.IsEnabled = false;
            DirectoryBox.IsEnabled = false;
            StatusText.Text = "Физическое соединение закрыто.";
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            var result = fileDialog.ShowDialog();
            if (result == wf.DialogResult.OK)
            {
                FileName.Text = fileDialog.FileName;
                SendFile.IsEnabled = true;
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
                Physical.SetRts(true);
                sending = null;
                StatusText.Text = "Ожидаем логического соединения...";
            }
        }

        private void SendFile_Click(object sender, RoutedEventArgs e)
        {
            if (CtsIndicator.IsChecked != true || DsrIndicator.IsChecked != true)
            {
                MessageBox.Show("Принимающая сторона не готова к логическому соединению.");
                return;
            }

            fileStream = fileDialog.OpenFile();
            var hash = new SHA512CryptoServiceProvider().ComputeHash(fileStream);
            fileStream.Seek(0, SeekOrigin.Begin);

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);

            writer.Write((byte)MessageType.FileName);
            writer.Write(fileDialog.SafeFileName);
            writer.Write(fileStream.Length);
            writer.Write(hash); // 64 bytes for security

            sending = true;
            CloseButton.IsEnabled = false;
            FileBox.IsEnabled = false;
            DirectoryBox.IsEnabled = false;
            StatusText.Text = "Установка логического соединения...";
            Title = "Отправляем " + fileDialog.SafeFileName;
            Physical.SetRts(true);
            SendPacket(stream.ToArray());
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            sending = null;
            CloseButton.IsEnabled = true;
            FileBox.IsEnabled = true;
            DirectoryBox.IsEnabled = true;
            DisconnectButton.IsEnabled = true;
            if (fileStream != null)
            {
                fileStream.Close();
            }

            StatusText.Text = "Логическое соединение разорвано.";
            SendPacket(new byte[] { (byte)MessageType.Disconnect });
            MessageBox.Show("Логическое соединение разорвано.");
        }

        private void InvokeHandler(byte[] packet, bool check)
        {
            Dispatcher.Invoke(new DataLink.RecieveMEthod(ReceiveMessage), DispatcherPriority.Send, packet, check);
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
                case MessageType.ReceiveNotReady:
                    timer.Stop();
                    CloseButton.IsEnabled = true;
                    FileBox.IsEnabled = true;
                    DirectoryBox.IsEnabled = true;
                    StatusText.Text = "Физическое соединение открыто.";
                    MessageBox.Show("Принимающая сторона не готова к логическому соединению.");
                    break;
                case MessageType.FileRequest:
                    SendFileChunk(reader);
                    break;
                case MessageType.FileChunk:
                    SaveFileChunk(reader);
                    break;
                case MessageType.FileReceived:
                    timer.Stop();
                    sending = null;
                    CloseButton.IsEnabled = true;
                    FileBox.IsEnabled = true;
                    DirectoryBox.IsEnabled = true;
                    DisconnectButton.IsEnabled = false;
                    StatusText.Text = "Файл успешно передан.";
                    Title = "Локальная безадаптерная сеть";
                    ProgressBar.Value = 1;
                    SendPacket(new byte[] { (byte)MessageType.FileReceivedOk });
                    Physical.SetRts(false);
                    break;
                case MessageType.FileReceivedOk:
                    if (sending == null) break;
                    timer.Stop();
                    sending = null;
                    CloseButton.IsEnabled = true;
                    FileBox.IsEnabled = true;
                    DirectoryBox.IsEnabled = true;
                    DisconnectButton.IsEnabled = false;
                    StatusText.Text = "Файл успешно получен.";
                    Title = "Локальная безадаптерная сеть";
                    break;
                case MessageType.Disconnect:
                    timer.Stop();
                    sending = null;
                    CloseButton.IsEnabled = true;
                    FileBox.IsEnabled = true;
                    DirectoryBox.IsEnabled = true;
                    DisconnectButton.IsEnabled = false;
                    StatusText.Text = "Логическое соединение разорвано.";
                    Title = "Локальная безадаптерная сеть";
                    MessageBox.Show("Логическое соединение разорвано.");
                    break;
                default:
                    MessageBox.Show("Получен неизвестный пакет.");
                    break;
            }
        }

        private void ParseFileName(BinaryReader reader)
        {
            if (!folderReady)
            {
                SendPacket(new byte[] { (byte)MessageType.ReceiveNotReady });
                timer.Stop();
                MessageBox.Show("Необходимо выбрать папку для приёма файла.");
                return;
            }
            fileName = reader.ReadString();
            length = reader.ReadInt64();
            var hash = reader.ReadBytes(64);

            hashName = "";
            foreach (var b in hash)
            {
                hashName += b.ToString("x2");
            }

            tempPath = Path.Combine(folderDialog.SelectedPath, hashName);
            filePath = Path.Combine(folderDialog.SelectedPath, fileName);

            fileStream = File.OpenWrite(tempPath);
            fileStream.Seek(0, SeekOrigin.End);

            DisconnectButton.IsEnabled = true;
            CloseButton.IsEnabled = false;
            FileBox.IsEnabled = false;
            DirectoryBox.IsEnabled = false;
            StatusText.Text = "Логическое соединение установлено.";
            Title = "Принимаем " + fileName;
            sending = false;

            RequestFileChunk();
        }

        private void RequestFileChunk()
        {
            if (fileStream.Length < length)
            {
                var stream = new MemoryStream();
                var writer = new BinaryWriter(stream);

                writer.Write((byte)MessageType.FileRequest);
                writer.Write(fileStream.Length);
                SendPacket(stream.ToArray());
            }
            else
            {
                fileStream.Close();
                File.Delete(filePath);
                File.Move(tempPath, filePath);
                SendPacket(new byte[] { (byte)MessageType.FileReceived });
            }
        }

        private void SendFileChunk(BinaryReader reader)
        {
            if (sending != true)
            {
                MessageBox.Show("Получен повреждённый пакет.");
                return;
            }

            timer.Stop();

            DisconnectButton.IsEnabled = true;
            StatusText.Text = "Логическое соединение установлено.";

            var allLength = fileStream.Length;
            var position = reader.ReadInt64();
            ProgressBar.Value = (double)position / allLength;

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);

            writer.Write((byte)MessageType.FileChunk);

            var remaining = fileStream.Length - position;
            if (remaining < chunkSize)
            {
                writer.Write(position);
                writer.Write((short)remaining);
                var buffer = new byte[remaining];
                fileStream.Read(buffer, 0, (int)remaining);
                writer.Write(buffer);
            }
            else
            {
                writer.Write(position);
                writer.Write(chunkSize);
                var buffer = new byte[chunkSize];
                fileStream.Read(buffer, 0, chunkSize);
                writer.Write(buffer);
            }

            SendPacket(stream.ToArray());
        }

        private void SaveFileChunk(BinaryReader reader)
        {
            if (sending != false)
            {
                MessageBox.Show("Получен повреждённый пакет.");
                return;
            }

            timer.Stop();

            var position = reader.ReadInt64();
            var chunk = reader.ReadInt16();
            var buffer = reader.ReadBytes(chunk);

            fileStream.Seek(position, SeekOrigin.Begin);
            fileStream.Write(buffer, 0, chunk);
            
            ProgressBar.Value = (double)(position + chunk) / length;

            RequestFileChunk();
        }

        private void SendPacket(byte[] packet)
        {
            lastPacket = packet;
            retries = 0;
            DataLink.SendPacket(packet);
            timer.Start();
        }

        private void ResendPacket(object sender, EventArgs e)
        {
            if (retries++ < maxRetries)
            {
                DataLink.SendPacket(lastPacket);
                return;
            }

            if (lastPacket[0] == (byte)MessageType.FileReceivedOk ||
                lastPacket[0] == (byte)MessageType.FileReceived ||
                lastPacket[0] == (byte)MessageType.Disconnect)
            {
                timer.Stop();
                return;
            }

            SendPacket(new byte[] { (byte)MessageType.Disconnect });

            CloseButton.IsEnabled = true;
            FileBox.IsEnabled = true;
            DirectoryBox.IsEnabled = true;
            DisconnectButton.IsEnabled = true;
            if (fileStream != null)
            {
                fileStream.Close();
            }

            if (sending == true && fileStream.Position == 0)
            {
                StatusText.Text = "Физическое соединение открыто.";
                MessageBox.Show("Принимающая сторона не готова к логическому соединению.");
            }
            else
            {
                StatusText.Text = "Логическое соединение потеряно.";
                MessageBox.Show("Логическое соединение потеряно.");
            }
            sending = null;
        }

        private void PortCheck(bool DSR, bool CTS, bool DTR, bool RTS)
        {
            Dispatcher.Invoke(new Physical.PortListener(RealPortCheck), DispatcherPriority.Send, DSR, CTS, DTR, RTS);
        }

        private void RealPortCheck(bool DSR, bool CTS, bool DTR, bool RTS)
        {
            DtrIndicator.IsChecked = DTR;
            DsrIndicator.IsChecked = DSR;
            RtsIndicator.IsChecked = RTS;
            CtsIndicator.IsChecked = CTS;
        }

        private void ExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            MessageBox.Show(e.Exception.Message);
        }


        private enum MessageType:byte
        {
            FileName, ReceiveNotReady, FileRequest, FileChunk, FileReceived, FileReceivedOk, Disconnect
        }
    }
}
