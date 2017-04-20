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
        static List<bool> debugBuffer = new List<bool>();
        static bool firstTrigger = false;
        static bool secondTrigger = false;
        static int firstTPosition = 0;
        static int counter = 4;
        public delegate void RecieveMEthod(byte[] packet, bool check);
        public static event RecieveMEthod onRecieve;
        public static void RecievePacket(BitArray recievedBit)
        {
            bool[] bbuffer = new bool[11];
            recievedBit.CopyTo(bbuffer, 0);
            recievedPacketBuffer.AddRange(bbuffer);
            debugBuffer.AddRange(bbuffer);
            while (recievedPacketBuffer.Count > 8)
            {
                
                bool[] seriousBuffer = recievedPacketBuffer.GetRange(0,8).ToArray();
                recievedPacketBuffer.RemoveRange(0, 8);
                var bitBufff = new BitArray(seriousBuffer);
                byte[] recieved = new byte[1];
                bitBufff.CopyTo(recieved, 0);
                if(firstTrigger || recieved[0] == 0xFF){
                    recievedPacket.AddRange(recieved);
                    firstTrigger = true;
                }

                var packLen = recievedPacket.Count;
                if(packLen > 6)
                {
                    for(var k = 1; k > 0 && packLen > 3; k--)
                    {
                        if (recievedPacket[packLen - k] == (byte)0xFF && recievedPacket[packLen - k - 1] == (byte)0xFE)
                        {
                            recievedPacket.RemoveAt(packLen - k - 1);
                            packLen--;
                        }
                        else
                        {
                            if (recievedPacket[packLen - k] == (byte)0xFF)
                            {
                                if (!secondTrigger)
                                {
                                    secondTrigger = true;
                                    firstTPosition = packLen - k - 1;
                                }
                            }
                            else
                            {
                                if (recievedPacket[packLen - k] == (byte)0xFE && recievedPacket[packLen - k - 1] == (byte)0xFE)
                                {
                                    recievedPacket.RemoveAt(packLen - k - 1);
                                    packLen--;
                                }
                            }

                        }
                    }
                
                }
            }
            if (secondTrigger)
            {
                var exactPacket = recievedPacket.Skip(5).Take(firstTPosition - 4).ToArray();
                var buffer = 0;
                foreach (var packByte in exactPacket)
                {
                    buffer += packByte;
                }
                //Тут может быть ошибка по длине для проверки суммы
                var checksummP = recievedPacket.Skip(1).Take(4).ToArray();
                if (buffer == BitConverter.ToUInt16(checksummP, 0))
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
            indexSumm.AddRange(indexPacket);
            indexSumm.Insert(0,0xFF);
            BitArray bitPacket = new BitArray(indexSumm.ToArray());
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
            var i = 0;
            while(_serialPort.BytesToRead > 1)
            {
                i++;
                var byteBuffer = new byte[4];
                _serialPort.Read(byteBuffer,0,2);
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
                clone = clone << ((int)Math.Log(qbite, 2) - 4);
                qbite ^= clone;
            }
            study = study + qbite;
            var getBuffed = new List<UInt32>();
            for (var i = 0; i < 15; i++)
            {
                var bits = BitConverter.GetBytes(study);
                var beef = new BitArray(new byte[] { bits[0],bits[1]});
                beef.Set(i, !beef.Get(i));
                var number = new Int32[1];
                beef.CopyTo(number, 0);
                qbite = (uint)number[0];
                while (qbite > 15)
                {
                    var clone = osn;
                    clone = clone << ((int)Math.Log(qbite, 2) - 4);
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
                clone = clone << ((int)Math.Log(qbite, 2) - 4);
                qbite ^= clone;
            }
            if (qbite == 0)
            {
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
            var buffer = new Int32[1];
            eleven.CopyTo(buffer, 0);
            UInt32 qbite = (uint)buffer[0];
            qbite *= 16;
            UInt32 osn = 19;
            while (qbite > 15)
            {
                UInt32 clone = osn;
                clone = clone << ((int)Math.Log(qbite, 2) - 4);
                qbite ^= clone;
            }
            byte[] result = BitConverter.GetBytes(buffer[0] * 16 + qbite);
            return new byte[] { result[0], result[1] };

        }
       
    }

 }
