using System.Threading.Tasks;

namespace CustomOnChipDebuggerConsoleApp
{
    internal class Server
    {
        static async Task Main(string[] args)
        {
            var debugTarget = new DebugTarget();
            var server = new GDBNetworkServer(debugTarget, 11000);
            //await Task.Run(() => server.StartServer());
        }
    }
}