// Defines the structure of various message types according to the IPK24-CHAT protocol.
// Includes serialization and deserialization methods for converting messages to and from byte arrays or strings.

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
        Confirm, // Used for UDP confirmations
        Invalid // Used for invalid messages
    }

    public class Message
    {
        public MessageType Type { get; }
        public ushort? MessageId { get; set;} // Relevant for UDP
        public string? Username { get; }
        public string? DisplayName { get; }
        public string? ChannelId { get; }
        public string? Secret { get; }
        public string? Content { get; }
        public bool? ReplySuccess { get; } // Relevant for Reply messages

        // Constructor for messages, including MessageId which is essential for UDP communication
        public Message(MessageType type, ushort? messageId = null, string? username = null,
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
            var parts = data.Split(new[] { ' ' });
            if (parts.Length < 1) throw new FormatException("Invalid TCP message format.");

            switch (parts[0].ToUpper())
            {
                // MSG FROM <display-name> IS <content>
                case "MSG":
                    if(parts.Length < 5) return new Message(MessageType.Invalid);
                    if (parts[1].ToUpper() != "FROM" && parts[3].ToUpper() != "IS")
                    {
                        return new Message(MessageType.Invalid);
                    }
                    var displayNameMsg = parts[2];
                    var contentMsg = string.Join(" ", parts.Skip(4));
                    return new Message(MessageType.Msg, displayName: displayNameMsg, content: contentMsg);

                // ERR FROM <display-name> IS <content>
                case "ERR":
                    if(parts.Length < 5) return new Message(MessageType.Invalid);
                    if (parts[1].ToUpper() != "FROM" && parts[3].ToUpper() != "IS")
                    {
                        return new Message(MessageType.Invalid);
                    }
                    var displayNameErr = parts[2];
                    var contentErr = string.Join(" ", parts.Skip(4));
                    return new Message(MessageType.Err, displayName: displayNameErr, content: contentErr);

                // REPLY (OK|NOK) IS <content>
                case "REPLY":
                    if(parts.Length < 4) return new Message(MessageType.Invalid);
                    if (parts[1].ToUpper() != "OK" && parts[1].ToUpper() != "NOK" && parts[2].ToUpper() != "IS")
                    {
                        return new Message(MessageType.Invalid);
                    }
                    bool isSuccess = parts[1].Equals("OK", StringComparison.OrdinalIgnoreCase);
                    var replyContent = string.Join(" ", parts.Skip(3));
                    return new Message(MessageType.Reply, replySuccess: isSuccess, content: replyContent);

                // BYE
                case "BYE":
                    return new Message(MessageType.Bye);

                default:
                    return new Message(MessageType.Invalid);
            }

        }

        public byte[] SerializeToUdp()
        {
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms, Encoding.ASCII);

            // Write message type as byte
            byte messageTypeByte = GetByteValFromMessageType(Type);
            writer.Write(messageTypeByte);

            // Write message ID
            writer.Write(MessageId == null ? (short)0 : (short)MessageId);


            // Write message-specific data
            switch (Type)
            {
                case MessageType.Auth:
                    writer.Write(Encoding.ASCII.GetBytes($"{Username}\0{DisplayName}\0{Secret}\0"));
                    break;
                case MessageType.Join:
                    writer.Write(Encoding.ASCII.GetBytes($"{ChannelId}\0{DisplayName}\0"));
                    break;
                case MessageType.Msg:
                case MessageType.Err:
                    writer.Write(Encoding.ASCII.GetBytes($"{DisplayName}\0{Content}\0"));
                    break;
                case MessageType.Bye:
                    // No additional data needed
                    break;
                case MessageType.Confirm:
                    // No additional data needed
                    break;
            }

            return ms.ToArray();
        }

        public static Message ParseFromUdp(byte[] data)
        {
            var ms = new MemoryStream(data);
            var reader = new BinaryReader(ms, Encoding.ASCII);

            // Read message type
            MessageType type = GetMessageTypeFromByte(reader.ReadByte());

            // Read message ID
            ushort messageId = (ushort)reader.ReadUInt16();

            // Read the rest of the message based on type
            string[] parts;
            switch (type)
            {
                case MessageType.Msg:
                case MessageType.Err:
                    parts = ReadNullTerminatedStrings(reader, 2);
                    return new Message(type, messageId, displayName: parts[0], content: parts[1]);
                case MessageType.Reply:
                    bool success = reader.ReadByte() == 1;
                    _ = reader.ReadUInt16(); // discard the refmessageid
                    string content = ReadNullTerminatedString(reader);
                    return new Message(type, messageId, replySuccess: success, content: content);
                case MessageType.Confirm:
                    return new Message(type, messageId);
                case MessageType.Bye:
                    return new Message(type, messageId);
            }

            return new Message(MessageType.Invalid);
        }

        // Helper method to read strings terminated by a null byte
        private static string[] ReadNullTerminatedStrings(BinaryReader reader, int count)
        {
            var results = new List<string>();
            for (int i = 0; i < count; i++)
            {
                results.Add(ReadNullTerminatedString(reader));
            }
            return results.ToArray();
        }

        private static string ReadNullTerminatedString(BinaryReader reader)
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0)
            {
                bytes.Add(b);
            }
            return Encoding.ASCII.GetString(bytes.ToArray());
        }

        public static byte GetByteValFromMessageType(MessageType type)
        {
            switch (type)
            {
                case MessageType.Confirm:
                    return 0x00;
                case MessageType.Reply:
                    return 0x01;
                case MessageType.Auth:
                    return 0x02;
                case MessageType.Join:
                    return 0x03;
                case MessageType.Msg:
                    return 0x04;
                case MessageType.Err:
                    return 0xFE;
                case MessageType.Bye:
                    return 0xFF;
                default:
                    return 0xFE;
            }
        }

        public static MessageType GetMessageTypeFromByte(byte type)
        {
            switch (type)
            {
                case 0x00:
                    return MessageType.Confirm;
                case 0x01:
                    return MessageType.Reply;
                case 0x02:
                    return MessageType.Auth;
                case 0x03:
                    return MessageType.Join;
                case 0x04:
                    return MessageType.Msg;
                case 0xFE:
                    return MessageType.Err;
                case 0xFF:
                    return MessageType.Bye;
                default:
                    return MessageType.Invalid;
            }
        }
    }


}