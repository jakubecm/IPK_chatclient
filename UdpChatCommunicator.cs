using System.Net;
using System.Net.Sockets;
// -----------------------------------------------------------------------------
// Project: IPK24ChatClient
// File: UdpChatCommunicator.cs
// Author: Milan Jakubec (xjakub41)
// Date: 2024-03-26
// License: GPU General Public License v3.0
// Description: This is implementation of IChatCommunicator for UDP communication.
// -----------------------------------------------------------------------------

namespace IPK24ChatClient
{
    /// <summary>
    /// Implementation of IChatCommunicator for UDP communication.
    /// </summary>
    public class UdpChatCommunicator : IChatCommunicator
    {
        /// <value>The UDP client instance used for communication.</value>
        private UdpClient? udpClient;

        /// <value>The server endpoint used for communication.</value>
        private IPEndPoint? serverEndpoint;

        /// <value>The number of retries for sending a message.</value>
        public int udpRetries;

        /// <value>The timeout in miliseconds for receiving a confirm to a message.</value>
        public int udpTimeout;

        /// <value>The message ID used for sending messages.</value>
        private ushort messageId = 0;

        /// <value>Flag indicating if the dynamic port was allocated.</value>
        private bool dynPortAllocated = false;

        /// <value>Set of received message IDs. Used to drop any repeating messages.</value>
        private HashSet<ushort> receivedMessageIds = new HashSet<ushort>();

        /// <value>Set of confirmed sent message IDs. Used to check whether to retransmit message.</value>
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

            // Do not "connect" the UDP client, just set the server endpoint
            // Reason : dynamic port allocation
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

            // Retranmission loop
            while (retries <= udpRetries && !confirmed)
            {
                if (!dynPortAllocated) // if the dynamic port is not allocated, send to the server endpoint
                {
                    await udpClient.SendAsync(messageBytes, messageBytes.Length, serverEndpoint);

                    if (message.Type == MessageType.Confirm)
                    {
                        return;
                    }
                    confirmed = await WaitForConfirmationAsync(messageId, udpTimeout);
                    retries++;
                }
                else // otherwise send to the connected endpoint
                {
                    await udpClient.SendAsync(messageBytes, messageBytes.Length);

                    if (message.Type == MessageType.Confirm)
                    {
                        messageId++;
                        return; // don't wait for confirmation for confirm messages
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

        /// <summary>
        /// Waits for a confirmation message with the given message ID.
        /// </summary>
        /// <param name="messageid">ID of the message of which we want confirmation</param>
        /// <param name="timeoutMilliseconds">UDP retransmission timeout in miliseconds</param>
        /// <returns>True if the message was confirmed, false otherwise</returns>
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

        /// <summary>
        /// Getter for the UDP timeout.
        /// </summary>
        /// <returns>int value of UDP timeout in miliseconds</returns>
        public int getUdpTimeout()
        {
            return udpTimeout;
        }
        
        /// <summary>
        /// Adds message ID to the set of confirmed sent message IDs.
        /// </summary>
        /// <param name="messageId">ushort message ID to add to the set</param>
        public void addConfirmedSentMessageId(ushort messageId)
        {
            confirmedSentMessageIds.Add(messageId);
        }

        /// <summary>
        /// Checks if the message ID is in the set of confirmed sent message IDs.
        /// </summary>
        /// <param name="messageId">messageId to search for</param>
        /// <returns>true if message is found in the set; false otherwise</returns>
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
