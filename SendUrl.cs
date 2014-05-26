using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;

namespace becksend
{
    /// <summary>
    /// SendUrl 的摘要说明。
    /// </summary>
    public class SendUrl
    {
        public SendUrl()
        {
            //
            // TODO: 在此处添加构造函数逻辑
            //
        }
        public static String SendUrlGet(String url)
        {
            String str;
            try
            {

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                // Sends the HttpWebRequest and waits for the response.            
                HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                int len = (int)resp.ContentLength;
                // Gets the stream associated with the response.
                Stream receiveStream = resp.GetResponseStream();
                Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
                // Pipes the stream to a higher level stream reader with the required encoding format. 
                StreamReader readStream = new StreamReader(receiveStream, encode);
                //Console.WriteLine("\r\nResponse stream received.");
                Char[] read = new Char[len];
                // Reads 256 characters at a time.    
                int count = readStream.Read(read, 0, len);
                // Dumps the 256 characters on a string and displays the string to the console.
                str = new String(read, 0, count);
                // Releases the resources of the response.
                resp.Close();
                // Releases the resources of the Stream.
                readStream.Close();
                return str;
            }
            catch (Exception es)
            {
                return es.Message;
            }
        }
        //public String SendUrlPost(String url);
    }
}
