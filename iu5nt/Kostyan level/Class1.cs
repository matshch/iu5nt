using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Collections;



namespace iu5nt.Kostyan_level
{
    public static class DataLink
    {
        static byte[] currentPacket;
        static byte[] checkSumm;
        static byte[] fullPacket;
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
            List<byte> indexPacket = new List<byte>(currentPacket);
            for (var i = 0; i < currentPacket.Length; i++ ){
                var taken = currentPacket[i];
                if(taken == 0xFF || taken == 0xFE){
                    indexPacket.Insert(i,0xFE);
                }
            }
            indexPacket.Add(0xFF);
            List<byte> indexSumm = new List<byte>(checkSumm);
            for (var i = 0; i < checkSumm.Length; i++ ){
                var taken = checkSumm[i];
                if(taken == 0xFF || taken == 0xFE){
                    indexSumm.Insert(i,0xFE);
                }
            }
            indexSumm.Add(0xFF);
            indexPacket.AddRange(indexSumm);
            BitArray bitPacket = new BitArray(indexPacket.ToArray());
            
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
            //TODO
        }

        private static byte[] BitArrayToByteArray(BitArray bits)
        {
            byte[] ret = new byte[(bits.Length - 1) / 8 + 1];
            bits.CopyTo(ret, 0);
            return ret;
        }
        private static BitArray getCycled(BitArray eleven){

        }
        private static void cycleCode(byte[] first, bool type)
        {
            byte[] second;
            BitArray buffer = new BitArray(first);
            if (type)
            {
                //Todo сделать чтобы работало для 2 байтов
                BitArray smallBuffer = new BitArray(0);
                smallBuffer.Length = 15;

                BitArray bigBuffer = new BitArray(0);
                bigBuffer.Length = 15;

                for (var i = 0; i < 11; i++)
                {
                    smallBuffer.Set(i, buffer[i]);
                }
                for (var i = 11; i < 16; i++)
                {
                    bigBuffer.Set(i - 5, buffer[i]);
                }
                var smallOst = BitConverter.ToInt32(BitArrayToByteArray(smallBuffer),0);
                var bigOst = BitConverter.ToInt32(BitArrayToByteArray(bigBuffer),0);
                int polynome = 19;
                while(smallOst > 15)
                {
                    var clonePoly = polynome;
                    polynome = polynome << (Convert.ToString(smallOst,2).Length -4);
                    smallOst = smallOst ^ polynome;
                }
                while (bigOst > 15)
                {
                    var clonePoly = polynome;
                    polynome = polynome << (Convert.ToString(bigOst, 2).Length - 4);
                    bigOst = bigOst ^ polynome;
                }
                second = BitConverter.GetBytes(smallOst).Skip(2).Concat(BitConverter.GetBytes(bigOst).Skip(2)).ToArray();

            }
            //return second;
        }

    }

 }