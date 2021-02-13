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
    public interface ISongExtractor
    {
        Task Start();
    }

    public class SpotifySongExtractor : ISongExtractor
    {
        private const string RedirectUri = "http://localhost:5000/callback";

        public SpotifySongExtractor(IOptions<SpotifyApiOptions> spotifyOptions, ILogger<SpotifySongExtractor> logger)
        {
            SpotifyOptions = spotifyOptions;
            Logger = logger;
        }

        private ILogger<SpotifySongExtractor> Logger;

        private IOptions<SpotifyApiOptions> SpotifyOptions { get; }

        private EmbedIOAuthServer Server { get; set; }

        private string Verifier { get; set; }

        private SpotifyClient SpotifyClient { get; set; }

        public async Task Start()
        {
            Logger.LogInformation("Started the Spotify song extractor...");
            await Authorize();
        }

        private async Task Authorize()
        {
            Logger.LogInformation("Started the Spotify authorization process...");
            Server = new EmbedIOAuthServer(new Uri(RedirectUri), 5000);
            await Server.Start();

            var pkceCodes= PKCEUtil.GenerateCodes(120);
            Verifier = pkceCodes.verifier;
            Server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;

            var loginRequest = new LoginRequest(Server.BaseUri, SpotifyOptions.Value.ClientId, LoginRequest.ResponseType.Code)
            {
                CodeChallengeMethod = "S256",
                CodeChallenge = pkceCodes.challenge,
                Scope = new[] { Scopes.UserReadCurrentlyPlaying }
            };

            var uri = loginRequest.ToUri();

            try
            {
                BrowserUtil.Open(uri);
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to open URL, manually open: {0}", uri);
            }
        }

        private async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await Server.Stop(); // TODO: Check if the refresh tokens work if the server is off?  Does the library turn the server on again?

            var client = new OAuthClient();
            var tokenResponse = await client.RequestToken(new PKCETokenRequest(SpotifyOptions.Value.ClientId, response.Code, new Uri(RedirectUri), Verifier));
            var authenticator = new PKCEAuthenticator(SpotifyOptions.Value.ClientId, tokenResponse);
            var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
            SpotifyClient = new SpotifyClient(config);

            RunSongExtractor();
        }

        private void RunSongExtractor()
        {
            Logger.LogInformation("Started the Spotify song extraction process...");

            var timer = new Timer(SpotifyOptions.Value.PollIntervalMilliseconds);
            timer.Elapsed += async (object sender, ElapsedEventArgs e) =>
            {
                Logger.LogInformation("Calling spotify API to get the currently playing item...");
                var currentlyplaying = await SpotifyClient.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());

                switch (currentlyplaying.Item)
                {
                    case FullTrack fullTrack:
                        var artistsText = string.Join(", ", fullTrack.Artists.Select(x => x.Name));
                        var fullTrackOutput = $"\"{fullTrack.Name}\" by {artistsText} - www.scottbuckley.com.au (CC BY 4.0)";
                        File.WriteAllText(SpotifyOptions.Value.FilePath, fullTrackOutput);
                        break;
                    case FullEpisode fullEpisode:
                        var fullEpisodeOutput = $"\"{fullEpisode.Name}\" - {fullEpisode.Show.Name}";
                        File.WriteAllText(SpotifyOptions.Value.FilePath, fullEpisodeOutput);
                        break;
                }
            };

            timer.AutoReset = true;
            timer.Enabled = true;
        }
    }
}
