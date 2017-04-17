using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace iu5nt.Kostyan_level
{
    public static class DataLink
    {
        static byte[] currentPacket;
        static byte[] checkSumm;
        static int length = 0;
        static int position = 0;
        public static void SendPacket(byte[] newPacket)
        {
            length = 0;
            position = 0;
            currentPacket = newPacket;
            length += currentPacket.Length;
            var summBuffer = 0;
            foreach(var item in currentPacket)
            {
                summBuffer += item;
            }
            checkSumm = BitConverter.GetBytes(summBuffer);
            length += checkSumm.Length;

        }

    }
    public static class Physical
    {
        static SerialPort _serialPort;
        public static bool connected = true;
        public static void Connect(String portName)
        {
            if (connected)
            {
                Disconnect();
            }
            _serialPort = new SerialPort(portName);
            _serialPort.DtrEnable = true;
            _serialPort.RtsEnable = true;
            _serialPort.ReceivedBytesThreshold = 2;
            _serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
            _serialPort.Open();
            connected = true;
        }
        public static void Disconnect()
        {
            _serialPort.Close();
            connected = false;
        }
        private static void DataReceivedHandler(
                        object sender,
                        SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
        }


    }
}
