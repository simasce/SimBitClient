using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace SimBitClient
{
    public class TorrentDownloader
    {
        private TrackerManager TrackerManager;
        private Torrent TorrentFile;
        private BencodeParser BCodeParser;
        private List<Peer> Peers = new List<Peer>();
        private TimeManager Timer = new TimeManager();

        public TorrentDownloader(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found: " + filePath); //uh oh 

            this.BCodeParser = new BencodeParser();
            this.TorrentFile = BCodeParser.Parse<Torrent>(filePath);
            this.TrackerManager = new TrackerManager(this.TorrentFile);
        }

        public bool Start()
        {
            BitFile fs = new BitFile(this.TorrentFile.File.FileName, this.TorrentFile.File.Md5Sum, this.TorrentFile.File.FileSize, (long)Math.Ceiling((double)this.TorrentFile.File.FileSize / (double)this.TorrentFile.PieceSize), this.TorrentFile.PieceSize);

            loopStart:

            if(fs.AlmostFinished())
            {
                Console.WriteLine("Flushing buffers!");
                while(!fs.Finished()) { Thread.Sleep(5); }
            }

            if(fs.Finished())
            {
                return true;
            }

            Console.WriteLine("Announcing...");
            if (!this.TrackerManager.Announce())
                return false;
            
            Timer.Reset();

            for (int i = 0; i < TrackerManager.GetPeers().Count; i++)
            {
                 var peerInfo = TrackerManager.GetPeers()[i];
                 Peer pr = new Peer(peerInfo.IP, peerInfo.Port, this.TorrentFile.GetInfoHash(), TrackerManager.GetPeerID(), fs);             
                 try
                 {
                     if (!pr.BeginConnection())
                         continue;
                 }
                 catch(Exception e)
                 {
#if DEBUG
                    Console.WriteLine("Exception: {0}", e.Message);
#endif
                    if(pr._ReadThread != null)
                        pr._ReadThread.Abort();
                     continue;
                 }

                if (fs.AlmostFinished())
                {
                    Console.WriteLine("File successfully downloaded!");
                    goto loopStart;
                }

                if (Timer.GetElapsedSeconds() >= 60.0 * 5)
                {
                    goto loopStart;
                }
            }

            goto loopStart;
        }
        
    }
}
