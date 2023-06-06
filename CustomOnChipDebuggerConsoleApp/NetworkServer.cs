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
        private const string folder = @"C:\Users\yasha.LAPTOP-L0KCDRSD\OneDrive\TUHH\WiSe2022\ProjectArbeit\RISCV\S500_CSSI";

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
            _listener = new TcpListener(IPAddress.Any, _port); //Create a TcpListener on specified port
            _listener.Start(); //Start the TcpListener
            Console.WriteLine("GDB RSP Server started on port " + 11000);
            await WaitForClients(); //Wait for client connection
        }

        private async Task WaitForClients()
        {
            try
            {
                while (true)
                {
                    var processStartInfo = new ProcessStartInfo();
                    processStartInfo.WorkingDirectory = folder;
                    processStartInfo.FileName = "cmd.exe";
                    processStartInfo.Arguments = "/C Start_GDB.bat";
                    myGdbClientProc = Process.Start(processStartInfo); //Start GDB client application
                    TcpClient client = _listener.AcceptTcpClient(); //Accept the client connection
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
                    bytesRead = await clientStream.ReadAsync(message, 0, 4096); //Try reading the byte packets from client application
                }
                //handle exceptions, if any, and log them
                catch (IOException iex) 
                {
                    var socketEx = iex.InnerException as SocketException;
                    if (socketEx == null || socketEx.SocketErrorCode != SocketError.Interrupted)
                    {
                        _target.LogException?.Invoke(socketEx);
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

                
                if (bytesRead > 0) //check if the packet read has any information
                {
                    GDBPacket packet = new GDBPacket(message, bytesRead);
                    _target.Log?.Invoke($"--> {packet}");
                    Console.WriteLine("Received packet: " + packet._text.ToString());

                    bool isSignal;
                    string response = session.ParseRequest(packet, out isSignal); //Parse the packet to generate the response
                    if (response != null)
                    {
                        if (isSignal)
                            await SendGlobal(response);
                        else
                            await SendResponse(clientStream, response);
                    }
                }
            }

            //terminate the GDB server application
            myGdbClientProc?.StandardInput.WriteLine(@"quit");

            //Close the application
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
            await stream.WriteAsync(bytes, 0, bytes.Length); //Send the bytes on connected client stream
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