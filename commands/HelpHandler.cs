// -----------------------------------------------------------------------------
// Project: IPK24ChatClient
// File: HelpHandler.cs
// Author: Milan Jakubec (xjakub41)
// Date: 2024-03-26
// License: GPU General Public License v3.0
// Description: Implementation of help command handler for the chat client.
// -----------------------------------------------------------------------------
namespace IPK24ChatClient
{
    /// <summary>
    /// Handles the help command for the chat client.
    /// </summary>
    public class HelpHandler : ICommandHandler
    {
        /// <value>True if the command requires server confirmation, false otherwise.</value>
        public bool RequiresServerConfirmation => false;

        /// <summary>
        /// Executes the help command.
        /// </summary>
        /// <param name="parameters">Parameters are here only because of the interface, help does not take any</param>
        /// <param name="cancellationToken">Cancel token to watch for operation cancelation</param>
        public Task ExecuteCommandAsync(string[] parameters, CancellationToken cancellationToken)
        {
            if (!validateParameters(parameters))
            {
                return Task.CompletedTask;
            }
            Console.WriteLine("/auth {Username} {Secret} {DisplayName} - Authenticate with the server.");
            Console.WriteLine("/join {ChannelID} - Join a chat channel.");
            Console.WriteLine("/rename {DisplayName} - Change your display name locally.");
            Console.WriteLine("/help - Show this help message.");
            return Task.CompletedTask;
        }

        public bool validateParameters(string[] parameters)
        {
            if (parameters.Length > 0)
            {
                Console.Error.WriteLine("Invalid parameters.");
                Console.Error.WriteLine("Usage: /help");
                return false;
            }
            return true;
        }
    }
}