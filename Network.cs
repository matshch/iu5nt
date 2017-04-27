using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO.Ports;
using System.Timers;

namespace iu5nt
{
  public static class DataLink
  {
    static byte[] currentPacket;
    static byte[] checkSumm;
    static int length = 0;
    static List<byte> recievedPacket = new List<byte>();
    static List<bool> recievedPacketBuffer = new List<bool>();
    static List<bool> debugBuffer = new List<bool>();
    static bool firstTrigger = false;
    static bool secondTrigger = false;
    static bool screenTrigger = false;
    static int firstTPosition = 0;
    static Timer cleanerTimer = new Timer(1000);
    public delegate void RecieveMEthod(byte[] packet, bool check);
    public static event RecieveMEthod OnRecieve;

    static DataLink(){
      cleanerTimer.Elapsed += new ElapsedEventHandler(TimerListener);
    }
    static void TimerListener(object sender, ElapsedEventArgs e){
      if(firstTrigger){
        recievedPacket.Clear();
        recievedPacketBuffer.Clear();
        firstTrigger = false;
        secondTrigger = false;
        firstTPosition = 0;   
      }

    }
    public static void RecievePacket(BitArray recievedBit)
    {
      bool[] bbuffer = new bool[11];
      recievedBit.CopyTo(bbuffer, 0);
      recievedPacketBuffer.AddRange(bbuffer);
      debugBuffer.AddRange(bbuffer);
      while (recievedPacketBuffer.Count >= 8)
      {

        bool[] seriousBuffer = recievedPacketBuffer.GetRange(0, 8).ToArray();
        recievedPacketBuffer.RemoveRange(0, 8);
        var bitBufff = new BitArray(seriousBuffer);
        byte[] recieved = new byte[1];
        bitBufff.CopyTo(recieved, 0);
        if (firstTrigger || recieved[0] == 0xFF) {
          if(!firstTrigger){
            cleanerTimer.Start();
          }
          recievedPacket.AddRange(recieved);
          firstTrigger = true;
        }

        var packLen = recievedPacket.Count;
        if (packLen > 6)
        {
          for (var k = 1; k > 0 && packLen > 3; k--)
          {
            if (recievedPacket[packLen - k] == 0xFF &&
              recievedPacket[packLen - k - 1] == 0xFE && !screenTrigger)
            {
              recievedPacket.RemoveAt(packLen - k - 1);
              packLen--;
              screenTrigger = true;
            }
            else
            {
              if (recievedPacket[packLen - k] == 0xFF)
              {
                if (!secondTrigger)
                {
                  secondTrigger = true;
                  firstTPosition = packLen - k - 1;
                }
              }
              else
              {
                if (recievedPacket[packLen - k] == 0xFE &&
                  recievedPacket[packLen - k - 1] == 0xFE && !screenTrigger)
                {
                  recievedPacket.RemoveAt(packLen - k - 1);
                  packLen--;
                  screenTrigger = true;
                } else {
                  if (screenTrigger){
                    screenTrigger = false;
                  }
                }         
              }

            }
          }

        }
      }
      if (secondTrigger)
      {
        var exactPacket = recievedPacket.Skip(5)
                          .Take(firstTPosition - 4).ToArray();
        var buffer = 0;
        foreach (var packByte in exactPacket)
        {
          buffer += packByte;
        }
        //Тут может быть ошибка по длине для проверки суммы
        var checksummP = recievedPacket.Skip(1).Take(4).ToArray();
        if (buffer == BitConverter.ToUInt32(checksummP, 0))
        {
          OnRecieve(exactPacket, true);
        } else
        {
          OnRecieve(exactPacket, false);
        }
        recievedPacket.Clear();
        recievedPacketBuffer.Clear();
        firstTrigger = false;
        secondTrigger = false;
        firstTPosition = 0;
        cleanerTimer.Stop();
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
      currentPacket = newPacket;
      length += currentPacket.Length;
      var summBuffer = 0;
      foreach (var item in currentPacket)
      {
        summBuffer += item;
      }
      checkSumm = BitConverter.GetBytes(summBuffer);
      length += checkSumm.Length;
      var indexPacket = currentPacket
        .SelectMany(x => (x == 0xFE || x == 0xFF) ?
          new byte[] { 0xFE, x } :
          new byte[] { x })
        .ToList();
      indexPacket.Add(0xFF);
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
    public static List<UInt32> failList = Learning();
    public delegate void PortListener(bool DSR, bool CTS, bool DTR, bool RTS);
    public static event PortListener OnCheck;
    public static event PortListener UICheck;
    public static void SetRts(bool setter)
    {
      _serialPort.RtsEnable = setter;
      OnCheck(_serialPort.DsrHolding, _serialPort.CtsHolding,
              _serialPort.DtrEnable, _serialPort.RtsEnable);
    }
    static void StatusCheck (Object sender, SerialPinChangedEventArgs e) {
      UICheck(_serialPort.DsrHolding, _serialPort.CtsHolding,
              _serialPort.DtrEnable, _serialPort.RtsEnable);
    }
    public static void Connect(String portName)
    {
      if (connected)
      {
        Disconnect();
      }
      _serialPort = new SerialPort(portName)
      {
        BaudRate = 115200,
        DtrEnable = true,
        ReceivedBytesThreshold = 2
      };
      _serialPort.DataReceived +=
        new SerialDataReceivedEventHandler(DataReceivedHandler);
      _serialPort.PinChanged += new SerialPinChangedEventHandler(StatusCheck);
      _serialPort.Open();
      OnCheck(_serialPort.DsrHolding, _serialPort.CtsHolding,
              _serialPort.DtrEnable, _serialPort.RtsEnable);
      connected = true;
    }
    public static void Disconnect()
    {
      _serialPort.Close();
      connected = false;
    }
    private static void DataReceivedHandler(object sender,
      SerialDataReceivedEventArgs e)
    {
      var i = 0;
      while(_serialPort.BytesToRead > 1)
      {
        i++;
        var byteBuffer = new byte[4];
        _serialPort.Read(byteBuffer,0,2);
        var dec = DeCycle(byteBuffer);
        DataLink.RecievePacket(dec);
      }
    }
    static public List<UInt32> Learning()
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
    static BitArray DeCycle(byte[] cycled)
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
        var bitar = new BitArray(convert)
        {
          Length = 11
        };
        return bitar;
      }
      else
      {
        var indi = failList.IndexOf(qbite);
        if(indi > 3)
        {
          var convert = BitConverter.GetBytes(buffer / 16);
          var bitar = new BitArray(convert)
          {
            Length = 11
          };
          bitar.Set(indi - 4, !bitar.Get(indi - 4));
          return bitar;
        } else
        {
          var convert = BitConverter.GetBytes(buffer / 16);
          var bitar = new BitArray(convert)
          {
            Length = 11
          };
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
        if(_serialPort.CtsHolding && _serialPort.DsrHolding){
          _serialPort.Write(GetCycled(new BitArray(array)), 0, 2);
        } else {
          OnCheck(_serialPort.DsrHolding, _serialPort.CtsHolding,
                  _serialPort.DtrEnable, _serialPort.RtsEnable);
          return;
        }
      }
    }
    private static byte[] BitArrayToByteArray(BitArray bits)
    {
      byte[] ret = new byte[(bits.Length - 1) / 8 + 1];
      bits.CopyTo(ret, 0);
      return ret;
    }
    private static byte[] GetCycled(BitArray eleven) {
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
