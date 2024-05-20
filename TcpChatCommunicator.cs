using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace IPK24ChatClient
{
    public class TcpChatCommunicator : IChatCommunicator
    {
        private TcpClient? tcpClient;
        private NetworkStream? stream;
        private StreamReader? reader;

        public async Task ConnectAsync(string serverAddress, int serverPort)
        {
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(serverAddress, serverPort);
            stream = tcpClient.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
        }

        public void Disconnect()
        {
            stream?.Close();
            reader?.Close();
            tcpClient?.Close();
        }

        public async Task SendMessageAsync(string message)
        {
            // Convert the string message to byte array and send
            var byteMessage = Encoding.UTF8.GetBytes(message);

            if (stream == null)
            {
                throw new InvalidOperationException("The client is not connected to the server.");
            }

            await stream.WriteAsync(byteMessage, 0, byteMessage.Length);
        }

        public async Task<string?> ReceiveMessageAsync()
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
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while receiving the message: {ex.Message}");
                return null;
            }

            return messageBuilder.ToString();
        }

        public Message ParseMessage(string message)
        {
            return Message.ParseFromTcp(message);
        }
    }
}