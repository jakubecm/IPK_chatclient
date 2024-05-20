// -----------------------------------------------------------------------------
// Project: IPK24ChatClient
// File: RenameHandler.cs
// Author: Milan Jakubec (xjakub41)
// Date: 2024-03-26
// License: GPU General Public License v3.0
// Description: Implementation of rename command handler for the chat client.
// -----------------------------------------------------------------------------
namespace IPK24ChatClient
{
    /// <summary>
    /// Handles the rename command for the chat client.
    /// </summary>
    public class RenameHandler : ICommandHandler
    {
        private readonly ChatClient chatClient;

        /// <value>True if the command requires server confirmation, false otherwise.</value>
        public bool RequiresServerConfirmation => false;

        /// <summary>
        /// Constructor for the RenameHandler class.
        /// </summary>
        /// <param name="chatClient">Instance of the chat client</param>
        public RenameHandler(ChatClient chatClient)
        {
            this.chatClient = chatClient;
        }


        /// <summary>
        /// Executes the rename command with the given parameters.
        /// </summary>
        /// <param name="parameters">Command parameters, expecting to contain new DisplayName</param>
        /// <param name="cancellationToken">Cancel token to watch for cancellation</param>
        public Task ExecuteCommandAsync(string[] parameters, CancellationToken cancellationToken)
        {

            if (!validateParameters(parameters))
            {
                return Task.CompletedTask;
            }

            chatClient.displayName = parameters[0];
            return Task.CompletedTask;
        }

        /// <summary>
        /// Validates the parameters for the rename command.
        /// </summary>
        /// <param name="parameters">Rename parameters to validate</param>
        /// <returns>true if the parameters are valid; otherwise, false.</returns>
        public bool validateParameters(string[] parameters)
        {
            if (parameters.Length != 1)
            {
                Console.Error.WriteLine("Usage: /rename {DisplayName}");
                return false;
            }

            string displayName = parameters[0];

            if (displayName.Length <= 0 || displayName.Length > 20)
            {
                return false;
            }

            foreach (char c in displayName)
            {
                // ASCII printable characters, excluding space
                // 0x21 = '!', 0x7E = '~'
                if (c < '!' || c > '~')
                {
                    return false;
                }
            }

            return true;
        }
    }
}