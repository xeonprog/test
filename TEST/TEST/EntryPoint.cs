using System;
using Fclp;
//using System.Collections.Generic;

namespace SWW.GStats.Server
{
    class EntryPoint
    {
        public static void Main(string[] args)
        {
            var comLP = new FluentCommandLineParser<Options>();
            comLP
                .Setup(options => options.Prefix)
                .As("prefix")
               // .SetDefault("http://+:8080/") //administrator
                .SetDefault("http://127.0.0.1:8080/")
                .WithDescription("HTTP prefix to listen on");

            comLP
                .SetupHelp("h", "help")
                .WithHeader($"{AppDomain.CurrentDomain.FriendlyName} [--prefix <prefix>]")
                .Callback(text => Console.WriteLine(text));

            if (comLP.Parse(args).HelpCalled) { return; }

            RunServer(comLP.Object);
        }

        private static void RunServer(Options options)
        {
            using (var server = new StatServer())
            {
                server.Start(options.Prefix);
                Console.ReadKey(true);
            }
        }
        private class Options
        {
            public string Prefix { get; set; }
        }
    }
    //===========================================================================
}
