using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace InteractiveService
{
    class Service : ServiceBase
    {
        private string[] args;
        private ServiceHost serviceHost = new();

        public Service(string[] args)
        {
            this.args = args;
        }

        protected override void OnStart(string[] args)
            => CommandLineArgs.Invoke(args.Length != 0 ? args : this.args, c => serviceHost.Start(c));

        protected override void OnStop()
        {
            serviceHost.Stop();
            serviceHost.WaitForExit();
        }
    }
}
