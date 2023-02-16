using CustomOnChipDebuggerBE.GDB.Formats;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Xml.Serialization;

namespace CustomOnChipDebuggerConsoleApp
{
    internal class Server
    {
        static void Main(string[] args)
        {
            IPHostEntry host = Dns.GetHostEntry("localhost");
            IPAddress ipAddress = host.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);
            Socket socket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.IP);

            TcpListener server = new TcpListener(IPAddress.Any, 11000);
            server.Start();

            Console.WriteLine("GDB RSP Server started on port " + 11000);

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Client connected from " + client.Client.RemoteEndPoint.ToString());

                NetworkStream stream = client.GetStream();

                while (client.Connected)
                {
                    byte[] data = new byte[4096];
                    int bytesRead = stream.Read(data, 0, data.Length);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    string packet = System.Text.Encoding.ASCII.GetString(data, 0, bytesRead);
                    Console.WriteLine("Received packet: " + packet);

                    // Process the packet and send a response
                    var response = string.Empty;

                    switch (packet[0])
                    {
                        case '$':
                            // Handle standard RSP packets
                            switch (packet[1])
                            {
                                case 'g':
                                    // Handle the $g packet (get the value of the general-purpose registers)
                                    response = "$00000000000000000000000000000000#00";
                                    break;

                                case 'm':
                                    // Handle the $m packet (read memory contents)
                                    response = "$00000000#00";
                                    break;

                                case '?':
                                    // Handle the $? packet (get the target's status)
                                    response = "$S05#b8";
                                    break;

                                case 'q':
                                    // Handle various $q packets (query the target)
                                    if (packet.StartsWith("$qSupported"))
                                    {
                                        // Load XML file and deserialize into TargetDescription object
                                        XmlSerializer serializer = new XmlSerializer(typeof(TargetDescription));
                                        TargetDescription targetDescription;
                                        using (StreamReader reader = new StreamReader(@"C:\Users\yasha.LAPTOP-L0KCDRSD\OneDrive\TUHH\WiSe2022\ProjectArbeit\CustomOCD\CustomOnChipDebuggerBE\GDB\Formats\RISCV32.xml"))
                                        {
                                            targetDescription = (TargetDescription)serializer.Deserialize(reader);
                                        }

                                        // Generate packet content from TargetDescription object
                                        string packetContent = $"+$qXfer:features:read:target.xml:{targetDescription.XmlSize}:{targetDescription.XmlChecksum}";
                                        WritePacket(stream, packetContent);
                                        Console.WriteLine("Sending packet: " + packetContent);

                                        // Send XML content to GDB client
                                        byte[] xmlData = File.ReadAllBytes(@"C:\Users\yasha.LAPTOP-L0KCDRSD\OneDrive\TUHH\WiSe2022\ProjectArbeit\CustomOCD\CustomOnChipDebuggerBE\GDB\Formats\RISCV32.xml");
                                        stream.Write(xmlData, 0, xmlData.Length);
                                    }
                                    break;

                                case 'v':
                                    response = "+";
                                    break;
                                default:
                                    // Return an error for unknown packets
                                    response = "$E01#00";
                                    break;
                            }
                            break;

                        case '-':
                            // Handle negative acknowledge packets
                            continue;
                        case '+':
                            // Handle acknowledge packets
                            break;
                        default:
                            // Return an error for unknown packets
                            response = "$E01#00";
                            break;
                    }
                    if (!string.IsNullOrEmpty(response))
                    {
                        byte[] responseData = System.Text.Encoding.ASCII.GetBytes(response);
                        stream.Write(responseData, 0, responseData.Length);
                        Console.WriteLine("Sending packet: " + response);
                    }
                }

                client.Close();
                Console.WriteLine("Client disconnected");
            }
        }

        private static void WritePacket(NetworkStream gdbStream, string packet)
        {
            byte[] packetBytes = System.Text.Encoding.ASCII.GetBytes(packet);
            ushort checksum = 0;
            for (int i = 0; i < packetBytes.Length; i++)
            {
                checksum += packetBytes[i];
            }
            checksum %= 256;

            gdbStream.WriteByte((byte)'$');
            for (int i = 0; i < packetBytes.Length; i++)
            {
                if (packetBytes[i] == (byte)'#' || packetBytes[i] == (byte)'$' || packetBytes[i] == (byte)'}')
                {
                    gdbStream.WriteByte((byte)'}');
                    gdbStream.WriteByte((byte)(packetBytes[i] ^ 0x20));
                }
                else
                {
                    gdbStream.WriteByte(packetBytes[i]);
                }
            }
            gdbStream.WriteByte((byte)'#');
            gdbStream.WriteByte((byte)checksum);
            gdbStream.Flush();
        }
    }
}
