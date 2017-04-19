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
        static List<byte> recievedPacket;
        static bool firstTrigger = false;
        static bool secondTrigger = false;
        static int firstTPosition;
        public static void RecievePacket(byte[] recieved)
        {
            recievedPacket.AddRange(recieved);
            var packLen = recievedPacket.Count;
            if(packLen > 3)
            {
                for(var k = 2; k>0; k--)
                {
                    if (recievedPacket[packLen - k] == (byte)0xFF && recievedPacket[packLen - k - 1] == (byte)0xFE)
                    {
                        recievedPacket.RemoveAt(packLen - k - 1);
                    }
                    else
                    {
                        if (recievedPacket[packLen - k] == (byte)0xFF)
                        {
                            if (!firstTrigger)
                            {
                                firstTrigger = true;
                                firstTPosition = packLen - k - 1;
                            }
                            else
                            {
                                secondTrigger = true;
                            }
                        }
                        else
                        {
                            if (recievedPacket[packLen - k] == (byte)0xFE && recievedPacket[packLen - k - 1] == (byte)0xFE)
                            {
                                recievedPacket.RemoveAt(packLen - k - 1);
                            }
                        }

                    }
                }
                
            } else
            {
                if ((recievedPacket[packLen - 1] == (byte)0xFF && recievedPacket[packLen - 2] == (byte)0xFE) || (recievedPacket[packLen - 1] == (byte)0xFE && recievedPacket[packLen - 2] == (byte)0xFE))
                {
                    recievedPacket.RemoveAt(packLen - 2);
                }
            }
        }
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
                if(taken == (byte)0xFF || taken == (byte)0xFE){
                    indexPacket.Insert(i,(byte)0xFE);
                }
            }
            indexPacket.Add((byte)0xFF);
            List<byte> indexSumm = new List<byte>(checkSumm);
            for (var i = 0; i < checkSumm.Length; i++ ){
                var taken = checkSumm[i];
                if(taken == (byte)0xFF || taken == (byte)0xFE){
                    indexSumm.Insert(i,(byte)0xFE);
                }
            }
            indexSumm.Add((byte)0xFF);
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
            while(sp.BytesToRead > 0)
            {
            }
            //TODO
        }
        public static void Send(BitArray allBits)
        {
            var work = new bool[allBits.Count];
            allBits.CopyTo(work, 0);
            int iterate = (allBits.Count - 1) / 11 + 1;
            for (var i = 0; i < iterate; i++)
            {
                var array = work.Skip(i * 11).Take(11).ToArray();
                _serialPort.Write(getCycled(new BitArray(array)), 0, 2);
            }
        }
        private static byte[] BitArrayToByteArray(BitArray bits)
        {
            byte[] ret = new byte[(bits.Length - 1) / 8 + 1];
            bits.CopyTo(ret, 0);
            return ret;
        }
        private static byte[] getCycled(BitArray eleven) {
            int[] buffer = new int[1];
            eleven.CopyTo(buffer, 0);
            var qbite = buffer[0];
            qbite *= 16;
            var osn = 19;
            while (qbite > 15)
            {
                var clone = osn;
                clone = clone << ((int)Math.Log(qbite, 2));
                qbite ^= clone;
            }
            byte[] result = BitConverter.GetBytes(buffer[0] * 16 + qbite);
            return new byte[] { result[result.Length - 2], result[result.Length - 1] };

        }
       

    }

 }
