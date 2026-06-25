using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Networking;
using VRSYS.Core.Logging;

namespace VRSYS.Scripts.Recording
{
    public class NetworkUtils
    {
        // NTP servers are queried in order; the first one to respond wins. Public pools
        // rate-limit aggressively, so the clock is queried at most once per resync interval
        // (see SynchronizeClock) rather than on every frame.
        private static readonly string[] NtpServers = { "pool.ntp.org", "time.google.com", "time.windows.com" };
        private const int NtpTimeoutMs = 3000;
        private static readonly TimeSpan MinResyncInterval = TimeSpan.FromSeconds(30);

        // Cached offset between NTP time and the local system clock. Once synchronized, the
        // global time is derived locally (GetSynchronizedTime) without touching the network.
        private static TimeSpan _ntpOffset = TimeSpan.Zero;
        private static bool _isClockSynchronized;
        private static DateTime _lastSyncUtc = DateTime.MinValue;

        /// <summary>
        /// Returns the synchronized (NTP-corrected) local time using the cached offset.
        /// This performs no network I/O and is safe to call every frame / inside loops.
        /// If the clock has never been synchronized it falls back to the local system clock.
        /// </summary>
        public static DateTime GetSynchronizedTime()
        {
            return DateTime.Now + _ntpOffset;
        }

        /// <summary>
        /// Refreshes the cached NTP offset. Throttled to <see cref="MinResyncInterval"/> unless
        /// <paramref name="force"/> is set, so it can be called freely without flooding NTP servers.
        /// This issues a blocking UDP query, so call it off the main thread.
        /// Returns true if a valid synchronization is available (fresh or previously cached).
        /// </summary>
        public static bool SynchronizeClock(bool force = false)
        {
            if (!force && _isClockSynchronized && DateTime.UtcNow - _lastSyncUtc < MinResyncInterval)
                return true;

            if (TryQueryNtp(out DateTime ntpLocalTime))
            {
                _ntpOffset = ntpLocalTime - DateTime.Now;
                _isClockSynchronized = true;
                _lastSyncUtc = DateTime.UtcNow;
                return true;
            }

            // Keep the previous offset if a refresh fails rather than reverting to the raw clock.
            return _isClockSynchronized;
        }

