using System;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Web;
using System.Diagnostics;
using System.Text;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper;

namespace WindBot
{
    public class Program
    {
        internal static Random Rand;
        internal static bool ServerMode;

        internal static void Main(string[] args)
        {
            Logger.WriteLine("WindBot starting...");

            Config.Load(args);

            string databasePath = Config.GetString("DbPath", "cards.cdb");

            InitDatas(databasePath);

            ServerMode = Config.GetBool("ServerMode", false);

            if (ServerMode)
            {
                // Run in server mode, provide a http interface to create bot.
                int serverPort = Config.GetInt("ServerPort", 2399);
                RunAsServer(serverPort);
            }
            else
            {
                // Join the host specified on the command line.
                if (args.Length == 0)
                {
                    Logger.WriteErrorLine("=== WARN ===", null);
                    Logger.WriteLine("No input found, trying to connect to localhost YGOPro host.");
                    Logger.WriteLine("If it fails, the program will quit silently.");
                }
                RunFromArgs();
            }
        }

        public static void InitDatas(string databasePath)
        {
            Rand = new Random();
            DecksManager.Init();
            string absolutePath = Path.GetFullPath(databasePath);
            if (!File.Exists(absolutePath))
                // In case windbot is placed in a folder under ygopro folder
                absolutePath = Path.GetFullPath("../" + databasePath);
            if (!File.Exists(absolutePath))
                // In case windbot is placed in a folder under ygopro2 folder
                absolutePath = Path.GetFullPath("../cdb/" + databasePath);
            if (!File.Exists(absolutePath))
            {
                Logger.WriteErrorLine("Can't find cards database file.", null);
                Logger.WriteErrorLine("Please place cards.cdb next to WindBot.exe or Bot.exe .", null);
                Logger.WriteLine("Press any key to quit...");
                Console.ReadKey();
                System.Environment.Exit(1);
            }
            NamedCardsManager.Init(absolutePath);
        }

        private static void RunFromArgs()
        {
            WindBotInfo Info = new WindBotInfo();
            Info.Name = Config.GetString("Name", Info.Name);
            Info.Deck = Config.GetString("Deck", Info.Deck);
            Info.DeckFile = Config.GetString("DeckFile", Info.DeckFile);
            Info.Dialog = Config.GetString("Dialog", Info.Dialog);
            Info.Host = Config.GetString("Host", Info.Host);
            Info.Port = Config.GetInt("Port", Info.Port);
            Info.HostInfo = Config.GetString("HostInfo", Info.HostInfo);
            Info.Version = Config.GetInt("Version", Info.Version);
            Info.Hand = Config.GetInt("Hand", Info.Hand);
            Info.Debug = Config.GetBool("Debug", Info.Debug);
            Info.Chat = Config.GetBool("Chat", Info.Chat);
            Run(Info);
        }

