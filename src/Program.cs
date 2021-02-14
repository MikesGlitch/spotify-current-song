using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
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
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, configuration) =>
                {
                    configuration.Sources.Clear();

                    var env = hostingContext.HostingEnvironment;
                    configuration
                        .SetFileLoadExceptionHandler((context) =>
                        {
                            var exeLocation = Path.GetDirectoryName(Process.GetCurrentProcess()?.MainModule?.FileName ?? AppDomain.CurrentDomain.BaseDirectory);
                            var errorInfo = $"You configuration could not be read - please ensure it is valid before running the exe \n\n {context.Exception}";
                            File.WriteAllText(exeLocation + "/config-error.txt", errorInfo);
                        })
                        .AddJsonFile("appsettings.json", optional: false);
                })
                .ConfigureServices((hostBuilderContext, services) =>
                {
                    services.Configure<SpotifyApiOptions>(hostBuilderContext.Configuration.GetSection(key: nameof(SpotifyApiOptions)));
                    services.AddSingleton<ISongExtractor, SpotifySongExtractor>();
                });
        }
    }
}
