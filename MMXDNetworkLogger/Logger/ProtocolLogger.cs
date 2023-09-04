using BepInEx.Logging;
using FlatBuffers;
using HarmonyLib;
using Newtonsoft.Json;
using OrangeSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MMXDNetworkLogger.Loggers
{
    // Serializer for ByteBuffer so we can serialize the data stored in NT and RS protocols
    class ByteBufferConverter : JsonConverter<ByteBuffer>
    {
        public override ByteBuffer ReadJson(JsonReader reader, Type objectType, ByteBuffer existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, ByteBuffer value, JsonSerializer serializer)
        {
            var dict = new Dictionary<string, object>();

            dict["Length"] = value.Length;
            dict["_buffer"] = value._buffer;

            serializer.Serialize(writer, dict);
        }
    }

    public static class ProtocolLogger
    {
        internal static ManualLogSource PluginLogger;

        internal static ManualLogSource CBLogger;
        internal static ManualLogSource CCLogger;
        internal static ManualLogSource CMLogger;
        internal static Dictionary<string, ManualLogSource> ChannelToLogger;

        internal static string BaseLoggingPath;
        internal static Dictionary<string, string> ChannelToLogPath;

        // SocketClientEx`1[CMSocketClient]
        static readonly int SocketClientChannelIndex = "SocketClientEx`1".Length + 1;
        static readonly int RequestNameIndex = "CreateRQ".Length;
        static readonly int FlatBufferChannelIndex = nameof(FlatBufferCBHelper).Replace("Helper", "").Length - 2;

        public static void Initialize(ManualLogSource pluginLogger, string pluginPath)
        {
            PluginLogger = pluginLogger;

            BaseLoggingPath = pluginPath;
            ChannelToLogPath = new Dictionary<string, string>();

            CreateDirectories();

            CBLogger = new ManualLogSource("Battle    (CB)");
            CCLogger = new ManualLogSource("Community (CC)");
            CMLogger = new ManualLogSource("Match     (CM)");

            ChannelToLogger = new Dictionary<string, ManualLogSource>();
            ChannelToLogger["CB"] = CBLogger;
            ChannelToLogger["CC"] = CCLogger;
            ChannelToLogger["CM"] = CMLogger;

            var logFileEvent = NetworkLogWriter.CreateLogFileEvent(Path.Combine(BaseLoggingPath, "ProtocolLog.txt"));
            foreach (var logger in ChannelToLogger.Values)
            {
                logger.LogEvent += logFileEvent;
            }
        }

        public static void PatchHarmony(Harmony harmony)
        {
            harmony.PatchAll(typeof(ProtocolLogger));

            foreach (var method in CreateRQTargetMethods())
            {
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(ProtocolLogger).GetMethod(nameof(CreateRQPrefix))));
            }

            foreach (var method in DeserializeTargetMethods())
            {
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(ProtocolLogger).GetMethod(nameof(DeserializePostfix))));
            }
        }

        private static void CreateDirectories()
        {
            Directory.CreateDirectory(BaseLoggingPath);

            var types = new (Type, Type)[] {
                (typeof(FlatBufferCBHelper), typeof(cb.CB)),
                (typeof(FlatBufferCCHelper), typeof(cc.CC)),
                (typeof(FlatBufferCMHelper), typeof(cm.CM)),
            };

            // Create a folder for each protocol type in each channel
            foreach (var (type, channel) in types)
            {
                var newPath = Path.Combine(BaseLoggingPath, $"Protocol{channel.Name}");
                Directory.CreateDirectory(newPath);
                ChannelToLogPath[channel.Name] = newPath;

                foreach (var enumName in Enum.GetNames(channel))
                {
                    if (enumName == "NONE")
                    {
                        continue;
                    }

                    Directory.CreateDirectory(Path.Combine(newPath, enumName.Substring(2)));
                }
            }
        }

        private static IEnumerable<MethodBase> CreateRQTargetMethods()
        {
            // Find all methods that create a request
            var targetMethods = new List<MethodBase>();
            var types = new Type[] {
                typeof(FlatBufferCBHelper),
                typeof(FlatBufferCCHelper),
                typeof(FlatBufferCMHelper),
            };

            foreach (var type in types)
            {
                targetMethods.AddRange(type.GetMethods().Where(method => method.Name.StartsWith("CreateRQ")).Cast<MethodBase>());
            }

            return targetMethods.AsEnumerable();
        }

        private static IEnumerable<MethodBase> DeserializeTargetMethods()
        {
            var types = new Type[] {
                typeof(FlatBufferCBDeserializer),
                typeof(FlatBufferCCDeserializer),
                typeof(FlatBufferCMDeserializer),
            };

            return types.Select(type => type.GetMethod("Deserialize")).Cast<MethodBase>();
        }

        public static void CreateRQPrefix(object[] __args, MethodBase __originalMethod)
        {
            // Log the arguments of the function that creates a new request
            var now = DateTime.Now;
            var info = new Dictionary<string, object>();

            var parameters = __originalMethod.GetParameters();
            for (var i = 0; i < __args.Length; i++)
            {
                info[parameters[i].Name] = __args[i];
            }

            var channel = __originalMethod.DeclaringType.Name.Substring(FlatBufferChannelIndex, 2);
            var requestName = __originalMethod.Name.Substring(RequestNameIndex);

            File.WriteAllText(
                Path.Combine(
                    ChannelToLogPath[channel],
                    requestName,
                    $"{now.ToString("HH'h'-mm'm'-ss.fff's'")}.json"
                ),
                JsonConvert.SerializeObject(info)
            );

            ChannelToLogger[channel].LogInfo($"RQ{requestName}");
        }

        public static void DeserializePostfix(IFlatbufferObject __result, MethodBase __originalMethod)
        {
            // Log the deserialized RQ/RS/NT object (which still needs to have its buffer read)
            var now = DateTime.Now;
            var name = __result.GetType().Name;

            var channel = __originalMethod.DeclaringType.Name.Substring(FlatBufferChannelIndex, 2);

            File.WriteAllText(
                Path.Combine(
                    ChannelToLogPath[channel],
                    name.Substring(2),
                    $"{now.ToString("HH'h'-mm'm'-ss.fff's'")}_{name.Substring(0, 2)}.json"
                ),
                JsonConvert.SerializeObject(__result, new ByteBufferConverter())
            );

            ChannelToLogger[channel].LogInfo(name);
        }

        [HarmonyPatch(typeof(SocketClientEx<CBSocketClient>), nameof(SocketClientEx<CBSocketClient>.ConnectToServer))]
        [HarmonyPatch(typeof(SocketClientEx<CCSocketClient>), nameof(SocketClientEx<CCSocketClient>.ConnectToServer))]
        [HarmonyPatch(typeof(SocketClientEx<CMSocketClient>), nameof(SocketClientEx<CMSocketClient>.ConnectToServer))]
        [HarmonyPostfix]
        static void SocketClientEx_ConnectToServerPostfix(string host, int port, SocketClientEx<MonoBehaviour> __instance, MethodBase __originalMethod)
        {
            if (ChannelToLogger.TryGetValue($"{__originalMethod.DeclaringType}".Substring(SocketClientChannelIndex, 2), out var logger))
            {
                switch (__instance.tcpState)
                {
                    case SocketClientEx<MonoBehaviour>.TcpClientState.Closed:
                        logger.LogInfo($"FAILED CONNECTING to {host}:{port}");
                        break;
                    case SocketClientEx<MonoBehaviour>.TcpClientState.Connected:
                        logger.LogInfo($"CONNECTED to {host}:{port}");
                        break;
                    case SocketClientEx<MonoBehaviour>.TcpClientState.Connecting:
                        logger.LogInfo($"CONNECTING to {host}:{port}");
                        break;
                    case SocketClientEx<MonoBehaviour>.TcpClientState.Disconnecting:
                        logger.LogInfo($"INVALID CONNECTING to {host}:{port}");
                        break;
                }
            }
        }

        [HarmonyPatch(typeof(SocketClientEx<CBSocketClient>), nameof(SocketClientEx<CBSocketClient>.Disconnect))]
        [HarmonyPatch(typeof(SocketClientEx<CCSocketClient>), nameof(SocketClientEx<CCSocketClient>.Disconnect))]
        [HarmonyPatch(typeof(SocketClientEx<CMSocketClient>), nameof(SocketClientEx<CMSocketClient>.Disconnect))]
        [HarmonyPrefix]
        static void SocketClientEx_DisconnectPrefix(SocketClientEx<MonoBehaviour> __instance, MethodBase __originalMethod)
        {
            if (ChannelToLogger.TryGetValue($"{__originalMethod.DeclaringType}".Substring(SocketClientChannelIndex, 2), out var logger))
            {
                logger.LogInfo($"DISCONNECTED from {__instance.Host}:{__instance.Port}");
            }
        }
    }
}
