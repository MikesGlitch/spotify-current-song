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
            /* TODO: 
             * I want this to run in the system tray - I'll probably want a UI for it? 
             *      If I do I may want to make this a WPF app(no linux support)? UWP is not suitable cause i can't make it a portable exe
             *      Also .NET comes with a lot of overhead, maybe Electron might be the better option here...
             *      Could try an electron app and it'll work on both linux and windows...
             *      Could check out how big the app size is as well and compare it.
             *      Otherwise WPF is a decent option...
             *      
             *      I think I should write this in electron and see how big it is in comparison
             * Put a front end on it: https://docs.microsoft.com/en-us/windows/apps/desktop/choose-your-platform
             * Make sure i'm on this list: https://johnnycrazy.github.io/SpotifyAPI-NET/docs/next/showcase/
            */
            SpotifyOptions = spotifyOptions;
            Logger = logger;
        }

        private ILogger<SpotifySongExtractor> Logger;

        private IOptions<SpotifyApiOptions> SpotifyOptions { get; }

        private EmbedIOAuthServer Server { get; set; }

        private string Verifier { get; set; }

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
                // TODO: Figure out how to handle this properly.  Will probably need to fail out and tell the user the port is taken or something
                Logger.LogCritical("Unable to open URL, manually open: {0}", uri);
            }
        }

        private async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await Server.Stop(); // TODO: Check if the refresh tokens work if the server is off?  Does the library turn the server on again?
            Server.Dispose();

            var client = new OAuthClient();
            var tokenResponse = await client.RequestToken(new PKCETokenRequest(SpotifyOptions.Value.ClientId, response.Code, new Uri(RedirectUri), Verifier));
            var authenticator = new PKCEAuthenticator(SpotifyOptions.Value.ClientId, tokenResponse);
            var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
            var spotifyClient = new SpotifyClient(config);

            RunSongExtractor(spotifyClient);
        }

        private void RunSongExtractor(SpotifyClient spotifyClient)
        {
            // TODO - All File.WriteAllText needs exception handling

            Logger.LogInformation("Started the Spotify song extraction process...");

            var timer = new Timer(SpotifyOptions.Value.PollIntervalMilliseconds);
            timer.Elapsed += async (object sender, ElapsedEventArgs e) =>
            {
                Logger.LogInformation("Calling spotify API to get the currently playing item...");
                var currentlyplaying = await spotifyClient.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());

                if (currentlyplaying == null)
                {
                    return;
                }

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
