using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SpotifyCurrentSong
{
    public interface ISongExtractor
    {
        void Start();
    }

    public class SpotifySongExtractor : ISongExtractor
    {
        public SpotifySongExtractor(IOptions<SpotifyApiOptions> spotifyOptions, ILogger<SpotifySongExtractor> logger)
        {
            SpotifyOptions = spotifyOptions;
            Logger = logger;
        }

        private ILogger<SpotifySongExtractor> Logger;
        private IOptions<SpotifyApiOptions> SpotifyOptions { get; }

        public void Start()
        {
            Logger.LogInformation("Started the Spotify song extractor...");
        }
    }
}
