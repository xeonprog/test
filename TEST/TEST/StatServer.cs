using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using SWW.GStats.Common;


namespace SWW.GStats.Server
{
    internal class StatServer : IDisposable
    {
        public StatServer()
        {
            listener = new HttpListener();
            AccessData AccessData = new AccessData("StatisticDb");
        }
        public void Start(string prefix)
        {
           // Console.WriteLine("Start {0}", prefix);
            lock (listener)
            {
                if (!isRunning)
                {
                    listener.Prefixes.Clear();
                    listener.Prefixes.Add(prefix);
                    listener.Start();

                    listenerThread = new Thread(Listen)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.Highest
                    };
                    listenerThread.Start();

                    isRunning = true;
                }
            }
        }
        public void Stop()
        {
            lock (listener)
            {
                if (!isRunning)
                    return;

                listener.Stop();
                listenerThread.Abort();
                listenerThread.Join();

                isRunning = false;
            }
        }
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            Stop();

            listener.Close();
        }
        private void Listen()
        {
            while (true)
            {
                try
                {
                    if (listener.IsListening)
                    {
                        var context = listener.GetContext();

                 //       Console.WriteLine("context= {0} | {1}", context.Request, context.Request.Headers);

                        Task.Run(() => HandleContextAsync(context));
                    }
                    else Thread.Sleep(0);
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (Exception error)
                {
                    ClassCommon.WriteLine("=>{0} An Error occurred: {1}  Message: {2} {3}",
                        DateTime.Now, error.StackTrace, error.Message, Environment.NewLine);
                }
            }
        }
        private async Task HandleContextAsync(HttpListenerContext listenerContext)
        {
            string rawUrl = listenerContext.Request.RawUrl;
            HttpListenerRequest request = listenerContext.Request;
            HttpListenerResponse response = listenerContext.Response;
            int segmentsCount = request.Url.Segments.Length;

         //   Console.WriteLine("rawUrl: {0} | {1} | {2} | {3}", rawUrl, request.HttpMethod, response, segmentsCount);

            if (request.HttpMethod == "PUT")
            {
                int statusCode;
                if (new Regex(@"/servers/(\S+)/info(/?$)", RegexOptions.IgnoreCase).IsMatch(rawUrl))
                {

                    string input = ClassCommon.ShowRequestData(request);
                    string endpoint = request.Url.Segments[segmentsCount - 2].Replace("/", "");
                    statusCode = AccessData.PutServerInfo(endpoint, input);

            //        Console.WriteLine("input: {0} | endpoint {1} | statusCode {2} ", input, endpoint, statusCode);
                }
                else if (new Regex(@"/servers/(\S+)/matches/", RegexOptions.IgnoreCase).IsMatch(rawUrl))
                {
                    string input = ClassCommon.ShowRequestData(request);
                    string endpoint = request.Url.Segments[segmentsCount - 3].Replace("/", "");
                    string matchTimestamp = request.Url.Segments[segmentsCount - 1].Replace("/", "").Replace("Z", "").Replace("T", " ");
                    statusCode = AccessData.PutMatchInfo(endpoint, matchTimestamp, input);
        //            Console.WriteLine("input: {0} | {1} | {2}", input, endpoint, matchTimestamp);
                }
                else
                {
                    statusCode = 400;
                }

                response.StatusCode = statusCode;

        //        Console.WriteLine("report {0} statusCode= {1}", response.OutputStream, response.StatusCode);

                using (var writer = new StreamWriter(response.OutputStream))
                    writer.WriteLine(string.Empty);
            }
            else if (request.HttpMethod == "GET")
            {
                string responseStr = string.Empty;
                if (new Regex(@"/servers/(\S+)/matches/", RegexOptions.IgnoreCase).IsMatch(rawUrl))
                {
                    string endpoint = request.Url.Segments[segmentsCount - 3].Replace("/", "");
                    string matchTimestamp = request.Url.Segments[segmentsCount - 1].Replace("/", "").Replace("Z", "").Replace("T", " ");
                    responseStr = AccessData.GetMatchInfo(endpoint, matchTimestamp);
                }
                else if (new Regex(@"/servers/info(/?$)", RegexOptions.IgnoreCase).IsMatch(rawUrl))
                {
                    responseStr = AccessData.GetAllServersInfo();
                }
                else if (new Regex(@"/servers/(\S+)/info(/?$)", RegexOptions.IgnoreCase).IsMatch(rawUrl))
                {
                    string endpoint = request.Url.Segments[segmentsCount - 2].Replace("/", "");
                    responseStr = AccessData.GetServerInfo(endpoint);
                }
                else if (new Regex(@"/servers/(\S+)/stats(/?$)", RegexOptions.IgnoreCase).IsMatch(rawUrl))
                {
                    string endpoint = request.Url.Segments[segmentsCount - 2].Replace("/", "");
                    responseStr = AccessData.GetServerStats(endpoint);
                }
                else if (new Regex(@"/players/(\S+)/stats(/?$)", RegexOptions.IgnoreCase).IsMatch(rawUrl))
                {
                    string playerName = request.Url.Segments[segmentsCount - 2].Replace("/", "");
                    responseStr = AccessData.GetPlayerStats(playerName);
                }
                else if (new Regex(@"/reports/recent-matches", RegexOptions.IgnoreCase).IsMatch(rawUrl))
                {
                    int count;
                    string lastSegment = request.Url.Segments[segmentsCount - 1].Replace("/", "").ToLower();
                    bool isCorrectCount = ClassCommon.isCorrectCountForReport(lastSegment, "recent-matches", out count);
                    if (isCorrectCount)
                        responseStr = AccessData.RecentMatches(count);
                }
                else if (new Regex(@"/reports/best-players", RegexOptions.IgnoreCase).IsMatch(rawUrl))
                {
                    int count;
                    string lastSegment = request.Url.Segments[segmentsCount - 1].Replace("/", "").ToLower();
                    bool isCorrectCount = ClassCommon.isCorrectCountForReport(lastSegment, "best-players", out count);
                    if (isCorrectCount)
                        responseStr = AccessData.BestPlayers(count);
                }
                else if (new Regex(@"/reports/popular-servers", RegexOptions.IgnoreCase).IsMatch(rawUrl))
                {
                    int count;
                    string lastSegment = request.Url.Segments[segmentsCount - 1].Replace("/", "").ToLower();
                    bool isCorrectCount = ClassCommon.isCorrectCountForReport(lastSegment, "popular-servers", out count);
                    if (isCorrectCount)
                        responseStr = AccessData.PopularServers(count);
                }
                response.StatusCode = (responseStr == string.Empty) ? (int)HttpStatusCode.NotFound : (int)HttpStatusCode.OK;
                using (var writer = new StreamWriter(response.OutputStream))
                    await writer.WriteLineAsync(responseStr);
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                using (var writer = new StreamWriter(listenerContext.Response.OutputStream))
                    writer.WriteLine(string.Empty);
            }
        }
        private readonly HttpListener listener;
        private Thread listenerThread;
        private bool disposed;
        private volatile bool isRunning;
    }

}