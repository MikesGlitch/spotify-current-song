using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace SpotifyCurrentSong
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using IHost host = CreateHostBuilder(args).Build();

            var spotifyExtractor = host.Services.GetRequiredService<ISongExtractor>();
            await spotifyExtractor.Start();

            await host.RunAsync();
        }

        static IHostBuilder CreateHostBuilder(string[] args)
        {
            // Consider a post startup task - https://docs.microsoft.com/en-us/dotnet/core/extensions/generic-host
            // It will authenticate with spotify and grab an access token

            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, configuration) =>
                {
                    configuration.Sources.Clear();

                    var env = hostingContext.HostingEnvironment;

                    configuration
                        .AddJsonFile("appsettings.json", optional: true)
                        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true);

                    var configurationRoot = configuration.Build();
                })
                .ConfigureServices((hostBuilderContext, services) =>
                {
                    services.Configure<SpotifyApiOptions>(hostBuilderContext.Configuration.GetSection(key: nameof(SpotifyApiOptions)));
                    services.AddSingleton<ISongExtractor, SpotifySongExtractor>();
                });
        }
    }
}
