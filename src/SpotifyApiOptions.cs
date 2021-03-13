using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace SpotifyCurrentSong
{
    public class SpotifyApiOptions
    {
        public const string SongToken = "{SONG}";

        public const string ArtistToken = "{ARTIST}";

        public const string licenceToken = "{LICENCE}";

        /// <summary>
        /// Client ID in spotify - allow the user to change it to their own if they want
        /// </summary>
        public string ClientId { get; set; } = "a810260eade74475addf269c50d87929";

        /// <summary>
        /// The localhost port that will serve the auth callback webpage.  
        /// This can only be changed IF the ClientID is changed due to Spotify app secruity (it'll need to be set as a valid redirect uri)
        /// </summary>
        public int AuthServerPort { get; set; } = 8035;

        /// <summary>
        /// File directory of the txt file to write to
        /// </summary>
#pragma warning disable CS8601 // Possible null reference assignment.
        public string FileDirectoryPath { get; set; } = Path.GetDirectoryName(Process.GetCurrentProcess()?.MainModule?.FileName ?? AppDomain.CurrentDomain.BaseDirectory);
#pragma warning restore CS8601 // Possible null reference assignment.

        /// <summary>
        /// Filename of the txt file to write to
        /// </summary>
        public string CurrentSongFilename { get; set; } = "spotify-currently-playing.txt";

        /// <summary>
        /// Text format to use when writing the file.  All {} properties are token replaced
        /// Valid Tokens are {SONG} and {ARTIST}
        /// </summary>
        public string CurrentSongTextFormat { get; set; } = $"{SongToken} by {ArtistToken} - {licenceToken}";

        /// <summary>
        /// Delay (in milliseconds) before the next Spotify API call is made
        /// </summary>
        public int PollIntervalMilliseconds { get; set; } = 3000;

        /// <summary>
        /// Wriite an empty file when no track is playing.  It can be turned off to optimize performance/minimize file writes
        /// </summary>
        public bool WriteEmptyFileWhenNoTrackPlaying { get; set; } = true;
    }
}
