using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace SpotifyCurrentSong
{
    public interface ISongExtractor : IAsyncDisposable
    {
        Task Start();
    }

    public class SpotifySongExtractor : ISongExtractor
    {
        private const int MaxRetryCountForFileWrite = 5;

        public SpotifySongExtractor(IOptions<SpotifyApiOptions> spotifyOptions, ILogger<SpotifySongExtractor> logger)
        {
            SpotifyOptions = spotifyOptions;
            Logger = logger;
            RedirectUri = $"http://localhost:{spotifyOptions.Value.AuthServerPort}/callback";
            FileLocation = Path.Combine(spotifyOptions.Value.FileDirectoryPath, spotifyOptions.Value.CurrentSongFilename);
            (Verifier, Challenge) = PKCEUtil.GenerateCodes(120);
        }

        private ILogger<SpotifySongExtractor> Logger { get; }

        private IOptions<SpotifyApiOptions> SpotifyOptions { get; }

        private string Verifier { get; }

        private string Challenge { get; }

        private string FileLocation { get; }

        private string RedirectUri { get; }

        private int CurrentRetryCountForFileWrite { get; set; }

        private string? CurrentSongWritten { get; set; }

        private EmbedIOAuthServer? Server { get; set; }

        private Timer? SongExtractorTimer { get; set; }

        public async Task Start()
        {
            Logger.LogInformation("Started the Spotify song extractor...");
            await Authorize();
        }

        private async Task Authorize()
        {
            Logger.LogInformation("Started the Spotify authorization process...");
            Server = new EmbedIOAuthServer(new Uri(RedirectUri), SpotifyOptions.Value.AuthServerPort);
            await Server.Start();

            Server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;

            var loginRequest = new LoginRequest(Server.BaseUri, SpotifyOptions.Value.ClientId, LoginRequest.ResponseType.Code)
            {
                CodeChallengeMethod = "S256",
                CodeChallenge = Challenge,
                Scope = new[] { Scopes.UserReadCurrentlyPlaying }
            };

            var uri = loginRequest.ToUri();

            try
            {
                BrowserUtil.Open(uri);
            }
            catch (Exception)
            {
                Logger.LogCritical("Unable to open a browser.  Please manually open: {0}", uri);
            }
        }

        private async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await StopAndDisposeServer();

            var client = new OAuthClient();
            var tokenResponse = await client.RequestToken(new PKCETokenRequest(SpotifyOptions.Value.ClientId, response.Code, new Uri(RedirectUri), Verifier));
            var authenticator = new PKCEAuthenticator(SpotifyOptions.Value.ClientId, tokenResponse);
            var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
            var spotifyClient = new SpotifyClient(config);

            RunSongExtractor(spotifyClient);
        }

        private void RunSongExtractor(SpotifyClient spotifyClient)
        {
            Logger.LogInformation("Started the Spotify song extraction process...");

            SongExtractorTimer = new Timer(SpotifyOptions.Value.PollIntervalMilliseconds);
            SongExtractorTimer.Elapsed += async (object sender, ElapsedEventArgs e) =>
            {
                Logger.LogInformation("Calling spotify API to get the currently playing item...");
                var currentlyplaying = await spotifyClient.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                var songText = string.Empty;

                if (currentlyplaying == null)
                {
                    if (SpotifyOptions.Value.WriteEmptyFileWhenNoTrackPlaying)
                    {
                        WriteSongToFile(songText);
                    }

                    return;
                }

                switch (currentlyplaying.Item)
                {
                    case FullTrack fullTrack:
                        var artistsText = string.Join(", ", fullTrack.Artists.Select(x => x.Name));
                        var licence = GetLicenceForArtist(fullTrack.Artists.FirstOrDefault()?.Name);
                        songText = SpotifyOptions.Value.CurrentSongTextFormat
                            .Replace(SpotifyApiOptions.SongToken, fullTrack.Name)
                            .Replace(SpotifyApiOptions.ArtistToken, artistsText)
                            .Replace(SpotifyApiOptions.licenceToken, licence);
                        break;
                    case FullEpisode fullEpisode:
                        songText = SpotifyOptions.Value.CurrentSongTextFormat
                            .Replace(SpotifyApiOptions.SongToken, fullEpisode.Name)
                            .Replace(SpotifyApiOptions.ArtistToken, fullEpisode.Show.Name)
                            .Replace(SpotifyApiOptions.licenceToken, "(Royalty free)");
                        break;
                }

                WriteSongToFile(songText);
            };

            SongExtractorTimer.AutoReset = true;
            SongExtractorTimer.Enabled = true;
        }

        private string GetLicenceForArtist(string? artist)
        {
            if (!string.IsNullOrEmpty(artist) && SpotifyOptions.Value.ArtistLicences.TryGetValue(artist, out var licence))
            {
                return licence;
            }
            
            return "(Copyright free)";
        }

        private void WriteSongToFile(string songText)
        {
            if (CurrentSongWritten == songText)
            {
                Logger.LogInformation($"Waiting until the next song before writing to file...");
                return;
            }

            try
            {
                File.WriteAllText(FileLocation, songText);
                CurrentSongWritten = songText;
                CurrentRetryCountForFileWrite = 0;
                Logger.LogInformation($"Wrote song information to: {FileLocation}");
            } 
            catch(Exception ex)
            {
                CurrentRetryCountForFileWrite++;
                Logger.LogError($"Could not write file to {FileLocation}", ex);

                if (CurrentRetryCountForFileWrite == MaxRetryCountForFileWrite)
                {
                    Logger.LogCritical($"After retrying {MaxRetryCountForFileWrite} time the file could not be written", ex);
                    // When we hit our max retries we can assume the app isn't working due to a file system problem
                    throw;
                }
            }
        }


        private async Task StopAndDisposeServer()
        {
            if (Server != null)
            {
                await Server.Stop();
                Server.Dispose();
                Server = null;
            }
        }

        private void StopAndDisposeSongExtractorTimer()
        {
            if (SongExtractorTimer!= null)
            {
                SongExtractorTimer.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAndDisposeServer();
            StopAndDisposeSongExtractorTimer();
        }
    }
}
