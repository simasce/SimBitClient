using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace SimBitClient
{
    struct WriteData
    {
        public byte[] Bytes;
        public int offset;
        public long pieceid;
    }

    class BitFile
    {
        private const long MB_1 = 1073741824; // 1 Megabyte

        private bool FileOpened = false;

        private string FileName;
        private string Hash;
        private long FileSize;
        private long Pieces;
        private long PieceSize;       
        private BitField ReceivedPieces;
        private int OneDownloadSize = 1024*16;

        volatile Queue<WriteData> WriteQueue = new Queue<WriteData>();
        volatile FileStream CurrentFileStream;

        private long CurrentWriteStart = 0;
        private Thread WriteThread;

        public BitFile(string filename, string hash, long filesize, long pieces, long piecesize)
        {
            FileName = filename;
            Hash = hash;
            FileSize = filesize;
            Pieces = pieces;
            PieceSize = piecesize;
            ReceivedPieces = new BitField(pieces);

            if ((long)PieceSize < OneDownloadSize)
                OneDownloadSize = (int)PieceSize;

            bool skipPrealloc = false;
            if(File.Exists(filename))
            {
                long curSize = new System.IO.FileInfo(filename).Length;

                if (curSize == filesize)
                    skipPrealloc = true;
                else
                    File.Delete(filename);
            }

            CurrentFileStream = new FileStream(FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            FileOpened = true;

            if (!skipPrealloc)
                Preallocate();

            WriteThread = new Thread(WriteThreadFunction);
            WriteThread.Start();
        }

        ~BitFile()
        {
            CloseFile();
        }

        private void WriteThreadFunction()
        {
            while (!Finished())
            {
                while(WriteQueue.Count != 0)
                {
                    WriteData ds = WriteQueue.Dequeue();

                    SeekPiece(ds.pieceid, ds.offset);
                    CurrentFileStream.Write(ds.Bytes, 0, ds.Bytes.Length);
                }               
                Thread.Sleep(5);
            }
            CurrentFileStream.Flush();
            CurrentFileStream.Close();
            FileOpened = false;
        }

        public bool ReadPiece(long pieceid, ref byte[] piece, int offset = 0, int length=-1)
        {
            if(!HavePiece(pieceid) || !FileOpened)
                return false;

            CurrentFileStream.Seek(offset + (pieceid * PieceSize), SeekOrigin.Begin);

            byte[] retPiece = new byte[(length == -1) ? OneDownloadSize : length];
            CurrentFileStream.Read(retPiece, 0, (length == -1) ? (int)OneDownloadSize : length);

            piece = retPiece;

            return true;
        }

        public void WritePiece(long pieceid, int offset, byte[] piece)
        {
            if (!FileOpened)
                return;

            WriteData data;
            data.Bytes = piece;
            data.offset = offset;
            data.pieceid = pieceid;

            WriteQueue.Enqueue(data);

            CurrentWriteStart = (pieceid * PieceSize) + offset + piece.Length;
        }

        public void Preallocate()
        {
            Console.WriteLine("Preallocating " + FileName);
            CurrentFileStream.Seek(0, SeekOrigin.Begin);

            long remainingSize = FileSize;
            byte[] allocArray = new byte[MB_1];
            while(remainingSize > 0)
            {
                CurrentFileStream.Write(allocArray, 0, (remainingSize > (MB_1)) ? allocArray.Length : (int)remainingSize);
                remainingSize -= (MB_1);
            }

            CurrentFileStream.Flush();
            CurrentFileStream.Close();
            CurrentFileStream = new FileStream(FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            Console.WriteLine("Preallocating done!");
        }

        private void SeekPiece(long piece, int offset)
        {
            CurrentFileStream.Seek((piece*PieceSize) + offset, SeekOrigin.Begin);
        }

        public bool HavePiece(long piece)
        {
            return ReceivedPieces.GetPiece(piece);
        }

        public bool GetNextMissingPiece(ref int piece, ref int offset, ref int length)
        {
            if (AlmostFinished())
                return false;

            long currentStream = CurrentWriteStart;
            int currentPiece = (int)Math.Floor((double)currentStream / (double)PieceSize);
         
            long leftover = (currentPiece + 1) * PieceSize - currentStream;

            if (FileSize - CurrentWriteStart < PieceSize)
                leftover = FileSize - CurrentWriteStart;

            long offsett = currentStream - (currentPiece * PieceSize);

            if (leftover >= OneDownloadSize)
                length = OneDownloadSize;
            else
                length = (int)leftover;

            offset = (int)offsett;
            piece = currentPiece;

            if (offset == 0 && currentPiece > 0)
            {
                ReceivedPieces.SetPiece(currentPiece - 1, true);
                Console.WriteLine("Set piece {0} as downloaded!", currentPiece - 1);
            }
               
            return ReceivedPieces.GetNextMissingPiece() != -1;
        }

        public bool Finished()
        {
            if (!FileOpened)
                return true;

            return CurrentFileStream.Position >= FileSize;
        }

        public bool AlmostFinished()
        {
            if (!FileOpened)
                return true;

            return CurrentWriteStart >= FileSize;
        }

        public BitField GetBitField()
        {
            return ReceivedPieces;
        }

        public long GetPieceSize()
        {
            return PieceSize;
        }

        public long GetDownloadedSize()
        {
            return CurrentFileStream.Position;
        }

        public long GetSize()
        {
            return FileSize;
        }

        public void CloseFile()
        {
            if (!FileOpened)
                return;

            try
            {
                CurrentFileStream.Flush();
                CurrentFileStream.Close();
                FileOpened = false;
            }
            catch (Exception e) { }
        }       
    }
}
