// -----------------------------------------------------------------------------
// Project: IPK24ChatClient
// File: ICommandHandler.cs
// Author: Milan Jakubec (xjakub41)
// Date: 2024-03-26
// License: GPU General Public License v3.0
// Description: Interface for command handlers in the chat client.
// -----------------------------------------------------------------------------
namespace IPK24ChatClient
{
    /// <summary>
    /// Defines a contract for command handlers in the chat client.
    /// </summary>
    public interface ICommandHandler{

        /// <summary>
        /// Gets a value indicating whether the command requires server confirmation.
        /// </summary>
        /// <value>True if the command requires server confirmation, false otherwise.</value>
        bool RequiresServerConfirmation { get; }

        /// <summary>
        /// Executes the command with the given parameters.
        /// </summary>
        /// <param name="parameters">The command parameters</param>
        /// <param name="cancelToken">Cancel token used to cancel operaton</param>
        /// <returns>A task that represents the async operation</returns>
        /// <remarks>
        /// Implementations handle command execution logic, including any communication with the server or updates to client state.
        /// </remarks>
        Task ExecuteCommandAsync(string[] parameters, CancellationToken cancelToken);

        /// <summary>
        /// Validates the parameters for the command.
        /// </summary>
        /// <param name="parameters">The command parameters to validate</param>
        /// <returns>True if the parameters are valid; otherwise, false.</returns>
        bool validateParameters(string[] parameters);
    }

}