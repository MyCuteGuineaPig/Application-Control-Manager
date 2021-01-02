using log4net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Program_Control
{
    class Control
    {
        private Dictionary<string, string> service_status = new Dictionary<string, string>();
        // key is service name, value is running/stopped

        private Dictionary<string, string> service_request = new Dictionary<string, string>();
        //key is service name, value is pending .... start /stop / restart 
        private readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private HashSet<string> restart_error = new HashSet<string>();
        //want to restart, but fail to kill, so even if currently is running, but fail to restart


        private StringBuilder sb_running = new StringBuilder();
        private StringBuilder sb_stopped = new StringBuilder();

        /**
        Need to change here
        
         */
        private Dictionary<string, string> process_map = new Dictionary<string, string>(){ //key is the name, value is service name
            { "program1", "service_for_program1"},

        };

        private HashSet<string> service_starting = new HashSet<string>();



        private Mysql mysql = new Mysql();
        public void DoWork() {
            service_status = new Dictionary<string, string>();
            service_request = new Dictionary<string, string>();
            restart_error = new HashSet<string>();

            DataTable df = new DataTable();
            mysql.fetches("SELECT * FROM web.program_status;", ref df);

           
            sb_running = new StringBuilder("UPDATE web.program_status SET `status` = 'running' WHERE"); //prevent when web request, but 
            sb_stopped = new StringBuilder("UPDATE web.program_status SET `status` = 'stopped' WHERE");

            foreach (DataRow rows in df.Rows) {
                string service = rows["service_name"].ToString();
                string status = rows["status"].ToString();
                service_status[service] = status;

                if (status.Contains("pending"))
                {
                    service_request[service] = status;
                    log.Info(service + " operation:  " + status);
                }
            }

            log.Info("Do operation.....");
            bool has_starting = false;
            foreach (var it in service_request) {
                var service = it.Key;
                var command = it.Value;
                string current_status = checkStatus(service);

                if (current_status == "running" && !service_starting.Contains(service))
                {
                    if (command == "pending stopped")
                    {
                        log.Info("try kill it");
                        KillService(service);
                    }
                    if (command == "pending restart")
                        RestartService(service);
                }
                else if (current_status == "stopped")
                {
                    if (command == "pending start")
                        StartService(service);
                    else if (command == "pending restart")
                        StartService(service);
                }
                else if (current_status == "starting") { 
                    service_starting.Add(service);
                    has_starting = true;
                }
            }
            





            //check if any process have two running at the same time, key is processName
            Dictionary<string, int> service_check = new Dictionary<string, int>();
            foreach (var service in service_status)
            {
                foreach(var processName in service_map[service.Key]) {
                    foreach (var process in Process.GetProcessesByName(processName))
                    {
                        if (service_check.ContainsKey(service.Key))
                            service_check[processName] += 1;
                        else
                            service_check[processName] = 1;
                    }
                }
            }

            foreach (var process in service_check) // kill process if have the same name
            { //if any process has multiple running, find service, then find all child process to kill
                var processName = process.Key;
                if (process.Value == 1) continue;

                var serviceName = process_map[processName];
                foreach (var process_child_name in service_map[serviceName])
                {
                    bool errorflag = false;
                    foreach (var process_child_ in Process.GetProcessesByName(process_child_name))
                    {
                        try
                        {
                            process_child_.Kill();
                        }
                        catch (Exception e)
                        {
                            errorflag = true;
                            log.Error("(Multiple processes) Error for kill  serviceName "+ serviceName + " processName " + processName + ". Error Message:  " + e.Message);
                        }
                    }
                    if (!errorflag)
                    {
                        log.Error("(Multiple processes) Restart the  serviceName " + serviceName + " processName " + process_child_name);
                        StartService(serviceName);
                    }
                }
            }


            Thread.Sleep(500);
            log.Info("check status.....");
            foreach (var it in service_status)
            {
                var service = it.Key;
               
                var status = it.Value;

                string current_status = checkStatus(service);

                if (restart_error.Contains(service))
                {
                    log.Error(service + " has error for restarting ");
                    continue;
                }
                if (service_request.ContainsKey(service))
                {
                    if (current_status == "running" && (service_request[service] == "pending start" || service_request[service] == "pending restart"))
                    {
                        sb_running.Append(" (  service_name = '" + service + "' and `status` = '"+ service_request[service] + "') or ");
                    }
                    else if(current_status == "stopped" && service_request[service] == "pending stopped")
                    {
                        sb_stopped.Append(" (  service_name = '" + service + "' and `status` = '" + service_request[service] + "') or ");
                    }
                }
                else { 
                    if (current_status == "running" && status!= "running")
                    {
                        sb_running.Append(" (  service_name = '" + service + "' and `status` = '" + status + "') or ");
                    }
                    else if (current_status == "stopped" && status != "stopped")
                    {
                        sb_stopped.Append(" (  service_name = '" + service + "' and `status` = '" + status + "') or ");
                    }
                   /* else if (current_status == "paused")
                    {
                        sb_status.Append(" ('" + service + "', 'paused'),");
                    }*/
                }
            }

            
            if (sb_running[sb_running.Length - 1] != 'E')
            {
                sb_running.Remove(sb_running.Length - 4, 4); sb_running.Append(";");
                log.Info(sb_running.ToString());
                mysql.insert(sb_running.ToString(), " change to running ");
            }

            if (sb_stopped[sb_stopped.Length - 1] != 'E')
            {
                sb_stopped.Remove(sb_stopped.Length - 4, 4); sb_stopped.Append(";");
                log.Info(sb_stopped.ToString());
                mysql.insert(sb_stopped.ToString(), " change to stopped ");
            }

            if (!has_starting)
            {
                service_starting = new HashSet<string>();
            }

        }

        private string checkStatus(string serviceName) {
            ServiceController sc = new ServiceController(serviceName);

            switch (sc.Status)
            {
                case ServiceControllerStatus.Running:
                    log.Info(serviceName + " is running now ");
                    return "running";
                case ServiceControllerStatus.Stopped:
                    log.Info(serviceName + " stopped now ");
                    return "stopped";
                case ServiceControllerStatus.Paused:
                    log.Info(serviceName + " is paused now ");
                    return "paused";
                case ServiceControllerStatus.StopPending:
                    log.Info(serviceName + " is stopping now ");
                    return "stopping";
                case ServiceControllerStatus.StartPending:
                    log.Info(serviceName + " is starting now ");
                   
                    return "starting";
                default:
                    return "unknown";
            }

        }



        private void StartService(string serviceName, int timeoutMilliseconds = 1500)
        {
            ServiceController service = new ServiceController(serviceName);
            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                log.Info("Successfully start " + serviceName);
               
            }
            catch (Exception e)
            {
                log.Error("Error for start service. Error Message:  " + e.Message);
            }
        }

        private void KillService(string serviceName, int timeoutMilliseconds = 1500) {


            ServiceController service = new ServiceController(serviceName);
            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                log.Info("successfully stop service " + serviceName);

            }
            catch (Exception e)
            {
                // ...
                log.Warn("Cannot Stop Service " + serviceName);
            }

            foreach (var processName in service_map[serviceName]) {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        process.Kill();

                        log.Info("successfully kill service " + serviceName + " processName "+processName );
                    }
                    catch (Exception e) {
                        log.Error("Error for kill " + serviceName + " processName " + processName + ". Error Message:  " + e.Message);
                    }
                }
            }
        }

        private void RestartService(string serviceName, int timeoutMilliseconds = 1500) {

            ServiceController service = new ServiceController(serviceName);
            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                log.Info("successfully stop service (for restart) " + serviceName);
            }
            catch (Exception e)
            {
                // ...
                log.Warn("Cannot Stop Service " + serviceName);
            }
            Thread.Sleep(300);

            foreach (var processName in service_map[serviceName])
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        process.Kill();
                        log.Info("successfully kill service (for restart) " + serviceName + " processName " + processName);
                    }
                    catch (Exception e)
                    {
                        log.Error("Error for kill " + serviceName + " processName " + processName + ". Error Message:  " + e.Message);
                        restart_error.Add(serviceName);
                    }
                }
            }

            Thread.Sleep(300);

            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(2000);

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                log.Info("Successfully start service (for restart) " + serviceName);
            }
            catch (Exception e)
            {
                log.Error("Error for Restarting(Start) service. Error Message:  " + e.Message);
            }
        }

    }
}
