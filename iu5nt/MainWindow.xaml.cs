using iu5nt.Kostyan_level;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO.Ports;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace iu5nt
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private OpenFileDialog fileDialog = new OpenFileDialog();

        public MainWindow()
        {
            InitializeComponent();
            PortsList.ItemsSource = SerialPort.GetPortNames();
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
                try
                {
                    Physical.Connect((string)port);
                    OpenButton.IsEnabled = false;
                    CloseButton.IsEnabled = true;
                    PortsList.IsEnabled = false;
                    FileBox.IsEnabled = true;
                    DirectoryBox.IsEnabled = true;
                    StatusText.Text = "Физическое соединение открыто.";
                }
                catch (Exception er)
                {
                    MessageBox.Show(er.Message);
                }
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
            if (result == true)
            {
                FileName.Text = fileDialog.FileName;
                SendFile.IsEnabled = true;
            }
        }
    }
}