        // see NTP query from https://stackoverflow.com/questions/1193955/how-to-query-an-ntp-server-using-c
        private static bool TryQueryNtp(out DateTime ntpLocalTime)
        {
            ntpLocalTime = DateTime.Now;

            foreach (var ntpServer in NtpServers)
            {
                try
                {
                    // NTP message size - 16 bytes of the digest (RFC 2030)
                    var ntpData = new byte[48];

                    //Setting the Leap Indicator, Version Number and Mode values
                    ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

                    var addresses = Dns.GetHostEntry(ntpServer).AddressList;

                    //The UDP port number assigned to NTP is 123
                    var ipEndPoint = new IPEndPoint(addresses[0], 123);
                    //NTP uses UDP

                    using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    {
                        socket.Connect(ipEndPoint);
                        socket.Send(ntpData);

                        // Explicitly wait for the reply with Poll instead of relying on ReceiveTimeout.
                        // Under Unity's Mono runtime, ReceiveTimeout puts the socket in non-blocking mode
                        // and a missing reply surfaces as WSAEWOULDBLOCK ("Operation on non-blocking
                        // socket would block") rather than a clean timeout. Poll avoids that noise.
                        if (!socket.Poll(NtpTimeoutMs * 1000, SelectMode.SelectRead))
                        {
                            ExtendedLogger.LogWarning(nameof(NetworkUtils), "NTP server '" + ntpServer + "' did not respond within " +
                                             NtpTimeoutMs + " ms. Trying next server.");
                            continue;
                        }

                        socket.Receive(ntpData);
                    }

                    //Offset to get to the "Transmit Timestamp" field (time at which the reply
                    //departed the server for the client, in 64-bit timestamp format."
                    const byte serverReplyTime = 40;

                    //Get the seconds part
                    ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

                    //Get the seconds fraction
                    ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

                    //Convert From big-endian to little-endian
                    intPart = SwapEndianness(intPart);
                    fractPart = SwapEndianness(fractPart);

                    var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

                    //**UTC** time
                    var networkDateTime =
                        (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

                    ntpLocalTime = networkDateTime.ToLocalTime();
                    return true;
                }
                catch (Exception exception)
                {
                    ExtendedLogger.LogWarning(nameof(NetworkUtils), "Could not synchronize time via '" + ntpServer + "': " + exception.Message);
                }
            }

            ExtendedLogger.LogError(nameof(NetworkUtils), "Could not synchronize time via any NTP server. Falling back to the local system clock.");
            return false;
        }

        // stackoverflow.com/a/3294698/162671
        private static uint SwapEndianness(ulong x)
        {
            return (uint) (((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }

        public static IEnumerator UploadToServer(string projectName, string filePath, string fileName, string serverAddress)
        {

            while (!File.Exists(filePath))
            {
                ExtendedLogger.LogError(nameof(NetworkUtils), "The file: " + filePath + " does not exist.");
                yield return new WaitForSeconds(0.01f);
            }

            string url = serverAddress + "/upload/" +  projectName + "/" + fileName;
            ExtendedLogger.LogInfo(nameof(NetworkUtils), "Upload file to: " + url);

            using (var uwr = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
            {
                uwr.uploadHandler = new UploadHandlerFile(filePath);
                yield return uwr.SendWebRequest();
                if (uwr.result != UnityWebRequest.Result.Success)
                    ExtendedLogger.LogError(nameof(NetworkUtils), uwr.error);
                else
                {
                    // file data successfully sent
                }
            }
        }

        public static IEnumerator DeleteRecording(string projectName, string recordingName, string serverAddress, string password, Action<bool, string> onComplete = null)
        {
            string url = serverAddress + "/delete_recording/" +
                         UnityWebRequest.EscapeURL(projectName) + "/" +
                         UnityWebRequest.EscapeURL(recordingName);
            ExtendedLogger.LogInfo(nameof(NetworkUtils), "Delete recording: " + url);

            using (var uwr = new UnityWebRequest(url, UnityWebRequest.kHttpVerbDELETE))
            {
                uwr.downloadHandler = new DownloadHandlerBuffer();
                uwr.SetRequestHeader("X-Auth-Password", password);

                yield return uwr.SendWebRequest();

                bool success = uwr.result == UnityWebRequest.Result.Success;
                string message = success ? uwr.downloadHandler.text : uwr.error;

                if (!success)
                    ExtendedLogger.LogError(nameof(NetworkUtils), "Delete failed (" + uwr.responseCode + "): " + message);
                else
                    ExtendedLogger.LogInfo(nameof(NetworkUtils), "Delete succeeded: " + message);

                onComplete?.Invoke(success, message);
            }
        }

        public static IEnumerator DownloadProjectZip(string projectName, string serverAddress, string password, string savePath, Action<bool, string> onComplete = null)
        {
            string url = serverAddress + "/download_zip/" + UnityWebRequest.EscapeURL(projectName);
            ExtendedLogger.LogInfo(nameof(NetworkUtils), "Download project zip: " + url);

            string parentDir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                Directory.CreateDirectory(parentDir);

            using (var uwr = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET))
            {
                uwr.downloadHandler = new DownloadHandlerFile(savePath);
                uwr.SetRequestHeader("X-Auth-Password", password);

                yield return uwr.SendWebRequest();

                bool success = uwr.result == UnityWebRequest.Result.Success;
                string message = success ? savePath : (uwr.responseCode + ": " + uwr.error);

                if (!success)
                {
                    ExtendedLogger.LogError(nameof(NetworkUtils), "Download failed (" + uwr.responseCode + "): " + uwr.error);
                    if (File.Exists(savePath))
                    {
                        try { File.Delete(savePath); }
                        catch (IOException) { /* leave partial file if locked */ }
                    }
                }
                else
                {
                    ExtendedLogger.LogInfo(nameof(NetworkUtils), "Download succeeded: " + savePath);
                }

                onComplete?.Invoke(success, message);
            }
        }

    }
}