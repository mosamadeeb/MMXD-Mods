using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MMXDNetworkLogger.Loggers
{
    public static class RequestLogger
    {
        internal static ManualLogSource PluginLogger;
        internal static ManualLogSource ReqLogger;
        internal static ManualLogSource ResLogger;

        internal static string LoggingPath;

        public static void Initialize(ManualLogSource pluginLogger, string pluginPath)
        {
            PluginLogger = pluginLogger;
            ReqLogger = new ManualLogSource("Request ");
            ResLogger = new ManualLogSource("Response");

            LoggingPath = Path.Combine(pluginPath, "Requests");
            Directory.CreateDirectory(LoggingPath);

            var logEvent = NetworkLogWriter.CreateLogFileEvent(Path.Combine(pluginPath, "RequestLog.txt"));
            ReqLogger.LogEvent += logEvent;
            ResLogger.LogEvent += logEvent;
        }

        public static void PatchHarmony(Harmony harmony)
        {
            harmony.PatchAll(typeof(RequestLogger));
        }

        [HarmonyPatch(typeof(ServerService<MonoBehaviour>), nameof(ServerService<MonoBehaviour>.BeginCommand))]
        [HarmonyPrefix]
        static bool BeginCommandGlobal(RequestCommand cmd, ServerService<MonoBehaviour> __instance, ref IEnumerator __result)
        {
            __result = BeginCommandGlobalEnumerator(cmd, __instance);
            return false;
        }

        static IEnumerator BeginCommandGlobalEnumerator(RequestCommand cmd, ServerService<MonoBehaviour> __instance)
        {
            // This is the entire function from the assembly
            if (string.IsNullOrEmpty(__instance.ServerUrl))
            {
                Debug.LogError("ServerUrl is not Initialized");
                yield break;
            }
            cmd.serverRequest.SeqID = __instance.SeqID;
            __instance.lastRequestCommand = cmd;
            __instance.SendingRequest(true);
            if (HttpSetting.MinRequestDuration - (DateTime.Now - __instance.lastResponseTime).TotalMilliseconds > 0.0)
            {
                yield return __instance.waitforSecRealtime;
            }
            IRequest serverRequest = cmd.serverRequest;

            // LOGGING
            string reqName = serverRequest.GetType().Name;
            string req = JsonConvert.SerializeObject(serverRequest);

            ReqLogger.LogInfo(reqName);
            Directory.CreateDirectory(Path.Combine(LoggingPath, reqName));
            File.WriteAllText(Path.Combine(LoggingPath, reqName, $"{DateTime.Now.ToString("HH'h'-mm'm'-ss's'")}.json"), req);

            string text = JsonHelper.Serialize(serverRequest);
            string text2 = AesCrypto.Encode(text);
            string text3 = string.Format("{0}/{1}", __instance.ServerUrl.TrimEnd(new char[] { '/' }), serverRequest.GetType().Name);
            UnityWebRequest www = UnityWebRequest.Put(text3, Encoding.ASCII.GetBytes(text2));
            www.method = "POST";
            www.timeout = HttpSetting.Timeout;
            www.SetRequestHeader("authorization", __instance.serviceToken);
            www.SetRequestHeader("Content-Type", "text/plain");
            www.SetRequestHeader("user-agent", "");
            Debug.Log(string.Format("Client送出HTTP封包, Request={0}, URL={1}", serverRequest.GetType(), www.url));
            yield return www.SendWebRequest();
            while (!www.isDone)
            {
                yield return CoroutineDefine._waitForEndOfFrame;
            }
            __instance.lastResponseTime = DateTime.Now;
            __instance.lastRequestCommand = null;
            __instance.SendingRequest(false);
            if (www.isNetworkError)
            {
                __instance.WWWNetworkError(cmd);
            }
            else if (www.isHttpError)
            {
                __instance.WWWRequestError(www, cmd);
            }
            else
            {
                byte[] data = www.downloadHandler.data;
                Debug.Log("Client收到Server的回傳封包, length=" + data.Length);
                try
                {
                    byte[] array = LZ4Helper.DecodeWithoutHeader(data);
                    text = AesCrypto.Decode(Encoding.ASCII.GetString(array));
                    __instance.SeqID++;
                    if (cmd.responseType != null)
                    {
                        object obj = JsonHelper.Deserialize(text, cmd.responseType);

                        // LOGGING
                        ResLogger.LogInfo($"{reqName}_Res");
                        string res = JsonConvert.SerializeObject(obj);
                        File.WriteAllText(Path.Combine(LoggingPath, reqName, $"{DateTime.Now.ToString("HH'h'-mm'm'-ss's'")}_res.json"), res);

                        __instance.ParseServerResponse(cmd, obj as IResponse);
                    }
                    else
                    {
                        Debug.Log("未設定DeserializeObject的類型, Request=" + cmd.serverRequest.GetType());
                    }
                    __instance.SendNextCommand();
                    yield break;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    __instance.WWWDecryptError(cmd);
                    yield break;
                }
            }
            yield break;
        }
    }
}
