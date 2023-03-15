using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CustomOnChipDebuggerConsoleApp
{
    public class GDBNetworkServer : IDisposable
    {
        private readonly ASCIIEncoding _encoder = new ASCIIEncoding();

        private TcpListener _listener;
        private Process myGdbClientProc;
        private readonly IDebugTarget _target;
        private readonly int _port;

        private readonly object _clientsLock = new object();
        private readonly List<TcpClient> _clients = new List<TcpClient>();

        public GDBNetworkServer(IDebugTarget target, int port)
        {
            _target = target;
            _port = port;
        }

        public async Task StartServer()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            Console.WriteLine("GDB RSP Server started on port " + 11000);
            await WaitForClients();
        }

        public async Task Breakpoint(Breakpoint breakpoint)
        {
            // We do not need old breakpoints because GDB will set them again
            _target.ClearBreakpoints();

            await SendGlobal(GDBSession.FormatResponse(GDBSession.StandartAnswers.Breakpoint));
        }

        private async Task SendGlobal(string message)
        {
            List<TcpClient> connectedClients;

            lock (_clientsLock)
            {
                connectedClients = _clients.Where(c => c.Connected).ToList();
            }

            await Task.WhenAll(connectedClients.Select(c => SendResponse(c.GetStream(), message)));
        }

        private async Task WaitForClients()
        {
            try
            {
                while (true)
                {
                    string folder = @"C:\cccs-sw-tools\riscv\S500_CSSI";
                    var processStartInfo = new ProcessStartInfo();
                    processStartInfo.WorkingDirectory = folder;
                    processStartInfo.FileName = "cmd.exe";
                    processStartInfo.Arguments = "/C Start_GDB.bat";
                    myGdbClientProc = Process.Start(processStartInfo);
                    TcpClient client = _listener.AcceptTcpClient();
                    Console.WriteLine("Client connected from " + client.Client.RemoteEndPoint.ToString());

                    lock (_clientsLock)
                    {
                        _clients.Add(client);
                        _clients.RemoveAll(c => !c.Connected);
                    }

                    await ProcessGdbClient(client);
                }
            }
            catch (Exception ex)
            {
                _target.LogException?.Invoke(ex);
            }
        }

        private async Task ProcessGdbClient(TcpClient tcpClient)
        {
            NetworkStream clientStream = tcpClient.GetStream();
            GDBSession session = new GDBSession(_target);

            byte[] message = new byte[0x1000];
            int bytesRead;

            //_target.DoStop();

            while (true)
            {
                try
                {
                    bytesRead = await clientStream.ReadAsync(message, 0, 4096);
                }
                catch (IOException iex)
                {
                    var sex = iex.InnerException as SocketException;
                    if (sex == null || sex.SocketErrorCode != SocketError.Interrupted)
                    {
                        _target.LogException?.Invoke(sex);
                    }
                    break;
                }
                catch (SocketException sex)
                {
                    if (sex.SocketErrorCode != SocketError.Interrupted)
                    {
                        _target.LogException?.Invoke(sex);
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _target.LogException?.Invoke(ex);
                    break;
                }

                if (bytesRead == 0)
                {
                    //the client has disconnected from the server
                    break;
                }

                if (bytesRead > 0)
                {
                    GDBPacket packet = new GDBPacket(message, bytesRead);
                    _target.Log?.Invoke($"--> {packet}");
                    Console.WriteLine("Received packet: " + packet._text.ToString());

                    bool isSignal;
                    string response = session.ParseRequest(packet, out isSignal);
                    if (response != null)
                    {
                        if (isSignal)
                            await SendGlobal(response);
                        else
                            await SendResponse(clientStream, response);
                    }
                }
            }
            myGdbClientProc?.StandardInput.WriteLine(@"quit");

            myGdbClientProc?.Close();
            myGdbClientProc?.Dispose();
            myGdbClientProc?.Kill();
            tcpClient.Client.Shutdown(SocketShutdown.Both);
            Console.WriteLine("Client disconnected");
        }

        private async Task SendResponse(Stream stream, string response)
        {
            _target.Log?.Invoke($"<-- {response}");

            byte[] bytes = _encoder.GetBytes(response);
            await stream.WriteAsync(bytes, 0, bytes.Length);
            Console.WriteLine("Sending packet: " + response.ToString());
        }

        public void Dispose()
        {
            if (_listener != null)
            {
                _listener.Stop();

                lock (_clientsLock)
                {
                    foreach (var client in _clients)
                        if (client.Connected)
                            client.Client.Shutdown(SocketShutdown.Both);
                }

                _listener.Server.Shutdown(SocketShutdown.Both);
            }
        }
    }
}