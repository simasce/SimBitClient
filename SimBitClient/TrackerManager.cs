using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SimBitClient
{
    struct SimplePeer
    {
        public string IP;
        public int Port;
    }

    class TrackerManager
    {
        private BencodeParser BencodeParser;
        private Torrent TorrentFile;
        private string AnnounceData;
        private string PeerID;
        private byte[] PureAnnounceData;

        private List<SimplePeer> Peers = new List<SimplePeer>();
       
        public TrackerManager(Torrent torrentFile)
        {
            this.BencodeParser = new BencodeParser();
            this.TorrentFile = torrentFile;
            this.PeerID = GeneratePeerId();
        }

        public bool Announce(long uploaded=0, long downloaded=0)
        {
           
                this.PureAnnounceData = GetRequest(
                string.Format("{0}?info_hash={1}&peer_id={2}&left={3}&port=6889&uploaded={4}&downloaded={5}&compact=1&no_peer_id=1",
                this.TorrentFile.Trackers[0][0],
                GenerateHexUrlString(this.TorrentFile.GetInfoHash()),
                this.PeerID,
                this.TorrentFile.File.FileSize,
                uploaded.ToString(),
                downloaded.ToString()
                ));

            this.AnnounceData = Encoding.UTF8.GetString(PureAnnounceData);

            if (this.AnnounceData.Length == 0)
                return false;

            int index = this.AnnounceData.IndexOf("peers");

            if (index == -1)
                return false;

            string sPeerLength = "";
            for(int i = index+5; i <index+12;i++)
            {
                if(this.AnnounceData[i] == ':')
                {
                    index = i + 1;
                    break;
                }

                sPeerLength += this.AnnounceData[i];
            }

            int peerlength = Convert.ToInt32(sPeerLength);
            int peerBytes = -1;

            byte[] subtrac = Encoding.UTF8.GetBytes(AnnounceData.Substring(0, index - 1)); 
            peerBytes = subtrac.Length + 1;

            string externalip = new WebClient().DownloadString("http://icanhazip.com");

            Peers.Clear();
            for(int i = peerBytes; i < peerlength + peerBytes; i+=6)
            {
                if (PureAnnounceData.Length - i < 6)
                    break;

                string ip = string.Format("{0}.{1}.{2}.{3}", PureAnnounceData[i], PureAnnounceData[i+1], PureAnnounceData[i + 2], PureAnnounceData[i + 3]);
                int port = (int)(PureAnnounceData[i + 4] << 8) + (int)PureAnnounceData[i + 5];

                if(!ip.Contains(externalip)) //we don't talk to ourselves omegaLUL
                    Peers.Add(new SimplePeer() { IP = ip, Port = port });
            }

            Console.WriteLine("Found {0} peers!", Peers.Count);
            return AnnounceData.Length > 0;
        }

        public string GetAnnounceData()
        {
            return AnnounceData;
        }

        public List<SimplePeer> GetPeers()
        {
            return Peers;
        }

        public string GetPeerID()
        {
            return PeerID;
        }

        private byte[] GetRequest(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            byte[] readBuffer = new byte[2048];
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream,Encoding.UTF8))
                {
                    int realLength = stream.Read(readBuffer, 0, 2048);
                    byte[] returnBuffer = new byte[realLength];
                    Array.Copy(readBuffer, returnBuffer, realLength);
                    return returnBuffer;
                }
            }
            catch(Exception)
            {
                return new byte[0];
            }
        }

        private string GeneratePeerId()
        {
            const string IDGen = "0123456789";
            string peerId = "-BT2080-"; //BitTorrent preffix
            Random r = new Random();
            for (int i = 8; i < 20; i++)
                peerId += IDGen[r.Next(IDGen.Length-1)];
            return peerId;
        }

        private string GenerateHexUrlString(string hexString, bool convertToHex = false)
        {
            string outString = "";

            if(!convertToHex)
            {
                for (int i = 0; i < hexString.Length; i += 2)
                {
                    outString += "%" + hexString[i] + hexString[i + 1];
                }
            }
            else
            {
                ASCIIEncoding encoder = new ASCIIEncoding();
                byte[] hexS = encoder.GetBytes(hexString);
                for (int i = 0; i < hexS.Length; i++)
                {
                    outString += "%" + hexS[i].ToString("X2");
                }
            }
            
                
            return outString;
        }

    }
}
