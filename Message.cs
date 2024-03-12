// Defines the structure of various message types according to the IPK24-CHAT protocol.
// Includes serialization and deserialization methods for converting messages to and from byte arrays or strings.

using System;
using System.Text;

namespace IPK24ChatClient
{
    public enum MessageType
    {
        Auth,
        Join,
        Msg,
        Bye,
        Err,
        Reply,
        Confirm // Used for UDP confirmations
    }

    public class Message
    {
        public MessageType Type { get; }
        public ushort? MessageId { get; } // Relevant for UDP
        public string? Username { get; }
        public string? DisplayName { get; }
        public string? ChannelId { get; }
        public string? Secret { get; }
        public string? Content { get; }
        public bool? ReplySuccess { get; } // Relevant for Reply messages

        // Constructor for TCP messages where the fields are more flexible based on message type
        public Message(MessageType type, string? username = null, string? displayName = null, 
                       string? channelId = null, string? secret = null, string? content = null, 
                       bool? replySuccess = null)
        {
            Type = type;
            Username = username;
            DisplayName = displayName;
            ChannelId = channelId;
            Secret = secret;
            Content = content;
            ReplySuccess = replySuccess;
        }

        // Constructor for UDP messages, including MessageId which is essential for UDP communication
        public Message(MessageType type, ushort messageId, string? username = null, 
                       string? displayName = null, string? channelId = null, string? secret = null, 
                       string? content = null, bool? replySuccess = null)
        {
            Type = type;
            MessageId = messageId;
            Username = username;
            DisplayName = displayName;
            ChannelId = channelId;
            Secret = secret;
            Content = content;
            ReplySuccess = replySuccess;
        }

        public string SerializeToTcp()
        {
            var sb = new StringBuilder();

            switch (Type)
            {
                case MessageType.Auth:
                    sb.Append($"AUTH {Username} AS {DisplayName} USING {Secret}\r\n");
                    break;
                case MessageType.Join:
                    sb.Append($"JOIN {ChannelId} AS {DisplayName}\r\n");
                    break;
                case MessageType.Msg:
                    sb.Append($"MSG FROM {DisplayName} IS {Content}\r\n");
                    break;
                case MessageType.Err:
                    sb.Append($"ERR FROM {DisplayName} IS {Content}\r\n");
                    break;
                case MessageType.Bye:
                    sb.Append("BYE\r\n");
                    break;
                default:
                    throw new ArgumentException("Unsupported message type for TCP serialization.");
            }

            return sb.ToString();
        }

        public static Message ParseFromTcp(string data)
        {
            var lines = data.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ' ' });
                if (parts.Length < 1) continue;

                switch (parts[0].ToUpper())
                {
                    // MSG FROM <display-name> IS <content>
                    case "MSG":
                        var displayNameMsg = parts[2];
                        var contentMsg = string.Join(" ", parts.Skip(4));
                        return new Message(MessageType.Msg, displayName: displayNameMsg, content: contentMsg);
                    
                    // ERR FROM <display-name> IS <content>
                    case "ERR":
                        var displayNameErr = parts[2];
                        var contentErr = string.Join(" ", parts.Skip(4));
                        return new Message(MessageType.Err, displayName: displayNameErr, content: contentErr);

                    // REPLY (OK|NOK) IS <content>
                    case "REPLY":
                        bool isSuccess = parts[1].Equals("OK", StringComparison.OrdinalIgnoreCase);
                        var replyContent = string.Join(" ", parts.Skip(3));
                        return new Message(MessageType.Reply, replySuccess: isSuccess, content: replyContent);

                    // BYE
                    case "BYE":
                        return new Message(MessageType.Bye);
                }
            }

            throw new FormatException("Invalid TCP message format.");
        }

    }
}
