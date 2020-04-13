using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimBitClient
{
    class PacketGenerator
    {
        private List<byte> PacketBytes;
        private int PacketIndex = 0;

        public PacketGenerator()
        {
            PacketBytes = new List<byte>();
        }

        public PacketGenerator(byte[] byteArray)
        {
            PacketBytes = new List<byte>(byteArray);
        }

        public static byte[] Handshake(string clientName, byte[] info_hash, string peer_id)
        {
            PacketGenerator rs = new PacketGenerator();

            byte[] reservedBytes = { 0, 0, 0, 0, 0, 0, 0, 0 };

            rs.WriteByte(Convert.ToByte(clientName.Length));
            rs.WriteString(clientName);
            rs.WriteByteArray(reservedBytes);
            rs.WriteByteArray(info_hash);
            rs.WriteString(peer_id);

            return rs.GetByteArray();
        }

        public static byte[] KeepAlive()
        {
            byte[] keepAlive = { 0, 0, 0, 0 };
            return keepAlive;
        }

        public static byte[] Choke()
        {
            PacketGenerator rs = new PacketGenerator();
            rs.WriteInt(1); //Length
            rs.WriteByte(0); //ID
            return rs.GetByteArray();
        }

        public static byte[] Unchoke()
        {
            PacketGenerator rs = new PacketGenerator();
            rs.WriteInt(1); //Length
            rs.WriteByte(1); //ID
            return rs.GetByteArray();
        }

        public static byte[] Interested()
        {
            PacketGenerator rs = new PacketGenerator();
            rs.WriteInt(1); //Length
            rs.WriteByte(2);//ID
            return rs.GetByteArray();
        }

        public static byte[] NotInterested()
        {
            PacketGenerator rs = new PacketGenerator();
            rs.WriteInt(1); //Length
            rs.WriteByte(3); //ID
            return rs.GetByteArray();
        }

        public static byte[] Have(int piece)
        {
            PacketGenerator rs = new PacketGenerator();
            rs.WriteInt(5); //Length
            rs.WriteByte(4); //ID
            rs.WriteInt(piece); //Payload: Piece ID
            return rs.GetByteArray();
        }

        public static byte[] BitField(byte[] bitfield)
        {
            PacketGenerator rs = new PacketGenerator();
            rs.WriteInt(1 + bitfield.Length); //Length
            rs.WriteByte(5); //ID
            rs.WriteByteArray(bitfield); //Payload: Bitfield
            return rs.GetByteArray();
        }

        public static byte[] Request(int pieceid, int piece_begin_offset, int piecelength) //WARNING: USE 16 KILOBYTES (2^14) AT A TIME, DOWNLOAD IN MULTIPLE REQUESTS
        {
            PacketGenerator rs = new PacketGenerator();
            rs.WriteInt(13); //Length
            rs.WriteByte(6); //ID
            rs.WriteInt(pieceid); //Payload: Piece ID [index]
            rs.WriteInt(piece_begin_offset); //Payload: Byte Offset [begin]
            rs.WriteInt(piecelength); //Payload: Piece Read Length [length]

            return rs.GetByteArray();
        }

        public static byte[] Piece(int pieceid, int piece_begin_offset, byte[] piece_part) //WARNING: USE REQUESTED AMOUNT OF BYTES (MAX 16KB) AT A TIME, SEND IN MULTIPLE REQUESTS
        {
            PacketGenerator rs = new PacketGenerator();
            rs.WriteInt(9 + piece_part.Length); //Length
            rs.WriteByte(7); //ID
            rs.WriteInt(pieceid); //Payload: Piece ID [index]
            rs.WriteInt(piece_begin_offset); //Payload: Byte Offset [begin]
            rs.WriteByteArray(piece_part); //Payload: Piece Block [block]

            return rs.GetByteArray();
        }

        public static byte[] Cancel(int pieceid, int piece_begin_offset, int piecelength) //Same parameters as 'Request' message
        {
            PacketGenerator rs = new PacketGenerator();
            rs.WriteInt(13); //Length
            rs.WriteByte(8); //ID
            rs.WriteInt(pieceid); //Payload: Piece ID [index]
            rs.WriteInt(piece_begin_offset); //Payload: Byte Offset [begin]
            rs.WriteInt(piecelength); //Payload: Piece Read Length [length]

            return rs.GetByteArray();
        }

        public static byte[] Port(short listen_port) //Used for DHT, currently unimplemented
        {
            PacketGenerator rs = new PacketGenerator();
            rs.WriteInt(3); //Length
            rs.WriteByte(9); //ID
            rs.WriteShort(listen_port);

            return rs.GetByteArray();
        }

        public void WriteInt(int i)
        {
            byte[] intBytes = BitConverter.GetBytes(i);
            if (BitConverter.IsLittleEndian) //We need big endian
                Array.Reverse(intBytes);
            PacketBytes.AddRange(intBytes);
        }

        public void WriteShort(short i)
        {
            byte[] intBytes = BitConverter.GetBytes(i);
            if (BitConverter.IsLittleEndian) //We need big endian
                Array.Reverse(intBytes);
            PacketBytes.AddRange(intBytes);
        }

        public void WriteString(string s)
        {
            byte[] str = Encoding.UTF8.GetBytes(s);
            WriteByteArray(str);
        }

        public void WriteByteArray(byte[] byteArray)
        {
            PacketBytes.AddRange(byteArray);
        }

        public void WriteByte(byte b)
        {
            PacketBytes.Add(b);
        }

        public byte ReadByte()
        {
            return PacketBytes[PacketIndex++];
        }

        public short ReadShort()
        {
            byte[] intBytes = PacketBytes.GetRange(PacketIndex, 2).ToArray();
            PacketIndex += 2;

            if (BitConverter.IsLittleEndian) //We need big endian
                Array.Reverse(intBytes);

           return BitConverter.ToInt16(intBytes, 0);
        }

        public int ReadInt()
        {
            byte[] intBytes = PacketBytes.GetRange(PacketIndex, 4).ToArray();
            PacketIndex += 4;

            if (BitConverter.IsLittleEndian) //We need big endian
                Array.Reverse(intBytes);

            return BitConverter.ToInt32(intBytes, 0);
        }

        public string ReadString(int length)
        {
            byte[] retBytes = PacketBytes.GetRange(PacketIndex, length).ToArray();
            PacketIndex += length;
            return Encoding.UTF8.GetString(retBytes, 0, length);
        }

        public byte[] ReadByteArray(int length)
        {
            byte[] retBytes = PacketBytes.GetRange(PacketIndex, length).ToArray();
            PacketIndex += length;
            return retBytes;
        }

        public byte[] ReadLeftoverByteArray()
        {
            byte[] retBytes = PacketBytes.GetRange(PacketIndex, PacketBytes.Count-PacketIndex).ToArray();
            return retBytes;
        }

        public void SeekIndex(int length)
        {
            PacketIndex += length;
        }

        public int GetIndex()
        {
            return PacketIndex;
        }

        public void SetIndex(int index)
        {
            PacketIndex = index;
        }

        public int GetLength()
        {
            return PacketBytes.Count;
        }

        public byte[] GetByteArray()
        {
            return PacketBytes.ToArray();
        }
    }
}
