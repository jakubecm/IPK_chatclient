using System.Text.RegularExpressions;
// -----------------------------------------------------------------------------
// Project: IPK24ChatClient
// File: JoinHandler.cs
// Author: Milan Jakubec (xjakub41)
// Date: 2024-03-26
// License: GPU General Public License v3.0
// Description: Implementation of join command handler for the chat client.
// -----------------------------------------------------------------------------

namespace IPK24ChatClient
{
    /// <summary>
    /// Handles join commands for the chat client, allowing users to join specific channels.
    /// </summary>
    /// <remarks>
    /// This class processes join commands by validating input parameters and communicating
    /// with the chat server to join a channel. It requires server confirmation for the join
    /// operation to be considered successful.
    /// </remarks>
    public class JoinHandler : ICommandHandler
    {
        private readonly IChatCommunicator chatCommunicator;
        private readonly ChatClient chatClient;

        /// <value>True if the command requires server confirmation, false otherwise.</value>
        public bool RequiresServerConfirmation => true;

        /// <summary>
        /// Constructor for the JoinHandler class.
        /// </summary>
        /// <param name="chatCommunicator">The chat communicator instance used to send and receive messages (TCP/UDP)</param>
        /// <param name="chatClient">The chat client instance</param>
        public JoinHandler(IChatCommunicator chatCommunicator, ChatClient chatClient)
        {
            this.chatCommunicator = chatCommunicator;
            this.chatClient = chatClient;
        }

        /// <summary>
        /// Executes the join command with the given parameters.
        /// </summary>
        /// <param name="parameters">Command parameters, expecting to contain the channelId/name to join.</param>
        /// <param name="cancellationToken">Cancel token to watch for cancellation</param>
        public async Task ExecuteCommandAsync(string[] parameters, CancellationToken cancellationToken)
        {
            if (chatClient.getClientState() != ClientState.Open)
            {
                Console.Error.WriteLine("ERR: You must authenticate before joining a channel.");
                chatClient.signalSemaphoreToRelease();
                return;
            }

            if (!validateParameters(parameters))
            {
                chatClient.signalSemaphoreToRelease();
                return;
            }

            Message joinMessage = new Message(MessageType.Join, channelId: parameters[0], displayName: chatClient.displayName);
            await chatCommunicator.SendMessageAsync(joinMessage);
            chatClient.setLastCommandSent(MessageType.Join);
            chatClient.setChannelId(parameters[0]);
        }

        /// <summary>
        /// Validates the parameters for the join command.
        /// </summary>
        /// <param name="parameters">Join command parameters to validate.</param>
        /// <returns>true if the parameters are valid; otherwise, false.</returns>
        /// <remarks>
        /// The channel name must be between 1 and 20 characters long and contain only alphanumeric characters and hyphens.
        /// Theoretically it can include dot to be used on reference server (channels are in format discord.channel)
        /// </remarks>
        public bool validateParameters(string[] parameters)
        {
            if (parameters.Length != 1)
            {
                Console.Error.WriteLine("ERR: Invalid number of parameters.");
                return false;
            }
            // regex to match alphanumeric characters and hyphens, 1-20 characters long
            var regex = new Regex("^[a-zA-Z0-9-.]{1,20}$");

            if(!regex.IsMatch(parameters[0]))
            {
                Console.Error.WriteLine("ERR: Invalid channel ID. Channel ID must be between 1 and 20 characters long and contain only alphanumeric characters and hyphens.");
                return false;
            }

            return true;
        }
    }

}