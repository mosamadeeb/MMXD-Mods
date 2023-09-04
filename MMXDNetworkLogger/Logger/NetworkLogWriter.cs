using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MMXDNetworkLogger.Loggers
{
    internal static class NetworkLogWriter
    {
        private static List<TextWriter> Writers = new List<TextWriter>();

        private static void WriteLogEvent(object sender, LogEventArgs eventArgs, int writerIndex)
        {
            // TextWriter doesn't have an AutoFlush property, while StreamWriter can't be created as Synchronized...
            // So the solution is to flush after every write
            Writers[writerIndex].WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}][{eventArgs.Source.SourceName}] {eventArgs.Data}");
            Writers[writerIndex].Flush();
        }

        public static EventHandler<LogEventArgs> CreateLogFileEvent(string filePath)
        {
            int writerIndex = Writers.Count;
            Writers.Add(TextWriter.Synchronized(File.AppendText(filePath)));

            return (sender, eventArgs) =>
            {
                Task.Run(() => WriteLogEvent(sender, eventArgs, writerIndex));
            };
        }
    }
}
