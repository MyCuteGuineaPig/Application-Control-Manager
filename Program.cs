using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;


[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace Program_Control
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            if (Environment.UserInteractive) ConsoleMain();
            else ServiceMain();
        }
        private readonly static ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        static void ConsoleMain()
        {
            Control c = new Control();
            while (true){
                c.DoWork();
                log.Info("finish job.....\n");
                Thread.Sleep(TimeSpan.FromSeconds(10));
                
            }
            
            
        }

        static void ServiceMain()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Service1()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
