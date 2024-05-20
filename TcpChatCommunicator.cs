using System.Net;
using System.Net.Sockets;
using System.Text;

// -----------------------------------------------------------------------------
// Project: IPK24ChatClient
// File: TcpChatCommunicator.cs
// Author: Milan Jakubec (xjakub41)
// Date: 2024-03-26
// License: GPU General Public License v3.0
// Description: This is implementation of IChatCommunicator for TCP communication.
// -----------------------------------------------------------------------------

namespace IPK24ChatClient
{
    /// <summary>
    /// Implementation of IChatCommunicator for TCP communication.
    /// </summary>
    public class TcpChatCommunicator : IChatCommunicator
    {
        /// <value>The TCP client instance used for communication.</value>
        private TcpClient? tcpClient;

        /// <value>The network stream used for communication.</value>
        private NetworkStream? stream;

        /// <value>The stream reader used for reading messages from the network stream.</value>
        private StreamReader? reader;

        public async Task ConnectAsync(string serverAddress, int serverPort)
        {
            tcpClient = new TcpClient();
            var parsedAddress = Dns.GetHostAddresses(serverAddress);
            // limit to only ipv4
            IPAddress? ipv4addr = parsedAddress.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

            if (ipv4addr == null)
            {
                throw new ArgumentException("Server address is not a valid IPv4 address.");
            }

            await tcpClient.ConnectAsync(ipv4addr, serverPort);
            stream = tcpClient.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
        }

        public void Disconnect()
        {
            if (stream != null)
            {
                stream.Close();
            }
            if(reader != null){
                reader.Close();
            }
            if(tcpClient!.Connected){
                tcpClient.Close();
            }
        }

        public async Task SendMessageAsync(Message message)
        {
            // Convert the string message to byte array and send
            string serializedMessage = message.SerializeToTcp();
            var byteMessage = Encoding.ASCII.GetBytes(serializedMessage);

            if (stream == null)
            {
                throw new InvalidOperationException("The client is not connected to the server.");
            }

            await stream.WriteAsync(byteMessage, 0, byteMessage.Length);
        }

        public async Task<Message?> ReceiveMessageAsync()
        {
            if (reader == null) throw new InvalidOperationException("Not connected to the server.");

            StringBuilder messageBuilder = new StringBuilder();
            char[] buffer = new char[1];
            try
            {
                // Read one character at a time to find the message delimiter (\r\n)
                while (true)
                {
                    int charRead = await reader.ReadAsync(buffer, 0, 1);
                    if (charRead == 0)
                    {
                        // End of stream reached
                        return null;
                    }

                    messageBuilder.Append(buffer[0]);
                    if (messageBuilder.Length >= 2 &&
                        messageBuilder[^2] == '\r' && messageBuilder[^1] == '\n')
                    {
                        // Remove the trailing \r\n before returning the message
                        messageBuilder.Length -= 2;
                        break;
                    }
                }
            }
            catch (Exception) // In case user cancels the operation, dont throw exception
            {
                return null;
            }

            string rawMessage = messageBuilder.ToString();
            return Message.ParseFromTcp(rawMessage);
        }
    }
}