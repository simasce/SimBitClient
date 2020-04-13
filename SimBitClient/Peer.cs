using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace SimBitClient
{
    enum PeerState
    {
        SHAKING = 0,
        READY = 1,
        CHOKED = 2,
        DOWNLOADING = 3
    }

    class Peer
    {
        private string IP;
        private int Port;
        private byte[] Infohash;
        private string PeerClientID;
        private BitFile CurrentFile;
        

        public Thread _ReadThread;
    
        volatile BitField DifferenceBitField;
        volatile TimeManager ChokeTimer = new TimeManager();
        volatile TimeManager AliveTimer = new TimeManager();
        volatile bool ThreadWorking = true;
        volatile PeerState State = PeerState.SHAKING;
        volatile NetworkStream Stream;
        volatile TcpClient Client;
        volatile byte[] LastPacketSent = null;
        
        public Peer(string ip, int port, string infohash, string peerClientID, BitFile fl)
        {
            IP = ip;
            Port = port;
#if DEBUG
            Console.WriteLine("Current InfoHash: {0}", infohash);
#endif
            Infohash = HexStringToByteArray(infohash);
            PeerClientID = peerClientID;
            CurrentFile = fl;
        }

        public void ReadThread()
        {
            Stream.ReadTimeout = 40000;// 40s timeout
            try
            {
                while (ThreadWorking)
                {
                    Send(PacketGenerator.Interested());
                    Send(PacketGenerator.Unchoke());

                    if (State == PeerState.CHOKED)
                    {
                        if(ChokeTimer.GetElapsedSeconds() >= 30.0)
                        {
                            ThreadWorking = false;
                            break;
                        }
                    }

                    byte packetID = 0;
                    byte[] received = ReadPacket(ref packetID);

                    if(packetID  <= 9)
                    {
                        AliveTimer.Reset();
                    }

#if DEBUG
                    Console.WriteLine("Received packet: {0}", packetID == 0xF0 ? "Keep-Alive" : Convert.ToString(packetID));
#endif
                    switch (packetID)
                    {
                        case 0xF0: //KEEP_ALIVE     
                            //AliveTimer.Reset();
                            if(State == PeerState.READY)
                            {
                                if (RequestForNewPiece() == -1)
                                {
                                    //all pieces downloaded or failed
                                    State = PeerState.SHAKING;
                                    ThreadWorking = false;
                                }
                            }
                            //SendPacket(null, 0);
                            break;
                        case 0: //CHOKE
                            ChokeTimer.Reset();
                            State = PeerState.CHOKED;
                            break;
                        case 1://UNCHOKE
                            if(State == PeerState.CHOKED)
                            {
                               // Send(PacketGenerator.Interested());
                                //Console.WriteLine("Sent Interested Packet!");
                                if (RequestForNewPiece() == -1)
                                {
                                    //all pieces downloaded or failed
                                    State = PeerState.SHAKING;
                                    ThreadWorking = false;
                                }
                                Send(PacketGenerator.Unchoke());
                            }
                            if (State == PeerState.CHOKED)
                                State = PeerState.READY;                                                   
                            break;
                        case 2: //INTERESTED
                            State = PeerState.READY;
                            if (RequestForNewPiece() == -1)
                            {
                                //all pieces downloaded or failed
                                State = PeerState.SHAKING;
                                ThreadWorking = false;
                            }
                            break;
                        case 3: //NOTINTERESTED
                            ThreadWorking = false;
#if DEBUG
                            Console.WriteLine("Killing thread.. Not interested.");
#endif
                            break;
                        case 4: //HAVE
                            Have(received); // OK
                            break;
                        case 5://BITFIELD
                            BitField(received);                      
                            break;
                        case 6: //REQUEST
                            FetchAndSendPiece(received);
                            break;
                        case 7: //PIECE
                            State = PeerState.READY;
                            if (SavePieceAndRequestForNewOne(received) == -1)
                            {
                                //all pieces downloaded or failed
                                State = PeerState.SHAKING;
                                ThreadWorking = false;
                            }                          
                            break;
                        case 8: //CANCEL
                            //No need
                            break;
                        case 9: //PORT
                            //Unused
                            break;
                    }
                }
            }
            catch(Exception e)
            {
                ThreadWorking = false;
#if DEBUG
                Console.WriteLine("ThreadException: {0}", e.Message);
#endif
            }
        }

        public bool BeginConnection()
        {
            Client = new TcpClient();
            Client.ReceiveTimeout = 1000;

            var result = Client.BeginConnect(this.IP, this.Port, null, null);

            Console.WriteLine("Connecting to: {0}:{1}", this.IP, this.Port);

            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
            
            if (!success || !Client.Connected)        
                return false;

            Stream = Client.GetStream();
            Stream.ReadTimeout = 6000;

            byte[] handshake = PacketGenerator.Handshake("BitTorrent protocol", Infohash, PeerClientID);
            Stream.Write(handshake, 0 ,handshake.Length);
            Stream.Flush();

#if DEBUG
            Console.WriteLine("Sent packet!");
#endif
            byte[] realRead = Read();

            if (realRead.Length < 49)
            {
#if DEBUG
                Console.WriteLine("Invalid handshake packet! Exiting!");
#endif
                Stream.Close();
                Client.Close();
                return false;
            }

#if DEBUG
            Console.WriteLine("Received: {0} bytes", realRead.Length);
#endif
            PacketGenerator gs = new PacketGenerator(realRead);

            if (!Handshake(gs))
            {
#if DEBUG
                Console.WriteLine("Handshake failed!");
#endif
                Stream.Close();
                Client.Close();
                return false;
            }
               
            Console.WriteLine("Connected!");

            State = PeerState.CHOKED;
            ChokeTimer.Reset();

            _ReadThread = new Thread(ReadThread);
            _ReadThread.Start();

            TimeManager resentTimer = new TimeManager();
            while (ThreadWorking)
            {
                if(AliveTimer.GetElapsedSeconds() > 20.0 && resentTimer.GetElapsedSeconds() > 10.0)
                {
                    resentTimer.Reset();
                    if (LastPacketSent == null)
                        Send(PacketGenerator.KeepAlive());
                    else
                        Send(LastPacketSent);
                }

                if (AliveTimer.GetElapsedSeconds() > 60.0)
                    ThreadWorking = false;

                Thread.Sleep(100);
            }

            ThreadWorking = false;
            return true;
        }

        public bool Handshake(PacketGenerator gs)
        {
            int nameLength = (int)gs.ReadByte();
            if (gs.GetLength() < 49 + nameLength)
            {
#if DEBUG
                Console.WriteLine("Invalid namelength! Corrupted packet?");
#endif
                return false;
            }

            string name = gs.ReadString(nameLength);
            gs.SeekIndex(8); //reservedBytes
            byte[] infoHash = gs.ReadByteArray(20);
            string peerID = gs.ReadString(20);

            bool HanshakeSuccess = infoHash.SequenceEqual(this.Infohash);

            return HanshakeSuccess;
        }

        public void BitField(byte[] packet)
        {
            BitField received = new BitField(packet);
#if DEBUG
            Console.WriteLine("Received bitfield length: {0}  ({1})", received.GetBytes().Length, CurrentFile.GetBitField().GetBytes().Length);
#endif
            DifferenceBitField = CurrentFile.GetBitField().Compare(received);
        }

        public void FetchAndSendPiece(byte[] packet)
        {
            PacketGenerator gs = new PacketGenerator(packet);
            int pieceRequested = gs.ReadInt();
            int pieceOffset = gs.ReadInt();
            int length = gs.ReadInt();

            byte[] fileData = new byte[1];
            
            if(CurrentFile.ReadPiece(pieceRequested, ref fileData, pieceOffset, length))
            {
#if DEBUG
                Console.WriteLine("Sending piece: {0}", pieceRequested);
#endif
                byte[] send =  PacketGenerator.Piece(pieceRequested, pieceOffset, fileData);
                Send(send);
            }
        }

        public void Have(byte[] data)
        {
            PacketGenerator gs = new PacketGenerator(data);
            int piece = gs.ReadInt();
#if DEBUG
            Console.WriteLine("Successfully uploaded piece {0}", piece);
#endif
        }
        
        public int SavePieceAndRequestForNewOne(byte[] packet)
        {
            PacketGenerator gs = new PacketGenerator(packet);
            int piece = gs.ReadInt();
            int pieceOffset = gs.ReadInt();
            byte[] data = gs.ReadLeftoverByteArray();

            CurrentFile.WritePiece(piece, pieceOffset, data);

            Console.WriteLine("Received piece: {0} {1:0.000}%", piece, ((float)CurrentFile.GetDownloadedSize() / (float)CurrentFile.GetSize()) * 100.0);

            if (data.Length < 1024*16)
            {
                byte[] have = PacketGenerator.Have(piece);
                Send(have);
               // Console.WriteLine("Successfully downloaded piece: {0}", piece);
            }

            return RequestForNewPiece();
        }

        public int RequestForNewPiece()
        {
            int piece=0, offset=0, length=0;
            if(!CurrentFile.GetNextMissingPiece(ref piece,ref offset,ref length))
            {
                return -1;
            }

            if (!DifferenceBitField.GetPiece(piece))
            {
#if DEBUG
                Console.WriteLine("Cutting connection.. Peer does not have the piece");
#endif
                return -1; //does not have the piece
            }

#if DEBUG
            Console.WriteLine("Requesting for piece: {0}", piece);
#endif

            byte[] pieceRequest = PacketGenerator.Request(piece, offset, length);
            Send(pieceRequest);
            State = PeerState.DOWNLOADING;

            return piece;
        }

        private byte[] Read()
        {
            byte[] buffer = new byte[1024 * 18];            
            int realRead = Stream.Read(buffer, 0, 1024 * 16);

            byte[] realBuffer = new byte[realRead];
            Array.Copy(buffer, realBuffer, realRead);
            return realBuffer;
        }

        private void Send(byte[] bytes)
        {
            Stream.Write(bytes, 0, bytes.Length);
            Stream.Flush();
            LastPacketSent = bytes;
        }

        private void SendPacket(byte[] packetData, byte packetID)
        {       
            byte[] bLength = BitConverter.GetBytes(packetID == 0 ? 0 : packetData.Length+1);
            byte[] bPacketID = { packetID };

            Array.Reverse(bLength); //little endian is a no-no

            Send(bLength);

            if(packetID != 0)
            {
                Send(bPacketID);
                Send(packetData);
            }
            
            Stream.Flush();
            Console.WriteLine("Sent packet: {0}", packetID);
        }

        private byte[] ReadPacket(ref byte packetID)
        {
            byte[] blength = new byte[4];
            byte[] bpacketid = new byte[1];

            Stream.Read(blength, 0, 4);
            Array.Reverse(blength); //big endian is a no-no
            int realLength = BitConverter.ToInt32(blength, 0);        

            if (realLength == 0)
            {
                packetID = 0xF0; //keep-alive
                return new byte[0];
            }
                
            Stream.Read(bpacketid, 0, 1);                   
            packetID = bpacketid[0];

            if (packetID >= 11 || Math.Abs(realLength) > 1024*17) //unrecognized packet
                return new byte[0];

            byte[] receivedbuffer = new byte[--realLength]; //reallength -1 cause first byte is the packetID and we already read it
            int receivedlength = 0;
            while(receivedlength < realLength)
            {
                receivedlength += Stream.Read(receivedbuffer, receivedlength, realLength - receivedlength);
            }

            return receivedbuffer;
        }

        private static string ByteArrayToHexString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:X2}", b);
            return hex.ToString();

        }

        private static byte[] HexStringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

    }
}
