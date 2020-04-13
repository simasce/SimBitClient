# SimBitClient
Simple C# Bittorrent client for single-file downloads

A simple BitTorrent client that utilizes single peer connection and downloads a single-file torrent.
Download speeds usually max out on ~32KB/s due to lack of multi-peer support.

# Usage:
SimBitClient.exe torrentfile

There are 2 example torrent files attached for testing. You can find them in TestFiles folder.

Example usage:

SimBitClient.exe dogg.gif.torrent


SimBitClient.exe debiannetinst.torrent   -- Warning: - ~2.5 hour long download.

  
# Libraries used:
Bencode.NET - For easier parsing of torrent file data.
