// In Logging/Log.cs
using Serilog;
using Serilog.Core;

namespace QueryX.Logging
{
    public static class Log
    {
        // This holds the single logger instance for the entire application
        public static ILogger? Logger { get; private set; }

        public static void Initialize(string logFilePath)
        {
            // Configure the logger
            // - WriteTo.File: Specifies the file sink.
            // - rollingInterval: Creates a new log file daily.
            // - retainedFileCountLimit: Keeps the last 7 days of logs.
            // - outputTemplate: Defines the format of each log message.
            // - MinimumLevel.Debug(): Records everything from Debug level upwards.
            Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logFilePath,
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: 7,
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }
    }
}