using System;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Certera.Web
{
    public class Program
    {
        private static CancellationTokenSource _cancelTokenSource;
        private static bool _restartRequested;
        private static bool _shutdownRequested;
        public static string ConfigFileName { get; private set; }

        public static void Main(string[] args)
        {
            while (true)
            {
                _cancelTokenSource = new CancellationTokenSource();
                var appThread = new Thread(new ThreadStart(() => {
                    var host = CreateHostBuilder(args).Build().InitializeDatabase();
                    try
                    {
                        var task = host.RunAsync(_cancelTokenSource.Token);
                        task.GetAwaiter().GetResult();

                        // User does CTRL+C
                        _shutdownRequested = task.IsCompletedSuccessfully;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }));

                if (_shutdownRequested)
                {
                    if (_restartRequested)
                    {
                        // Clear flag
                        _restartRequested = false;
                        Thread.Sleep(3000);
                    }
                    else
                    {
                        break;
                    }
                }

                appThread.Start();

                // Block and wait until thread is terminated due to restart
                appThread.Join();
            }
        }

        public static void Restart()
        {
            _restartRequested = true;
            _cancelTokenSource.Cancel();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHost(builder => {
                    builder.ConfigureAppConfiguration((hostingContext, appBuilder) => {
                        var env = hostingContext.HostingEnvironment;

                        ConfigFileName = env.IsProduction()
                            ? "config.json"
                            : $"config.{env.EnvironmentName}.json";

                        appBuilder.AddJsonFile("config.json", optional: true, reloadOnChange: true)
                                  .AddJsonFile($"config.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    })
                    .UseKestrel()
                    .ConfigureKestrel((context, options) => options.ConfigureEndpoints())
                    .UseStartup<Startup>();
                });
    }
}