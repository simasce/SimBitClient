using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimBitClient
{
    class BitField
    {
        private byte[] FieldBytes;
        private int LastPieceBlock = 0;

        public BitField(long Pieces)
        {
            FieldBytes = new byte[(long)Math.Ceiling((double)Pieces / 8)];
        }

        public BitField(byte[] Bytes)
        {
            FieldBytes = Bytes;
        }
            
        public byte[] GetBytes()
        {
            return FieldBytes;
        }

        public BitField Compare(BitField input) //returns 1 only where we are interested in
        {
            if (FieldBytes.Length != input.GetBytes().Length)
                throw new ArgumentException("BitField should be the same length for comparison!");


            byte[] inputBytes = input.GetBytes();
            byte[] comparedBytes = new byte[FieldBytes.Length];

            for(int i = 0; i < FieldBytes.Length; i++)
            {
                comparedBytes[i] = (byte)((~FieldBytes[i]) & inputBytes[i]);
            }

            return new BitField(comparedBytes);
        }

        public bool GetPiece(long index)
        {
            int arrIndex = (int)Math.Floor((double)index / 8);
            byte bitIndex = (byte)(index % 8);

            byte curByte = FieldBytes[arrIndex];
            return ((curByte & ((byte)0x80 >> bitIndex)) > 0);
        }

        public void SetPiece(long index, bool got)
        {
            int arrIndex = (int)Math.Floor((double)index / 8);
            byte bitIndex = (byte)(index % 8);

            if (got)
                FieldBytes[arrIndex] |= (byte)((byte)0x80 >> bitIndex);
            else
                FieldBytes[arrIndex] |= (byte)(~((byte)0x80 >> bitIndex));
        }

        public long GetNextMissingPiece()
        {
            for(int i = LastPieceBlock; i < FieldBytes.Length; i++)
            {
                byte curByte = FieldBytes[i];
                for(byte a = 0; a < 8; a++)
                {
                    byte pieceStatus = (byte)(curByte & (0x80 >> a));
                    if (pieceStatus == 0)
                    {
                        LastPieceBlock = i;
                        return (i * 8) + a;
                    }
                }
            }
            return -1;
        }

        public long GetNextPresentPiece()
        {
            for (int i = LastPieceBlock; i < FieldBytes.Length; i++)
            {
                byte curByte = FieldBytes[i];
                for (byte a = 0; a < 8; a++)
                {
                    byte pieceStatus = (byte)(curByte & (0x80 >> a));
                    if (pieceStatus != 0)
                    {
                        LastPieceBlock = i;
                        return (i * 8) + a;
                    }
                      
                }
            }
            return -1;
        }

        public bool IsFull()
        {
            for(int i = 0; i < FieldBytes.Length; i++)
            {
                if(i != FieldBytes.Length - 1)
                {
                    if (FieldBytes[i] != 0xFF)
                        return false;
                }
            }
            return true;
        }
    }
}
