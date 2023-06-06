using CMSISDAP;
using System;
using System.Threading.Tasks;
namespace CustomOnChipDebuggerConsoleApp
{
    public class Server
    {
        static async Task Main(string[] args)
        {
            var adapter = new CMSISDAPAdapter();
            var jtagDriver = new Ftdi4232HJtag("210249A061AE");
            jtagDriver.Initialize();
            var debugTarget = new DebugTarget();
            var server = new GDBNetworkServer(debugTarget, 11000);
            await Task.Run(() => server.StartServer());
        }
    }
}