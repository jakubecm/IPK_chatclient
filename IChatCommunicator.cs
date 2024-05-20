// -----------------------------------------------------------------------------
// Project: IPK24ChatClient
// File: IChatCommunicator.cs
// Author: Milan Jakubec (xjakub41)
// Date: 2024-03-26
// License: GPU General Public License v3.0
// Description: This is interface for ChatCommunicator class.
// It was created to abstract the communication with the server through some form so it doesnt matter if it is TCP or UDP.
// -----------------------------------------------------------------------------
namespace IPK24ChatClient
{
    /// <summary>
    /// Interface for ChatCommunicator class.
    /// </summary>
    /// <remarks>
    /// Implementations of this interface are responsible for establishing a connection
    /// to the server, sending and receiving messages, and disconnecting from the server.
    /// It abstracts the details of the underlying communication protocol.
    /// </remarks>
    public interface IChatCommunicator
    {
        /// <summary>
        /// Asynchronously establishes a connection to the server.
        /// </summary>
        /// <param name="serverAddress">The IP address or hostname of the server</param>
        /// <param name="serverPort">The port number on which the server is listening</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ConnectAsync(string serverAddress, int serverPort);

        /// <summary>
        /// Disconnects from the server.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Asynchronously sends a message to the server.
        /// </summary>
        /// <param name="message">The message to be sent</param>
        /// <returns>A task that represents the async operation</returns>
        Task SendMessageAsync(Message message);

        /// <summary>
        /// Asynchronously receives a message from the server.
        /// </summary>
        /// <returns>A task that represents the async receive operation, containing the received message in form of a string.</returns>
        Task<Message?> ReceiveMessageAsync();

    }

}

