using System.Net;
using System.Net.Sockets;
using System.Text;

namespace IPK24ChatClient
{
    public class TcpChatCommunicator : IChatCommunicator
    {
        private TcpClient? tcpClient;
        private NetworkStream? stream;
        private StreamReader? reader;

        public async Task ConnectAsync(string serverAddress, int serverPort)
        {
            // limit to only ipv4
            tcpClient = new TcpClient();
            var parsedAddress = Dns.GetHostAddresses(serverAddress);
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
            stream?.Close();
            reader?.Close();
            tcpClient?.Close();
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
            catch (Exception)
            {
                return null;
            }

            return messageBuilder.ToString();
        }

        public Message ParseMessage(string message)
        {
            return Message.ParseFromTcp(message);
        }

        public Message? ParseMessage(byte[] data)
        {
            return null; // Not used for TCP
        }

        public async Task<Message?> ReceiveMessageAsync(int timeoutMs)
        {
            await Task.Delay(timeoutMs);
            return null; // Not used for TCP
        }
    }
}