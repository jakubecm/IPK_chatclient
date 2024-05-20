using System.Text.RegularExpressions;

// -----------------------------------------------------------------------------
// Project: IPK24ChatClient
// File: AuthHandler.cs
// Author: Milan Jakubec (xjakub41)
// Date: 2024-03-26
// License: GPU General Public License v3.0
// Description: Implementation of authentication handler for the chat client.
// -----------------------------------------------------------------------------

namespace IPK24ChatClient
{
    /// <summary>
    /// Handles authentication for the chat client.
    /// </summary>
    /// <remarks>
    /// This class processes authentication by validating input parameters and managing the authentication state
    /// of the client. It requires server confirmation for the authentication to be considered successful.
    /// </remarks>
    public class AuthHandler : ICommandHandler
    {
        private readonly IChatCommunicator chatCommunicator;
        private readonly ChatClient chatClient;

        /// <value>True if the command requires server confirmation, false otherwise.</value>
        public bool RequiresServerConfirmation => true;

        /// <summary>
        /// Constructor for the AuthHandler class.
        /// </summary>
        /// <param name="chatCommunicator">The chat communicator instance used to send and receive messages (TCP/UDP)</param>
        /// <param name="chatClient">The chat client instance</param>
        public AuthHandler(IChatCommunicator chatCommunicator, ChatClient chatClient)
        {
            this.chatCommunicator = chatCommunicator;
            this.chatClient = chatClient;
        }

        /// <summary>
        /// Executes the authentication command with the given parameters.
        /// </summary>
        /// <param name="parameters">Authentication parameters : Username, Secret, DisplayName</param>
        /// <param name="cancellationToken">Token used to observe whether the task was not cancelled</param>
        /// <remarks>
        /// This method validates the parameters and sends an authentication message to the server.
        /// It sets the client state to Auth and the last command sent to Auth, if the parameters are valid.
        /// Usage: /auth {Username} {Secret} {DisplayName}
        /// </remarks>
        /// <exception cref="System.ArgumentException">Thrown when the parameters are invalid</exception>
        public async Task ExecuteCommandAsync(string[] parameters, CancellationToken cancellationToken)
        {
            if (!validateParameters(parameters))
            {
                chatClient.signalSemaphoreToRelease();
                return;
            }

            if (chatClient.getClientState() == ClientState.Open ||
                chatClient.getClientState() == ClientState.Error ||
                chatClient.getClientState() == ClientState.End)
            {
                Console.Error.WriteLine("You are already authenticated.");
                chatClient.signalSemaphoreToRelease();
                return;
            }

            chatClient.displayName = parameters[2];
            Message authMessage = new Message(MessageType.Auth, username: parameters[0], secret: parameters[1], displayName: parameters[2]);
            chatClient.setClientState(ClientState.Auth);
            chatClient.setLastCommandSent(MessageType.Auth);

            await chatCommunicator.SendMessageAsync(authMessage);

        }

        /// <summary>
        /// Validates the authentication parameters.
        /// </summary>
        /// <param name="parameters">Parameters to validate</param>
        /// <returns>true if the parameters are valid; otherwise false</returns>
        public bool validateParameters(string[] parameters)
        {
            if (parameters.Length != 3 || parameters.Any(string.IsNullOrWhiteSpace))
            {
                Console.Error.WriteLine("Invalid number of parameters.");
                return false;
            }

            string username = parameters[0];
            string secret = parameters[1];
            string displayName = parameters[2];

            var usernameRegex = new Regex("^[a-zA-Z0-9-]{1,20}$");
            var secretRegex = new Regex("^[a-zA-Z0-9-]{1,128}$");

            if (!usernameRegex.IsMatch(username) || !secretRegex.IsMatch(secret))
            {
                Console.Error.WriteLine("Invalid username or secret.");
                return false;
            }

            if (displayName.Length <= 0 || displayName.Length > 20)
            {
                Console.Error.WriteLine("Invalid display name.");
                return false;
            }

            foreach (char c in displayName)
            {
                // ASCII printable characters, excluding space
                // 0x21 = '!', 0x7E = '~'
                if (c < '!' || c > '~')
                {
                    Console.Error.WriteLine("Invalid display name.");
                    return false;
                }
            }

            return true;
        }
    }


}