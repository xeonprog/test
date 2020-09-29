using System;
using System.Text;
using System.IO;
using System.Net;

namespace SWW.GStats.Common
{
    public class ClassCommon
    {
        public static string ShowRequestData(HttpListenerRequest request)
        {
            if (!request.HasEntityBody)
                return string.Empty;
            Stream body = request.InputStream;
            Encoding encoding = request.ContentEncoding;
            StreamReader reader = new StreamReader(body, encoding);
            string s = reader.ReadToEnd();
            body.Close();
            reader.Close();
            return s;
        }
        internal static void WriteLine(string LogText, params object[] arg)
        {
            String str = String.Format(LogText, arg);
            File.AppendAllText("log.txt", str); ;
        }
        public static bool isCorrectCountForReport(string lastSegment, string typeReport, out int count)
        {
            bool isCorrectCount;
            if (lastSegment == typeReport)
            {
                count = 5;
                isCorrectCount = true;
            }
            else
            {
                isCorrectCount = int.TryParse(lastSegment, out count);
                if (count > 50)
                    count = 50;
                else if (count < 0)
                    count = 0;
            }
            return isCorrectCount;
        }
    }
}
//=====================================================================================================