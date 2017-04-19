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
        static List<byte> recievedPacket = new List<byte>();
        static List<bool> recievedPacketBuffer = new List<bool>();
        static bool firstTrigger = false;
        static bool secondTrigger = false;
        static int firstTPosition = 0;
        public delegate void RecieveMEthod(byte[] packet, bool check);
        public static event RecieveMEthod onRecieve;
        public static void RecievePacket(BitArray recievedBit)
        {
            bool[] bbuffer = new bool[11];
            recievedBit.CopyTo(bbuffer, 0);
            recievedPacketBuffer.AddRange(bbuffer);
            if(recievedPacketBuffer.Count > 8)
            {
                bool[] seriousBuffer = recievedPacketBuffer.GetRange(0,8).ToArray();
                recievedPacketBuffer.RemoveRange(0, 8);
                var bitBufff = new BitArray(seriousBuffer);
                byte[] recieved = new byte[1];
                bitBufff.CopyTo(recieved, 0);
                recievedPacket.AddRange(recieved);
            }

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
                if (packLen > 2 && ((recievedPacket[packLen - 1] == (byte)0xFF && recievedPacket[packLen - 2] == (byte)0xFE) || (recievedPacket[packLen - 1] == (byte)0xFE && recievedPacket[packLen - 2] == (byte)0xFE)))
                {
                    recievedPacket.RemoveAt(packLen - 2);
                }
            }
            if (secondTrigger)
            {
                var exactPacket = recievedPacket.Take(firstTPosition + 1).ToArray();
                var buffer = 0;
                foreach (var packByte in exactPacket)
                {
                    buffer += packByte;
                }
                //Тут может быть ошибка по длине для проверки суммы
                var checksummP = recievedPacket.Skip(firstTPosition + 2).Take(recievedPacket.Count - firstTPosition - 3).ToArray();
                if (buffer == BitConverter.ToInt16(checksummP, 0))
                {
                    onRecieve(exactPacket, true);
                } else
                {
                    onRecieve(exactPacket, false);
                }
                recievedPacket.Clear();
                recievedPacketBuffer.Clear();      
                firstTrigger = false;
                secondTrigger = false;
                firstTPosition = 0;
            }
        }
        public static void SendPacket(byte[] newPacket)
        {
            recievedPacket.Clear();
            firstTrigger = false;
            secondTrigger = false;
            firstTPosition = 0;
            recievedPacketBuffer.Clear();
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
            Physical.Send(bitPacket);
        }

    }


    public static class Physical
    {
        static SerialPort _serialPort;
        public static bool connected = false;
        public static List<UInt32> failList = learning();
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
            while(sp.BytesToRead > 1)
            {
                var byteBuffer = new byte[2];
                sp.Read(byteBuffer,0,2);
                var dec = deCycle(byteBuffer);
                DataLink.RecievePacket(dec);
            }
            //TODO
        }
        static public List<UInt32> learning()
        {
            UInt32 study = 16384;
            UInt32 qbite = study;
            UInt32 osn = 19;
            while (qbite > 15)
            {
                UInt32 clone = osn;
                clone = clone << ((int)Math.Log(qbite, 2));
                qbite ^= clone;
            }
            study = study + qbite;
            var getBuffed = new List<UInt32>();
            for (var i = 0; i < 15; i++)
            {
                var bits = BitConverter.GetBytes(study);
                var beef = new BitArray(new byte[] { bits[0],bits[1]});
                beef.Set(i, !beef.Get(i));
                var number = new UInt32[1];
                beef.CopyTo(number, 0);
                qbite = number[0];
                while (qbite > 15)
                {
                    var clone = osn;
                    clone = clone << ((int)Math.Log(qbite, 2));
                    qbite ^= clone;
                }
                getBuffed.Add(qbite);
            }
            return getBuffed;
        }
        static BitArray deCycle(byte[] cycled)
        {

            var buffer = BitConverter.ToUInt32(cycled,0);
            UInt32 qbite = buffer;
            UInt32 osn = 19;
            while (qbite > 15)
            {
                UInt32 clone = osn;
                clone = clone << ((int)Math.Log(qbite, 2));
                qbite ^= clone;
            }
            if (qbite == 0)
            {
                var len = (int)Math.Log(buffer / 16, 2);
                var convert = BitConverter.GetBytes(buffer / 16);
                var bitar = new BitArray(convert);
                bitar.Length = 11;
                return bitar;
            }
            else
            {
                var indi = failList.IndexOf(qbite);
                if(indi > 3)
                {
                    var convert = BitConverter.GetBytes(buffer / 16);
                    var bitar = new BitArray(convert);
                    bitar.Length = 11;
                    bitar.Set(indi - 4, !bitar.Get(indi - 4));
                    return bitar;
                } else
                {
                    var convert = BitConverter.GetBytes(buffer / 16);
                    var bitar = new BitArray(convert);
                    bitar.Length = 11;
                    return bitar;
                }

            }
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
            UInt32[] buffer = new UInt32[1];
            eleven.CopyTo(buffer, 0);
            UInt32 qbite = buffer[0];
            qbite *= 16;
            UInt32 osn = 19;
            while (qbite > 15)
            {
                UInt32 clone = osn;
                clone = clone << ((int)Math.Log(qbite, 2));
                qbite ^= clone;
            }
            byte[] result = BitConverter.GetBytes(buffer[0] * 16 + qbite);
            return new byte[] { result[0], result[1] };

        }
       
    }

 }
