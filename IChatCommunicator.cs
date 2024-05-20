namespace IPK24ChatClient
{
    public interface IChatCommunicator
    {
        Task ConnectAsync(string serverAddress, int serverPort);
        void Disconnect();
        Task SendMessageAsync(Message message);
        Task<string?> ReceiveMessageAsync(); // TCP
        Task<Message?> ReceiveMessageAsync(int timeoutMs); // overload for UDP
        Message? ParseMessage(string message); // TCP
        Message? ParseMessage(byte[] messageBytes); // overload for UDP

    }

}

