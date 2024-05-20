namespace IPK24ChatClient
{
    public interface IChatCommunicator
    {
        Task ConnectAsync(string serverAddress, int serverPort);
        void Disconnect();
        Task SendMessageAsync(Message message);
        Task<string?> ReceiveMessageAsync();
        Message ParseMessage(string message);
    }

}