        private static void RunAsServer(int ServerPort)
        {
            TcpListener mainServer = new TcpListener(IPAddress.Loopback, ServerPort);
            try
            {
                mainServer.Start();
                Logger.WriteLine("WindBot server start successed.");
                Logger.WriteLine("HTTP GET http://127.0.0.1:" + ServerPort + "/?name=WindBot&host=127.0.0.1&port=7911 to call the bot.");
                while (true)
                {
                    try
                    {
                        using (TcpClient client = mainServer.AcceptTcpClient())
                        {
                            HandleServerRequest(client);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (Debugger.IsAttached)
                            throw;
                        Logger.WriteErrorLine("Parse Http Request Error", ex);
                    }
                }
            }
            finally
            {
                mainServer.Stop();
            }
        }

        private static void HandleServerRequest(TcpClient client)
        {
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 5000;
            NetworkStream stream = client.GetStream();
            int statusCode = 400;

            try
            {
                string requestLine = ReadHttpLine(stream, 16384);
                string[] requestParts = requestLine.Split(' ');
                if (requestParts.Length != 3 || requestParts[0] != "GET")
                {
                    WriteHttpResponse(stream, 405);
                    return;
                }

                int totalHeaderLength = requestLine.Length;
                while (true)
                {
                    string headerLine = ReadHttpLine(stream, 16384 - totalHeaderLength);
                    totalHeaderLength += headerLine.Length;
                    if (headerLine.Length == 0)
                        break;
                }

                Uri requestUri;
                if (!Uri.TryCreate("http://127.0.0.1" + requestParts[1], UriKind.Absolute, out requestUri))
                {
                    WriteHttpResponse(stream, 400);
                    return;
                }

                var queryParams = HttpUtility.ParseQueryString(requestUri.Query);
                WindBotInfo Info = new WindBotInfo();
                Info.Name = queryParams.Get("name");
                Info.Deck = queryParams.Get("deck");
                Info.Host = queryParams.Get("host");
                string port = queryParams.Get("port");
                if (port != null)
                    Info.Port = Int32.Parse(port);
                string deckfile = queryParams.Get("deckfile"); // Obsoleted
                string dialog = queryParams.Get("dialog");
                if (dialog != null)
                    Info.Dialog = dialog;
                string version = queryParams.Get("version");
                if (version != null)
                    Info.Version = Int16.Parse(version);
                string password = queryParams.Get("password");
                if (password != null)
                    Info.HostInfo = password;
                string hand = queryParams.Get("hand");
                if (hand != null)
                    Info.Hand = Int32.Parse(hand);
                string debug = queryParams.Get("debug");
                if (debug != null)
                    Info.Debug = bool.Parse(debug);
                string chat = queryParams.Get("chat");
                if (chat != null)
                    Info.Chat = bool.Parse(chat);

                if (Info.Name == null || Info.Host == null || port == null || deckfile != null)
                {
                    statusCode = 400;
                }
                else if (Info.Deck != null && !DecksManager.HasDeck(Info.Deck))
                {
                    statusCode = 404;
                }
                else
                {
                    statusCode = 200;
                    Thread workThread = new Thread(new ParameterizedThreadStart(Run));
                    workThread.Start(Info);
                }
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                    throw;
                Logger.WriteErrorLine("Parse Http Request Error", ex);
                statusCode = 500;
            }

            WriteHttpResponse(stream, statusCode);
        }

        private static string ReadHttpLine(NetworkStream stream, int maximumLength)
        {
            if (maximumLength <= 0)
                throw new InvalidDataException("HTTP request headers are too large.");

            StringBuilder line = new StringBuilder();
            while (line.Length < maximumLength)
            {
                int value = stream.ReadByte();
                if (value < 0)
                    throw new EndOfStreamException("HTTP request ended before the headers were complete.");
                if (value == '\n')
                {
                    if (line.Length > 0 && line[line.Length - 1] == '\r')
                        line.Length--;
                    return line.ToString();
                }
                line.Append((char)value);
            }
            throw new InvalidDataException("HTTP request headers are too large.");
        }

        private static void WriteHttpResponse(NetworkStream stream, int statusCode)
        {
            string reason = statusCode == 200 ? "OK"
                : statusCode == 400 ? "Bad Request"
                : statusCode == 404 ? "Not Found"
                : statusCode == 405 ? "Method Not Allowed"
                : "Internal Server Error";
            byte[] response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 " + statusCode + " " + reason + "\r\n" +
                "Content-Length: 0\r\n" +
                "Connection: close\r\n" +
                "Cache-Control: no-store\r\n\r\n");
            stream.Write(response, 0, response.Length);
        }

        private static void Run(object o)
        {
            // All errors should be caught instead of causing the program to crash.
            GameClient client = null;
            try
            {
                WindBotInfo Info = (WindBotInfo)o;
                client = new GameClient(Info);
                Logger.SetContext(client.GetLogContext);
                client.Start();
                Logger.DebugWriteLine(client.Username + " started.");
                while (client.Connection.IsConnected)
                {
                    try
                    {
                        client.Tick();
                        #if DEBUG
                            Thread.Sleep(1);
                        #else
                            Thread.Sleep(30);
                        #endif
                    }
                    catch (Exception ex)
                    {
                        if (Debugger.IsAttached)
                            throw;
                        Logger.WriteErrorLine("Tick Error", ex);
                    }
                }
                Logger.DebugWriteLine(client.Username + " end.");
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                    throw;
                Logger.WriteErrorLine("Run Error", ex);
            }
            finally
            {
                Logger.ClearContext();
            }
        }

        public static FileStream ReadFile(string directory, string filename, string extension)
        {
            string tryfilename = filename + "." + extension;
            string fullpath = Path.Combine(directory, tryfilename);
            if (!File.Exists(fullpath))
                fullpath = filename;
            if (!File.Exists(fullpath))
                fullpath = Path.Combine("../", filename);
            if (!File.Exists(fullpath))
                fullpath = Path.Combine("../deck/", filename);
            if (!File.Exists(fullpath))
                fullpath = Path.Combine("../", tryfilename);
            if (!File.Exists(fullpath))
                fullpath = Path.Combine("../deck/", tryfilename);
            return new FileStream(fullpath, FileMode.Open, FileAccess.Read);
        }
    }
}
