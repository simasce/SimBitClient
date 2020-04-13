using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimBitClient
{
    class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            TorrentDownloader down = new TorrentDownloader("dogg.gif.torrent"); //we set our own stuf on debug
#else
            if(args.Length < 1)
            {
                Console.WriteLine("Usage: SimBitClient.exe <torrentfile>");
                return;
            }

            if(!File.Exists(args[0]))
            {
                Console.WriteLine("File does not exist: {0}", args[0]);
                return;
            }

            TorrentDownloader down = new TorrentDownloader(args[0]);
#endif
            bool result = down.Start();
            if (result)
                Console.WriteLine("Successfully downloaded!");
            else
                Console.WriteLine("Failed to download!");

            Console.WriteLine("Press any key to exit!");
            Console.ReadKey();
        }
    }
}
