using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;

namespace IPK24ChatClient
{
    public class UdpChatCommunicator : IChatCommunicator
    {
        private UdpClient? udpClient;
        private IPEndPoint? serverEndpoint;
        public int udpRetries;
        public int udpTimeout;
        private ushort messageId = 0;
        private bool dynPortAllocated = false;
        private HashSet<ushort> receivedMessageIds = new HashSet<ushort>();
        private HashSet<ushort> confirmedSentMessageIds = new HashSet<ushort>();


        public Task ConnectAsync(string serverAddress, int serverPort)
        {
            // Implementing only IPv4
            var parsedAddress = Dns.GetHostAddresses(serverAddress);
            IPAddress? ipv4addr = parsedAddress.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

            if (ipv4addr == null)
            {
                throw new ArgumentException("Server address is not a valid IPv4 address.");
            }
            serverEndpoint = new IPEndPoint(ipv4addr, serverPort);
            udpClient = new UdpClient();
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
            udpClient?.Close();
        }

        public async Task SendMessageAsync(Message message)
        {
            if (udpClient == null || serverEndpoint == null)
            {
                throw new InvalidOperationException("UDP client is not initialized or connected.");
            }

            int retries = 0;
            bool confirmed = false;

            // If the message does not have a message ID, assign it
            // This block was added because confirm messages IDs were being overwritten
            if (message.MessageId == null)
            {
                message.MessageId = messageId;
            }

            var messageBytes = message.SerializeToUdp();

            while (retries <= udpRetries && !confirmed)
            {
                if (!dynPortAllocated)
                {
                    await udpClient.SendAsync(messageBytes, messageBytes.Length, serverEndpoint);

                    if (message.Type == MessageType.Confirm) 
                    {
                        Console.WriteLine($"Sent confirm message: {message.MessageId}");
                        return;
                    }
                    confirmed = await WaitForConfirmationAsync(messageId, udpTimeout);
                    retries++;
                }
                else
                {
                    await udpClient.SendAsync(messageBytes, messageBytes.Length);

                    if (message.Type == MessageType.Confirm)
                    {
                        Console.WriteLine($"Sent confirm message: {message.MessageId}");
                        messageId++;
                        return;
                    }
                    confirmed = await WaitForConfirmationAsync(messageId, udpTimeout);
                    retries++;
                }
            }

            if (!confirmed)
            {
                throw new TimeoutException($"Message {messageId} of type {message.Type} was not confirmed by the server.");
            }

            messageId++;
        }

        private async Task<bool> WaitForConfirmationAsync(ushort messageid, int timeoutMilliseconds)
        {
 
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopWatch.ElapsedMilliseconds < timeoutMilliseconds)
            {
                if (CheckIfConfirmed(messageid))
                {
                    return true;
                }
                await Task.Delay(100);
            }

            return false;
        }

        public async Task<Message?> ReceiveMessageAsync(int udpTimeout)
        {
            if (udpClient == null)
            {
                throw new InvalidOperationException("UDP client is not initialized.");
            }

            try
            {
                while (true)
                {
                    var receivedBytes = await udpClient.ReceiveAsync();

                    var message = ParseMessage(receivedBytes.Buffer);

                    // On first reply from server, dynamic port is received
                    // Create a new endpoint with the dynamic port and send everything there from now on
                    if (!dynPortAllocated && message.Type == MessageType.Reply)
                    {
                        var senderPort = receivedBytes.RemoteEndPoint.Port;
                        var senderAddress = receivedBytes.RemoteEndPoint.Address;
                        var newServerEndpoint = new IPEndPoint(senderAddress, senderPort);
                        udpClient.Connect(newServerEndpoint);
                        dynPortAllocated = true;
                    }

                    if (message != null && message.MessageId != null)
                    {
                        if (message.Type != MessageType.Confirm)
                        {
                            // Try to add the message ID to the set
                            // If it is already there, return null (message was already received)
                            if (!receivedMessageIds.Add((ushort)message.MessageId)) 
                            {
                                return null;
                            }
                            else
                            {
                                var confirm = new Message(MessageType.Confirm, messageId: message.MessageId);
                                await SendMessageAsync(confirm); // Confirm the message arrived
                            }
                        }

                        return message;
                    }
                }

            }
            catch (Exception)
            {

                return null;
            }

        }

        public Message ParseMessage(byte[] data)
        {
            return Message.ParseFromUdp(data);
        }


        public int getUdpTimeout()
        {
            return udpTimeout;
        }

        public void addConfirmedSentMessageId(ushort messageId)
        {
            confirmedSentMessageIds.Add(messageId);
        }

        public bool CheckIfConfirmed(ushort messageId)
        {
            return confirmedSentMessageIds.Contains(messageId);
        }
        public async Task<string?> ReceiveMessageAsync()
        {
            await Task.Delay(udpTimeout);
            return null; // Not used for UDP
        }
        public Message? ParseMessage(string message)
        {
            return null; // Not used for UDP
        }
    }
}
