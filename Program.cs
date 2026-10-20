using System;
using System.IO;
using System.Threading;
using Skynet.Game;
using Skynet.Game.AI;
using YGOSharp.OCGWrapper;

namespace Skynet
{
    public class Program
    {
        internal static Random Rand;

        internal static int Main(string[] args)
        {
            if (args.Length == 0 || (args.Length == 1 && (args[0] == "--help" || args[0] == "-h")))
            {
                Console.WriteLine("SKYNET - adaptive duel intelligence");
                Console.WriteLine("Usage: SKYNET.exe Host=127.0.0.1 Port=7911 DbPath=cards.cdb [DeckFile=starter.ydk]");
                Console.WriteLine("Options use Key=Value. Config=path loads additional options from a file.");
                Console.WriteLine("Optional: Name, HostInfo, Version, Hand, Debug, Chat, Dialog.");
                Console.WriteLine("Agent=skynet (default) or Agent=expert-oldschool (frozen expert comparison).");
                Console.WriteLine("Learning: PolicyModel, PolicyTraining, PolicyData, PolicySeed, PolicyGroup.");
                Console.WriteLine("Each process joins one duel room. Chat and training are disabled by default.");
                return 0;
            }
            try
            {
                Logger.WriteLine("SKYNET 0.1 - adaptive duel intelligence");
                Config.Load(args);
                AgentOptions options = ReadOptions();
                InitDatas(Config.GetString("DbPath", "cards.cdb"));
                Run(options);
                return 0;
            }
            catch (Exception exception)
            {
                Logger.WriteErrorLine("SKYNET failed: " + exception.Message, exception);
                return 1;
            }
        }

        public static void InitDatas(string databasePath)
        {
            Rand = new Random();
            string[] candidates =
            {
                databasePath,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, databasePath),
                Path.Combine("..", databasePath),
                Path.Combine("../cdb", databasePath)
            };
            foreach (string candidate in candidates)
            {
                if (!File.Exists(candidate))
                    continue;
                NamedCardsManager.Init(Path.GetFullPath(candidate));
                return;
            }
            throw new FileNotFoundException("Card database not found. Set DbPath to a compatible cards.cdb.", databasePath);
        }

        private static AgentOptions ReadOptions()
        {
            AgentOptions options = new AgentOptions();
            options.Name = Config.GetString("Name", options.Name);
            options.Agent = Config.GetString("Agent", options.Agent);
            options.Deck = Config.GetString("Deck", options.Deck);
            options.DeckFile = Config.GetString("DeckFile", options.DeckFile);
            options.Dialog = Config.GetString("Dialog", options.Dialog);
            options.Host = Config.GetString("Host", options.Host);
            options.Port = Config.GetInt("Port", options.Port);
            options.HostInfo = Config.GetString("HostInfo", options.HostInfo);
            options.Version = Config.GetInt("Version", options.Version);
            options.Hand = Config.GetInt("Hand", options.Hand);
            options.Debug = Config.GetBool("Debug", options.Debug);
            options.Chat = Config.GetBool("Chat", options.Chat);
            options.PolicyModel = Config.GetString("PolicyModel", options.PolicyModel);
            options.PolicyTraining = Config.GetBool("PolicyTraining", options.PolicyTraining);
            options.PolicyData = Config.GetString("PolicyData", options.PolicyData);
            options.PolicySeed = Config.GetInt("PolicySeed", options.PolicySeed);
            options.PolicyGroup = Config.GetString("PolicyGroup", options.PolicyGroup);
            if (!string.Equals(options.Deck, "SKYNET", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Deck must be SKYNET. Select behavior with Agent and cards with DeckFile=path.ydk.");
            AgentFactory.ValidateOptions(options.Agent, options.PolicyTraining, options.PolicyModel);
            if (string.IsNullOrWhiteSpace(options.Host) || options.Port < 1 || options.Port > 65535)
                throw new ArgumentException("Host must be nonempty and Port must be between 1 and 65535.");
            return options;
        }

        private static void Run(AgentOptions options)
        {
            GameClient client = new GameClient(options);
            try
            {
                Logger.SetContext(client.GetLogContext);
                client.Start();
                Exception connectionError = null;
                client.Connection.Disconnected += error => connectionError = error;
                if (!client.Connection.IsConnected)
                    throw new IOException("Could not connect to the duel host.");
                while (client.Connection.IsConnected)
                {
                    client.Tick();
                    Thread.Sleep(1);
                }
                if (connectionError != null)
                    throw new IOException("The duel connection failed.", connectionError);
            }
            finally
            {
                if (client.Connection != null)
                    client.Connection.Close();
                Logger.ClearContext();
            }
        }

        public static FileStream ReadFile(string directory, string filename, string extension)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("A resource filename is required.", "filename");
            string withExtension = filename + "." + extension;
            string[] candidates =
            {
                Path.Combine(directory, withExtension),
                filename,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, directory, withExtension),
                Path.Combine("..", filename),
                Path.Combine("../deck", filename),
                Path.Combine("..", withExtension),
                Path.Combine("../deck", withExtension)
            };
            foreach (string candidate in candidates)
                if (File.Exists(candidate))
                    return new FileStream(candidate, FileMode.Open, FileAccess.Read);
            throw new FileNotFoundException("SKYNET resource not found: " + filename, filename);
        }
    }
}
