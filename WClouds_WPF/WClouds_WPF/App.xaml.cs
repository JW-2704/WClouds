using System.Configuration;
using System.Data;
using System.Windows;
using Serilog;
using Serilog.Sinks;

namespace WClouds_WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Verbose)
                .WriteTo.File(
                    "log.txt",
                    rollingInterval: RollingInterval.Month,
                    fileSizeLimitBytes: 1000000,
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug)
                .CreateLogger();
        }

        public static int CurrentUserId { get; set; }
    }
}
