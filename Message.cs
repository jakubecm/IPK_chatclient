using System.Text;
// -----------------------------------------------------------------------------
// Project: IPK24ChatClient
// File: Message.cs
// Author: Milan Jakubec (xjakub41)
// Date: 2024-03-26
// License: GPU General Public License v3.0
// Description: This is implementation of Message class and its methods.
//              Defines the structure of various message types according to the IPK24-CHAT protocol.
//              Includes serialization and deserialization methods for converting messages to and from byte arrays or strings.
// -----------------------------------------------------------------------------

namespace IPK24ChatClient
{
    /// <summary>
    /// Enum representing the type of a message.
    /// </summary>
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

    /// <summary>
    /// Class representing a message according to the IPK24-CHAT protocol.
    /// </summary>
    public class Message
    {
        /// <value>The type of the message.</value>
        public MessageType Type { get; }

        /// <value>The message ID. Relevant for UDP communication.</value>
        public ushort? MessageId { get; set;}

        /// <value>The username of the sender. Relevant for Auth messages.</value>
        public string? Username { get; }

        /// <value>The display name of the sender. Relevant for Auth, Join, Msg, Err messages.</value>
        public string? DisplayName { get; }

        /// <value>The channel ID. Relevant for Join messages.</value>
        public string? ChannelId { get; }

        /// <value>The secret. Relevant for Auth messages.</value>
        public string? Secret { get; }

        /// <value>The content of the message. Relevant for Msg, Err messages.</value>
        public string? Content { get; }

        /// <value>Flag indicating if the reply to server request is positive or negative. Relevant for Reply messages.</value>
        public bool? ReplySuccess { get; }

        /// <summary>
        /// Constructor for a message.
        /// </summary>
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

        /// <summary>
        /// Serialize the message to a string for TCP communication according to protocol.
        /// </summary>
        /// <returns>string in the format ready to be sent over TCP</returns>
        /// <exception cref="ArgumentException">In case the user tries to serialize invalid message type</exception>
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

        /// <summary>
        /// Parses a message from a string received over TCP according to protocol.
        /// </summary>
        /// <param name="data">The raw message string</param>
        /// <returns>Message object filled with received information</returns>
        /// <exception cref="FormatException">Happens if the received string has incorrect format</exception>
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

        /// <summary>
        /// Serialize the message to a byte array for UDP communication according to protocol.
        /// </summary>
        /// <returns>Byte array filled with message info ready to be sent over UDP</returns>
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

        /// <summary>
        /// Parses a message from a byte array received over UDP according to protocol.
        /// </summary>
        /// <param name="data">Byte array of message data</param>
        /// <returns>Message object carrying parsed information from the byte array</returns>
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
                default:
                    return new Message(MessageType.Invalid);
            }
        }

        /// <summary>
        /// Reads a given number of null-terminated strings from the binary reader.
        /// </summary>
        /// <param name="reader">BinaryReader instance</param>
        /// <param name="count">number of string to be read</param>
        /// <returns></returns>
        private static string[] ReadNullTerminatedStrings(BinaryReader reader, int count)
        {
            var results = new List<string>();
            for (int i = 0; i < count; i++)
            {
                results.Add(ReadNullTerminatedString(reader));
            }
            return results.ToArray();
        }

        /// <summary>
        /// Reads a null-terminated string from the binary reader.
        /// </summary>
        /// <param name="reader">BinaryReader instance</param>
        /// <returns>a string read from the reader</returns>
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

        /// <summary>
        /// Converts MessageType enum to byte value.
        /// </summary>
        /// <param name="type">Message type to be converted</param>
        /// <returns>a byte of message type</returns>
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

        /// <summary>
        /// Converts byte value to MessageType enum.
        /// </summary>
        /// <param name="type">byte to be converted</param>
        /// <returns>MessageType extracted form the byte value</returns>
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
