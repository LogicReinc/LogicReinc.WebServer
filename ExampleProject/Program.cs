using ExampleProject.Controllers;
using LogicReinc.WebSercer.Controllers;
using LogicReinc.WebServer;
using LogicReinc.WebServer.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExampleProject
{
    class Program
    {
        static void Main(string[] args)
        {
            Server server = new Server();
            server.Server.OnException += (m, x) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine($"[{DateTime.Now.ToString()}] EXCEPTION\n{x}\n{m}\n{x.Message}");
                Console.ResetColor();
            };
            server.Server.OnLog += (loc, log) => System.Console.WriteLine($"[{DateTime.Now.ToString()}] - {loc}: {log}");


            server.Start();
            bool active = false;
            while(active)
            {
                string command = Console.ReadLine();
                switch(command.Split(' ')[0])
                {
                    case "exit":
                        server.Stop();
                        active = false;
                        break;
                }
            }
        }
        
        public class Server : WebServer
        {
            public Server() : base(9990)
            {

            }

            public override void DefaultHandling(HttpRequest request)
            {
                request.ThrowCode(404, "Path not found");
            }

            public override void RegisterControllers()
            {
                Server.AddRoute<SecurityController<SecSets>>("/security");
                Server.AddRoute<ExampleController>("/example");
                Server.AddRoute<SyncController>("/sync");
            }

            public override void RegisterFiles()
            {
                Server.AddFile("/", "Files/Views/Index.html");
                Server.AddDirectory("/", "Files/");
            }

            public override void RegisterRoutes()
            {
                
            }
        }
    }
}
