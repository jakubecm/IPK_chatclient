# IPK Project 1: Client for a chat server using `IPK24-CHAT` protocol documentation

The following documentation describes implementation of the IPK24-CHAT IPK project.
The task was to design and implement a client application, which is able to communicate with a remote server using the 'IPK24-chat' protocol. The protocol has two variants, each built on top of a different transport protocol.

## Table of Contents
- [Theory](#theory)
    - [Understanding transport protocols](#understanding-transport-protocols)
        - [TCP Protocol](#tcp-protocol)
        - [UDP Protocol](#udp-protocol)
        - [IPK24-CHAT Protocol](#ipk24-chat-protocol)
    - [Sockets](#sockets)
        - [How sockets work](#how-sockets-work)
        - [Key concepts](#key-concepts)
        - [Practical use](#practical-use)

- [The project goals](#the-project-goals)
- [Implementation](#implementation)
    - [Layout](#design)
    - [Chat Client](#chat-client)
    - [Network communicators](#network-communicators)
        - [TCP Communicator](#tcp-communicator)
        - [UDP Communicator](#udp-communicator)
    - [Command handler](#command-handler)
    - [Messages](#messages)
    - [Interesting problems](#interesting-problems)
        - [Communicator interface](#communicator-interface)
        - [Handling port switching (UDP)](#handling-port-switching-(UDP))
        - [Sequential working with messages](#sequential-working-with-messages)
- [Testing](#testing)
    - [Testing TCP](#testing-tcp)
        - [Netcat testing](#netcat-testing)
            - [Pre-auth](#pre-auth)
                - [Unavailable commands]
                - [Respecting message parameters]
                - [Negative reply]
            - [While authed](#while-authed)
                - [Sending messages]
                - [Receiving messages]
                - [Joining a channel]
                - [Receiving invalid message]
                - [Receiving error]
                - [Retrying auth]
                - [User interrupt]
        - [Reference server](#reference-server)
        - [Piping a file](#piping-a-file)
    - [Testing UDP](#testing-udp)
        - [Dynamic port switch]
        - [Retransmission]
        - [Confirming messages]
        - [Dumping duplicates]







## Theory
Before we dive into technical aspects of this project, we first need to understand how do the technolgies this project works with work. For the sake of readability and simplicity, I will do my best not to go into too much detail. First, let's start with saying something about the transport protocols themselves.

### Understanding transport protocols
A transport-layer protocol provides for logical communication between application processes running on different hosts. By logical communication, we mean that from an application’s perspective, it is as if the hosts running the processes were directly connected; in reality, the hosts may be on opposite sides of the planet, connected via numerous routers and a wide range of link types. Application processes use the logical communication provided by the transport layer to send 
messages to each other, free from the worry of the details of the physical infrastructure used to carry these messages. [1]
In this project, two types of transport protocols are used - TCP and UDP. Both will be briefly introduced in the following paragraphs.

### TCP Protocol
The Transmission Control Protocol (TCP) is a core protocol of the Internet Protocol Suite. TCP is one of the main protocols in TCP/IP networks. Whereas the Internet Protocol (IP) enables computers to communicate over a network, TCP enables the establishment of a connection between two endpoints and the exchange of streams of data between them with reliability, ordering, and error-checking to ensure that data is delivered accurately and in sequence.

As per [RFC9293], TCP provides a reliable, in-order, byte-stream service to applications. The application byte-stream is conveyed over the network via TCP segments, with each TCP segment sent as an Internet Protocol (IP) datagram. TCP reliability consists of detecting packet losses (via sequence numbers) and errors (via per-segment checksums), as well as correction via automatic retransmission. TCP supports unicast (meaning one-to-one transmission) delivery of data. There are anycast applications that can successfully use TCP without modifications, though there is some risk of instability due to changes of lower-layer forwarding behavior.
TCP is **connection oriented**, though it does not inherently include a liveness detection capability. Data flow is supported bidirectionally over TCP connections, though applications are free to send data only unidirectionally, if they so choose. TCP uses port numbers to identify application services and to multiplex distinct flows between hosts.

Since TCP is connection oriented, it works by establishing a reliable two-way connection between two hosts. The process begins with a handshake to initiate a connection. This handshake process involves three steps: a SYN (synchronize) message sent by the client to the server, a SYN-ACK (synchronize-acknowledge) response from the server to the client, and finally, an ACK (acknowledge) message sent back to the client, establishing a connection. Once this connection is established, data can be sent back and forth between the hosts in segments. Since it also is reliable, we do not need to focus on implementing any retransmission from the application itself. That makes TCP a very suitable choice for bulding upon. 


### UDP Protocol
The User Datagram Protocol (UDP) is another core member of the Internet Protocol Suite, operating alongside Transmission Control Protocol (TCP) within the Transport Layer. Unlike TCP, UDP is **connectionless**, meaning it does not establish a dedicated end-to-end connection before sending data. This fundamental difference makes UDP much simpler and faster than TCP, but it comes at the cost of reliability and ordering.
UDP works by sending datagrams, which are essentially packets of data, without waiting for acknowledgments from the receiving end. There is no handshake process like in TCP, and UDP does not guarantee delivery, order, or error-free communication. Each UDP datagram includes the sender and recipient's IP addresses and ports, allowing the receiving side to know where the packet came from and where it is intended to go, but with minimal overhead.
This makes UDP highly unsuitable for anything like a client for a chat server, because we have to implement some sort of confirmation and/or retransmission of our packets in case they don't get delivered.
For more detailed information about UDP, it is best to refer to [RFC768]

### IPK24-CHAT Protocol
Finally, we can very briefly introduce the IPK24-CHAT protocol.
The IPK24-CHAT protocol defines a high-level behaviour, which is in our case implemented on top of the two previously mentioned protocols, TCP and UDP. As for the network layer protocol requirements, only IPv4 is supported by this protocol.

The protocol defines the following message types to correctly represent the behaviour of each party communicating with this protocol:

| Type name | Description
| --------- | -----------
| `ERR`     | Indicates that an error has occured while processing the other party's last message, this eventually results in the termination of the communication
| `CONFIRM` | Only leveraged in certain protocol variants (UDP) to explicitly confirm the successful delivery of the message to the other party on the application level
| `REPLY`   | Some messages (requests) require a positive/negative confirmation from the other side, this message contains such data
| `AUTH`    | Used for client authentication (signing in) using user-provided username, display name and a password
| `JOIN`    | Represents client's request to join a chat channel by its identifier
| `MSG`     | Contains user display name and a message designated for the channel they're joined in
| `BYE`     | Either party can send this message to indicate that the conversation/connection is to be terminated. This is the final message sent in a conversation (except its potential confirmations in UDP)

The following table shows the mandatory parameters of given message types.

| FMS name | Mandatory message parameters
| -------- | ----------------------------
| `AUTH`   | `Username`, `DisplayName`, `Secret`
| `JOIN`   | `ChannelID`, `DisplayName`
| `ERR`    | `DisplayName`, `MessageContent`
| `BYE`    | *N/A*
| `MSG`    | `DisplayName`, `MessageContent`
| `REPLY`  | `true`, `MessageContent`
| `!REPLY` | `false`, `MessageContent`

The values for the message contents defined above are extracted from the provided user input.

| Message parameter | Max. length | Characters
| ----------------- | ----------- | ----------
| `MessageID`       | `uint16`    | *N/A*
| `Username`        | `20`        | `[A-Z]\|[a-z]\|[0-9]\|-` (e.g., `Abc-23`)
| `ChannelID`       | `20`        | `[A-Z]\|[a-z]\|[0-9]\|-` (e.g., `Abc-23`)
| `Secret`          | `128`       | `[A-Z]\|[a-z]\|[0-9]\|-` (e.g., `Abc-23`)
| `DisplayName`     | `20`        | *Printable characters* (`0x21-7E`)
| `MessageContent`  | `1400`      | *Printable characters with space* (`0x20-7E`)

These parameter identifiers will be used in the sections to follow to denote their locations within the protocol messages or program output.
The notation with braces (`{}`) is used for required parameters, e.g., `{Username}`.
Optional parameters are specified in square brackets (`[]`).
Both braces and brackets must not be a part of the resulting string after the interpolation.
Vertical bar denotes a choice of one of the options available.
Quoted values in braces or brackets are to be interpreted as constants, e.g., `{"Ahoj"|"Hello"}` means either `Ahoj` or `Hello`.

Based on the parameter content limitations defined above, there should never be an issue with IP fragmentation caused by exceeding the default Ethernet MTU of `1500` octets as defined by RFC 894.

This is only a fraction of the full specification, which can be found at the faculty gitea [2]

### Sockets
Sockets provide a way for software to communicate between different processes, either on the same machine or over a network. Think of a socket as an endpoint in a two-way communication link. Each socket is bound to a specific port number and IP address, enabling data to travel to and from a particular service running on a device. In the context of TCP/IP networking, sockets form the bridge between the application layer and the transport layer, allowing applications to send and receive data using the Transmission Control Protocol (TCP) or the User Datagram Protocol (UDP).

#### How Sockets Work
- **TCP Sockets**: When using TCP, sockets operate by establishing a reliable connection between two endpoints. As previously stated, this connection is initiated through a three-way handshake, ensuring both sides are ready for data exchange. Once established, data can be sent across this connection as a continuous stream, with TCP handling ordering, delivery acknowledgment, and retransmission of lost packets. This makes TCP sockets ideal for applications requiring reliable data exchange, such as web servers and clients, email services, and file transfers.

- **UDP Sockets**: In contrast, UDP sockets are connectionless. They send data as independent packets, called datagrams, without establishing a connection or ensuring the packets arrive in order or at all. This makes UDP sockets much faster and more efficient for cases where speed is critical and some data loss is acceptable, such as live audio or video streaming, gaming, or broadcasting messages to multiple recipients.

#### Key Concepts
- **Socket Address**: A combination of an IP address and a port number. The IP address specifies the device, while the port number specifies the particular application or service within the device.
- **Binding**: Associating a socket with a specific address (IP and port). This is necessary for servers so that clients know where to send their messages.
- **Listening**: In TCP, a server socket listens for incoming connections, a step not applicable for UDP sockets.
- **Connecting**: A client initiates a connection to a server (TCP) or sends a message to a specific endpoint (UDP).
- **Sending and Receiving Data**: Once a connection is established (TCP) or immediately (UDP), sockets can be used to exchange data.

#### Practical Use
Sockets are utilized in a wide range of applications, from simple file transfers to complex web applications and real-time communication tools, like this chat client. They abstract the complexity of network communication, providing a simpler interface for developers to build networked applications. Regardless of the underlying protocol (TCP or UDP), sockets enable the transmission of data packets across networks, making them a fundamental element in the implementation of network protocols and services. [3]

## The project goals
The main goal of this project is to implement an application, that will serve as a chat client. This application needs to be able to communicate through TCP and UDP as well, whilst tackling the obstacles of each of those transport protocols. As we can see from the specification of the [IPK24-CHAT] protocol, that consists of several smaller parts needed to reach full functionality, for example:

- Entry point of the program for argument parsing and launching
- The chat client itself, that handles current client state, user input and received message handling
- A TCP communication module
- A UDP communication module + a form of message confirmations and retransmission
- A structure that makes up a Message and is able to serialize and deserialize/parse this message upon sending/receiving

The TCP variant itself doesn't have much obstacles except correctly intercepting message delimiters, however, the UDP variant is in itself significantly more challenging due to its connectionless and unreliable nature.

## Implementation
Looking at the specification and analyzing the situation of this application, C# was chosen as a suitable option, because OOP seems as a suitable solution for this challenge due to wide possibility of abstraction. The following section will provide basic explanation design and implementation, the problems faced during development and my solutions to them. 
However, since the code is widely commented, this documentation will try not to go too much in depth.

### Layout
The main entrance point to the program is in the class *Program.cs*, where arguments are parsed, a chat communicator instance based on the protocol choice is instantiated and the chat client starts running.

Chat client itself is in the class *ChatClient.cs*. In this class, current client state is managed, together with handling user input and listening for new messages. Since the client is able to use various commands, an abstraction for this was made - an interface called *ICommandHandler.cs*. This makes it easy to implement more commands in the future. Every command has its own handler in the chat communicator.

For the actual networking part of the project, *IChatCommunicator.cs* was implemented. This interface serves as a abstraction for the actual protocol used, so that chat client always uses a chat communicator instance, no matter which protocol is used. TCP and UDP both implement this interface in their classes *TcpChatCommunicator.cs* and *UdpChatCommunicator.cs*

To work with Messages, *Message.cs* exists - a class to represent a message, including an enumeration of message types, methods to serialize and deserialize a message from TCP and UDP together with a few helper methods.

### Chat client
By default, the chat client begins in the "start" client state. It runs an asynchronous run method, which uses chat communicator to connect to the server and starts up listener and input handling. The run method also watches, whether ctrl+c (SIGINT) was pressed to abort all operations and terminate. For this, cancellation token is implemented in the chat client.

The message listener keeps listening for new messages and in case a new one arrives, it gets passed to *HandleMessageByType()* method, which checks the message type and reacts accordingly.

The input handling is done by *HandleUserInputAsync()*, which looks whether the input into it was redirected from a file or whether the user is inputting manually and reacts accordingly. This method checks whether the first inputted characted is '/': if not, it tries to send the input as a message, if yes and if it's a valid command, command handler handles it. Moreover, if it is a command which requires a reply from the server, a semaphore deffers all user input until the response. Both concepts will be explained later in this documentation.

### Network communicators

Both communicators implement the *IChatCommunicator.cs* interface slightly differently.

#### TCP Communicator
Since TCP is connection oriented, its *ConnectAsync()* implementation parses an IPv4 from the server address and makes a fixed connection right from the start. It also takes advantage of the C# *TcpClient* class, after connecting, a network stream and a stream reader are estabilished.

Sending messages through TCP happens in this order: the message gets serialized to TCP according to protocol, it gets converted to a byte array and sent through the *WriteAsync()* method of the network stream.

Recieving messages happens with the help of the *StringBuilder* class. The receiver reads one character at a time and adds it to the string builder until it reaches the message delimiter - \r\n. After this, the delimiter is truncated and the read message string is parsed and returned as a message object.

#### UDP Communicator
UDP Communicator works slightly differently, in its *ConnectAsync()*, it only sets a server endpoint for the communicator because of the port switching mechanism. This will be explained in detail in the Problems section.

Sending messages through UDP introduces retransmission loop. Since UDP messages contain messageId due to protocol specification, this communicator implements two *HashSet* sets - one for received message IDs, so that the same message is not processed twice, and one for confirmed sent messages, so that the message is not retransmitted if it gets confirmed. When a message is sent, a helper method *WaitForConfirmationAsync()* gets activated and until the timeout runs out, it keeps checking if a confirmation arrived. If not, the message gets retransmitted and this repeats until the specified number of retries is reached.

Recieving is slightly different here, because the whole byte array of information arrives at once, gets parsed into a Message object and a confirm for the recieved message is sent right away.

### Command handler
The command handler interface includes three important aspects:

1. A boolean that holds the information whether the client needs to wait for a reply for this command
2. *ExecuteCommandAsync()* method responsible for executing the command and sending a message to the server in case of commands like /auth and /join
3. *validateParameters()* method responsible for validating the parameters as the specification requires from the client

Since not all commands necessarily send a message to the server, their implementations slightly differ.

### Messages
The *Message* class is responsible for handling evertyhing message-related. It includes an enumaration of all message types, definition for message parameters and serialization and parse methods for both variants of the transport protocol.

*SerializeToTcp()* uses StringBuilder instance and appends content to it based on the message type, after which it return the serialized string.

*ParseFromTcp()* gets the raw string as a parameter, splits it into an array and returns a message object based on the contents of the array.

*SerializeToUdp()* uses *MemoryStream* and *BinaryWriter* instances to write the bytes into an array, again while appending content based on the message type. This method returns serialized byte array.

*ParseFromUdp()* also uses *MemoryStream*, though here *BinaryReader* is used to read the bytes from the byte array with the help of supporting methods like *GetMessageTypeFromByte()*, *ReadNullTerminatedStrings()* and *ReadNullTerminatedString()* 

### Interesting problems

#### Communicator interface
The first major problem of this implementation arised after fully implementing TCP. Since both communicators were not developed simultaneously, and probably due to not deep enough understanding of the problematics at the time, it turned out that the interface can't be exactly the same for both protocol variants, since *ParseMessage()* for TCP needs a string, though it needs a byte array for UDP. A similar problem arised with *ReceiveMessageAsync()* method. The reason for this problem was bad design of the communicator interface.

Luckily enough, while writing this documentation, the solution introduced itself - TcpChatCommunicator receive function now return a Message object insted of a string because it calls the static Message method for parsing from TCP, and the UdpChatCommunicator now uses *ParseFromUdp()* instead its own parsing method in its receive method. Because of this, the full abstraction was regained and therefore the need for any overloads disappeared, only the two methods from the beginning of this paragraph are left.

#### Handling port switching (UDP)
The communication through UDP using IPK24-CHAT is specific, because after sending an initial authentication message to the server and fixed port 4567, a confirm is received from the same port to this message, but after that, a reply comes back from a dynamically assigned port from the server. This port is reserved for communication just between the server and client. It also means a port switching mechanism needs to be implemented in the client.

This problem was solved by introducing a bool *dynPortAllocated* in the UDP communicator. In the *ConnectAsync()* method, the communicator does not invoke UdpClient *Connect()* function which basically sets a fixed endpoint (since UDP is connectionless), but only sets a manual server endpoint.
In the sending logic, it looks on the bool value indicating whether the port was allocated, and if not, it sends the message with the specified endpoint as a parameter in the *SendAsync()* method.

Then in the receive method, right in the beginning it checks the boolean again. If it is still false and the message type that arrived is a reply, it parses the server port from the *RemoteEndPoint.Port*, creates a new endpoint and only after this the *Connect()* method gets called with the new endpoint passed as the parameter, so the client "binds" to communicate through this endpoint from now on and the boolean gets set to true.

#### Sequential working with messages
During the implementation of this project, the requirement for parallel message handling changed to sequential message handling. For example, if an auth message gets sent, any other user input should be deffered until a reply to this auth message is recieved. Works the same way for join for example.

This was solved by implementing the new, earlier mentioned boolean into command handler interface, which specifies whether the command requires server confirmation or not. The chat client makes use of this in its input handling method and checks this boolean, if it is set to true, a implemented semaphore of the *SemaphoreSlim* class doesn't let any other input be sent until the reply is recieved. However, because of this, a method named *SignalSemaphoreToRelease()* had to be implemented, because sometimes the command fails before it even gets sent (for example if the user inputs wrong parameters with the command etc.). 

## Testing
The testing of this chat client was mostly done on a classical home network, the parts where Wireshark communication is displayed was while connected on the KolejNet BUT network.

The hardware used for testing was an Apple MacBook Air 13, M1, 16GB, 512GB, 7-core GPU (M1, 2020) running a .NET version 8.0.201.
Some test runs have also been executed on the reference virtual machine of NESFIT with the C# Nix development environment active.

The whole correct chat client functionality is pretty much demonstrated on the TCP version of this project, so the UDP version mostly tests correct behaviour of UDP itself. There also will not be any testing of parameter parsing since we were supossed to focus on the networking aspects as much as possible.

### Testing TCP
#### Netcat testing
Since TCP version of this project is text-based, Netcat is a suitable way to test most of the functionality of the client. To demonstrate the functionality of this project, we can use the Client FSM (available at the project specification - see bibliography) and traverse all of its paths while observing behaviour, which is our testing goal.

*The netcat testing command:*
```nc -4 -c -l -v 127.0.0.1 4567```

##### Pre-auth
The first testing scenario is right after launching the client. The client is in start or auth state of the client FSM here.

###### Unavailable commands
The specification says the application must not terminate in case the provided user input is unacceptable at the current client state and must inform the user about such situation. The expected output is always a useful error message.

*Sending a message*\
Input: ```This is a test message```

Output: ```ERR: You must authenticate and join a channel before sending messages.```

*Trying join command*\
Input: ```/join ipk-despair```

Output: ```ERR: You must authenticate before joining a channel.```

*Trying rename command*\
Input: ```/rename xplagiat0b```

Output: ```ERR: You must be authenticated to be able to rename.```
###### Respecting auth parameters
Username and display name can be max 20 characters long, secret max 128 characters. All of these must contain only [A-z]|[a-z]|[0-9]|- (e.g., Abc-23). Expected output is again an error message without termination.

*Invalid username*\
Input: ```/auth thisisover20characterslongname secret coolUser```\
Output: ```ERR: Invalid username or secret```

*Invalid secret*\
Input: ```/auth username sečřet coolUser```\
Output: ```ERR: Invalid username or secret```

*Invalid display name (whitespace)*\
Input: ```/auth username secret  ```\
Output: ```ERR: Invalid number of parameters.```

###### Negative reply
After receiving a negative reply to the auth message, it is expected the client will stay in the auth state, unable to send messages and not terminate.

*Receiving negative reply & sending a message*\
Input: ```/auth username secret coolUser```\
Netcat Input (after receiving): ```REPLY NOK IS Invalid secret.```\
Output: ```Failure: Invalid secret.```

Input: ```Sending a message after auth!```\
Output:```ERR: You must authenticate and join a channel before sending messages.```

###### Receiving an error
The specification says that the client can receive an error message instead of a OK/NOK reply. In this case, the client is supossed to send a BYE message and terminate the connection and program.

*Receiving an error after auth*\
Input: ```/auth username secret coolUser```\
Netcat Input (after receiving): ```ERR FROM Server IS DD gives you 0 points.```\
Output: ```ERR FROM Server: DD gives you 0 points.```

*Whole Netcat record*\
```AUTH username AS coolUser USING secret```\
```ERR FROM Server IS DD gives you 0 points.```\
```BYE```

##### While authed
The second testing scenario happens after a sucessful authentication. The client FSM is in the open state, so authentication process will be left out here, assuming it has been successful.
###### Sending messages
The first test attempts to send a regular message. The expected output is nothing on the client-side, but in Netcat we want to see ```MSG FROM coolUser IS This is a test message.```.

The second test attempts to send a message that is too long, the expected output is an error message.

The third test attemts to send a message containing invalid characters, the expected output is an error message.

*Sending a regular message*\
Input: ```This is a test message.```\
Netcat Output: ```MSG FROM coolUser IS This is a test message.```\

*Sending a too long message*\
Input: ```This is a test message.This is a test message.This is a test message.This is a test message....``` (the message is over 1400 characters long, ommited for simplicity)\
Output: ```ERR: Message is too long. Maximum length is 1400 characters.```\

*Sending invalid message content*\
Input: ```IPP půlsemka byla free```\
Output: ```ERR: Message contains non-printable characters and can contain only printable characters and space.```\

###### Receiving messages
In this test case, netcat was used to send a message. The expected output should be in the format "displayname: content", so in this case, it should be "Server: Hello from netcat."

*Sending message from Netcat*\
Netcat Input: ```MsG fRoM sErVeR iS Hello from netcat.```\
Output: ```Server: Hello from netcat.```\

###### Joining a channel
In this test case, the user tries to join a channel called "mordor". The expected output is a success message in the format "Success: {content}"

*Joining a channel*\
Input: ```/join mordor```\
Netcat Input: ```REPLY OK IS join success.```\
Output: ```Success: join success.```\

We can also try an invalid channel ID.

*Joining a channel with invalid channel ID*\
Input: ```/join GandalfŠedý```\
Output: ```ERR: Invalid channel ID. Channel ID must be between 1 and 20 characters long and contain only alphanumeric characters and hyphens.```\

###### Receiving invalid message
In the specification, the grammar for valid messages is strictly specified. When receiving anything outside of this grammar, the client FSM is supossed to send an ERR message to the server and then gracefully ending the connection with a BYE message.
The expected output to see on netcat is "ERR FROM coolUser IS Invalid message type" and a "BYE" following that.

*Receiving invalid msg*\
Netcat Input: ```I DECLARE BANKRUPTCYYYYYYYYYYYY```\
Output: ```ERR: Unknown message type recieved```\
Netcat Output: ```ERR FROM coolUser IS Unknown message type```
Netcat Output: ```BYE```

###### Receiving error
When receiving an ERR message from the server, the client is expected to send a BYE message and gracefully end the connection.

*Receiving error msg*\
Netcat Input: ```ERR FROM Server IS The server got a stroke.```\
Output: ```ERR FROM Server: The server had a stroke.```
Netcat Output: ```BYE```

###### Receiving BYE
When receiving a BYE message from server, the client is not expected to do anything except close the connection and end.

*Receiving a BYE*\
Netcat Input: ```BYE```\
Output: *client termination*

###### Retrying auth
When already authenticated, the user might think about trying to authenticate again. The expected output to that is an error message informing the user that he already is authenticated, in case his short-term memory does not exist.

*Retrying authentication*\
Input: ```/auth codeBreaker secret noShrtTrmMem```\
Output: ```ERR: You are already authenticated.```

###### User interrupt
One of the ways to end the connection is with a SIGINT - signal interrupt (pressing ctrl+c). The specification requires to gracefully terminate the connection as appropriate for the selected protocol. The expected output is a BYE message from the client.

*Pressing ctrl+c*\
Input: SIGINT\
Netcat Output: ```BYE```







## Bibliography
[1] Kurose, F. J., and Ross, W. K. _Computer Networking: A Top-Down Approach, 8th Edition_. 2021 [cited 2024-03-27].

[RFC9293] Eddy, W. _Transmission Control Protocol (TCP)_ [online]. August 2022. [cited 2024-03-27]. DOI: 10.17487/RFC9293. Available at: https://datatracker.ietf.org/doc/html/rfc9293

[RFC768] Postel, J. _User Datagram Protocol_ [online]. March 1997. [cited 2024-03-27]. DOI: 10.17487/RFC0768. Available at: https://datatracker.ietf.org/doc/html/rfc768

[2] Dolejška, D. _IPK Project 1: Client for a chat server using `IPK24-CHAT` protocol_. 2024 [cited 2024-03-27]. Available at: https://git.fit.vutbr.cz/NESFIT/IPK-Projects-2024/src/branch/master/Project%201/README.md

[3] Board Infinity, _Socket in Computer Network_ [online]. Available at: https://www.boardinfinity.com/blog/socket-in-computer-network/
