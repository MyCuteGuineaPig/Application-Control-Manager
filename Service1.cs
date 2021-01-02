using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Program_Control
{
    public partial class Service1 : ServiceBase
    {
        private readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        Thread t1;
        public Service1()
        {
            InitializeComponent();
        }
        Control c = new Control();

        protected override void OnStart(string[] args)
        {
            log.Info("Process start...");
            t1 = new Thread(do_the_job);
            t1.IsBackground = true;
            t1.Start();
        }

        protected override void OnStop()
        {
            _shutdownEvent.Set();
            if (!t1.Join(10000))
                t1.Abort();
            log.Info("Process Stop");
        }

        private void do_the_job()
        {

            c.DoWork();
            while (!_shutdownEvent.WaitOne(1000 * 6))
            {
                c.DoWork();
                log.Info("Finish the job. Go to sleep...\n");
            }
        }
    }
}

