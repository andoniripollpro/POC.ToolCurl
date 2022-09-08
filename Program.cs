using System;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using AndoIt.Common;
using AndoIt.Common.Common;
using CurlThin;
using CurlThin.Enums;
using CurlThin.Helpers;
using CurlThin.Native;
using CurlThin.SafeHandles;

namespace POC.ToolCurl
{
    class Program
    {
        static void Main(string[] args)
        {
            DateTime compillationDateTime = Assembly.GetAssembly(typeof(Program)).GetLinkerTime();
            Console.WriteLine($"Versión: {compillationDateTime}");
            Console.WriteLine($"MachineName: '{Environment.MachineName}'. OSVersion: '{Environment.OSVersion}'.");
            string cipherList = null;
            if (args.Length < 1)
            {
                ConsoleWriteInRed("Error en parámetros: ");
                Console.WriteLine();

                WriteHelp();
                return;
            }
            else if (args[0] == "-?" || args[0] == "-h" || args[0] == "--help")
            {
                WriteHelp();
                return;
            }

            if (args.Length > 2)
            {
                if (args[1] == "-c" || args[1] == "--cipher")
                    cipherList = args[2];
                else
                {
                    ConsoleWriteInRed($"Error en parámeto opciones.");
                    Console.WriteLine($"'{args[1]}' not valid");
                    WriteHelp();
                }
            }

            CallUrl(args[0], cipherList);

            Console.WriteLine("Llamada finalizada");            
        }

        private static void ConsoleWriteInRed(string text)
        {
            ConsoleColor redColor = ConsoleColor.Red;
            ConsoleColor defaultForeColor = Console.ForegroundColor;
            Console.ForegroundColor = redColor;
            Console.Write(text);
            Console.ForegroundColor = defaultForeColor;
        }

        private static void CallUrl(string url, string cipherList = null)
        {
            Console.WriteLine($"ServicePointManager={ServicePointManager.SecurityProtocol.ToString()}");
            Console.WriteLine("ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            Console.WriteLine($"ServicePointManager={ServicePointManager.SecurityProtocol.ToString()}");

            if (cipherList != null)
            {
                SetCipherList(url, cipherList);
            }

            var httpClient = new HttpClientAdapter();
            var tracer = new HttpEventListener();
            tracer.EnableEvents(new EventSource("System.Net.Http.HttpClient"), EventLevel.Verbose);
            var result = httpClient.StandardGet(url);
            tracer.Dispose();
            tracer = null;

            Console.WriteLine($"-> Resultado:");
            Console.WriteLine($"  -> StatusCode: {(int)result.StatusCode} {result.StatusCode}.");
            Console.WriteLine($"  -> Headers: {result.Headers}.");
            Console.WriteLine($"  -> Content: '{result.Content.ReadAsStringAsync().Result}'");
            Console.WriteLine($"  -> RequestMessage: '{result.RequestMessage}'");

            //Console.WriteLine("Versión con CurlThinWrapper.");
            //var httpSender = new HttpCulrThinWrapper(new LogConsoleWrapper(LogConsoleWrapper.LogLevel.Debug));
            //httpSender.CookedUpGet(url);
        }

        private static string SetCipherList(string url, string cipherList)
        {
            Console.WriteLine($"-> CipherList: {cipherList}");

            //This string is for extracting libcurl and ssl libs to the bin directory.
            CurlResources.Init();
            var global = CurlNative.Init();
            var easy = CurlNative.Easy.Init();
            string content = string.Empty;

            try
            {
                var dataCopier = new DataCallbackCopier();

                CurlNative.Easy.SetOpt(easy, CURLoption.URL, url);
                CurlNative.Easy.SetOpt(easy, CURLoption.WRITEFUNCTION, dataCopier.DataHandler);
                //This string is needed when you call a https endpoint.
                CurlNative.Easy.SetOpt(easy, CURLoption.CAINFO, CurlResources.CaBundlePath);

                var headers = CurlNative.Slist.Append(SafeSlistHandle.Null, "Authorization: Bearer blablabla");
                CurlNative.Easy.SetOpt(easy, CURLoption.HTTPHEADER, headers.DangerousGetHandle());
                //Your set of ciphers, full list is here https://curl.se/docs/ssl-ciphers.html
                CurlNative.Easy.SetOpt(easy, CURLoption.SSL_CIPHER_LIST, cipherList);

                var result = CurlNative.Easy.Perform(easy);

                Console.WriteLine($"Result code: {result}.");
                Console.WriteLine();
                //Console.WriteLine("Response body:");
                //Console.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));

                int a;
                var curlInfo = CurlNative.Easy.GetInfo(easy, CURLINFO.TLS_SESSION, out a);
                Console.WriteLine($"  -> CurlNative.Easy.GetInfo = {curlInfo.ToString()}");

                content = Encoding.UTF8.GetString(dataCopier.Stream.ToArray());
                Console.WriteLine($"  -> dataCopier.Stream = '{content}'.");
            }
            catch (Exception e)
            {
                ConsoleWriteInRed($"Excepción al hacer SetCipherList. Excepción: {e.Message}.");
                Console.WriteLine();
                Console.WriteLine(e);
            }
            finally
            {
                easy.Dispose();

                if (global == CURLcode.OK)
                    CurlNative.Cleanup();
            }

            return content;
        }

        private static void WriteHelp()
        {
            Console.WriteLine("Sintaxis:");
            Console.WriteLine(" POC.ToolCurl (--help | <url>) [options]");
            Console.WriteLine("Options:");
            Console.WriteLine(" --cipher <cipherList separadas con ':' >");
        }
    }
}
