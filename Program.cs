//////using System;
//////using System.Net.WebSockets;
//////using System.Threading;
//////using System.Threading.Tasks;
//////using Azure.AI.Projects;
//////using Azure.Messaging.WebPubSub.Clients;
//////using Google.Protobuf;
//////using Webpubsub;
//////using WebPubSub; // Namespace from generated Protobuf files

//////class Program
//////{
//////    private const string Group = "protobuf-client-group";
//////    private const string Uri = "wss://rewe456txrrtf.webpubsub.azure.com/client/hubs/Hub?access_token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJ3c3M6Ly9yZXdlNDU2dHhycnRmLndlYnB1YnN1Yi5henVyZS5jb20vY2xpZW50L2h1YnMvSHViIiwiaWF0IjoxNzQwNjQ5ODQyLCJleHAiOjE3NDA2NTM0NDIsInJvbGUiOlsid2VicHVic3ViLnNlbmRUb0dyb3VwIiwid2VicHVic3ViLmpvaW5MZWF2ZUdyb3VwIl19.qKgqTFL0pZNa27gsUn2PGeUzWiMhs7XkruRoGjOZezc"; // Replace with actual Web PubSub Client Access URL

//////    static async Task Main()
//////    {
//////        ClientWebSocket webSocket = new();
//////        webSocket.Options.AddSubProtocol("protobuf.webpubsub.azure.v1");

//////        await webSocket.ConnectAsync(new Uri(Uri), CancellationToken.None);
//////        Console.WriteLine("Connected to Web PubSub!");

//////        // Start listening for messages
//////        _ = Task.Run(async () =>
//////        {
//////            while (webSocket.State == WebSocketState.Open)
//////            {
//////                var buffer = new byte[8192];
//////                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

//////                if (result.MessageType == WebSocketMessageType.Binary)
//////                {
//////                    var messageBytes = new byte[result.Count];
//////                    Array.Copy(buffer, 0, messageBytes, 0, result.Count);

//////                    try
//////                    {
//////                        var msg = DownstreamMessage.Parser.ParseFrom(messageBytes);
//////                        Console.WriteLine($"Received message: {msg}");
//////                    }
//////                    catch (Exception ex)
//////                    {
//////                        Console.WriteLine($"Error parsing message: {ex.Message}");
//////                    }
//////                }
//////                else if (result.MessageType == WebSocketMessageType.Close)
//////                {
//////                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
//////                    break;
//////                }
//////            }
//////        });

//////        // Wait for connection establishment
//////        await Task.Delay(2000);

//////        // Send join group message
//////        UpstreamMessage joiningMessage = new()
//////        {
//////            JoinGroupMessage = new Webpubsub.JoinGroupMessage()
//////            {
//////                Group = Group,
//////            }
//////        };

//////        await webSocket.SendAsync(joiningMessage.ToByteArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
//////        Console.WriteLine("Join group message sent");

//////        // Wait for join to be processed
//////        await Task.Delay(1000);

//////        // Create and send data message
//////        UpstreamMessage dataMessage = new()
//////        {
//////            EventMessage = new EventMessage()
//////            {
//////                EventName = "chatMessage",
//////                Data = new MessageData()
//////                {
//////                    TextData = "Hello from protobuf dheeraj"
//////                }
//////            }
//////        };

//////        await webSocket.SendAsync(dataMessage.ToByteArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
//////        Console.WriteLine("Data message sent");
//////    }
//////}
////using System;
////using System.Net.WebSockets;
////using System.Threading;
////using System.Threading.Tasks;
////using Azure.Messaging.WebPubSub;
////using Azure.Messaging.WebPubSub.Clients;
////using Google.Protobuf;
////using Webpubsub;
////using WebPubSub; // Make sure this matches your generated Protobuf namespace

////class Program
////{
////    private const string Group = "protobuf-client-group";
////    private static Uri WebSocketUri;

////    static async Task Main()
////    {
////        // Step 1: Generate a fresh access token dynamically
////        WebSocketUri = await GetFreshWebSocketUri();

////        // Step 2: Initialize WebSocket
////        using ClientWebSocket webSocket = new();
////        webSocket.Options.AddSubProtocol("protobuf.webpubsub.azure.v1");

////        try
////        {
////            await webSocket.ConnectAsync(WebSocketUri, CancellationToken.None);
////            Console.WriteLine("✅ Connected to Web PubSub!");

////            // Step 3: Start listening for messages
////            _ = ListenForMessages(webSocket);

////            // Step 4: Send a "Join Group" message
////            await SendJoinGroupMessage(webSocket);

////            // Step 5: Send a test chat message
////            await SendChatMessage(webSocket, "Hello from protobuf!");

////            // Keep running
////            await Task.Delay(Timeout.Infinite);
////        }
////        catch (Exception ex)
////        {
////            Console.WriteLine($"❌ Error: {ex.Message}");
////        }
////    }

////    /// <summary>
////    /// Generates a fresh WebSocket URI with a valid access token.
////    /// </summary>
////    private static async Task<Uri> GetFreshWebSocketUri()
////    {
////        string connectionString = "Endpoint=https://rewe456txrrtf.webpubsub.azure.com;AccessKey=7QfKlCDoKmkXiptJFe2wZ17XK3U1yphRkK6g0UF1iiWcq0AQ7jJjJQQJ99BBACYeBjFXJ3w3AAAAAWPSXNot;Version=1.0;"; // Get this from Azure Portal
////        string hubName = "sampath"; // Change this to your hub name

////        var serviceClient = new WebPubSubServiceClient(connectionString, hubName);
////        var uri = await serviceClient.GetClientAccessUriAsync();

////        Console.WriteLine($"🔄 Generated fresh WebSocket URI: {uri.AbsoluteUri}");
////        return uri;
////    }

////    /// <summary>
////    /// Listens for incoming WebSocket messages and parses them.
////    /// </summary>
////    private static async Task ListenForMessages(ClientWebSocket webSocket)
////    {
////        byte[] buffer = new byte[8192];

////        while (webSocket.State == WebSocketState.Open)
////        {
////            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

////            if (result.MessageType == WebSocketMessageType.Binary)
////            {
////                var messageBytes = new byte[result.Count];
////                Array.Copy(buffer, messageBytes, result.Count);

////                try
////                {
////                    var msg = DownstreamMessage.Parser.ParseFrom(messageBytes);
////                    Console.WriteLine($"📩 Received message: {msg}");
////                }
////                catch (Exception ex)
////                {
////                    Console.WriteLine($"⚠️ Error parsing message: {ex.Message}");
////                }
////            }
////            else if (result.MessageType == WebSocketMessageType.Close)
////            {
////                Console.WriteLine($"🔴 WebSocket closed: {result.CloseStatusDescription}");
////                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
////                break;
////            }
////        }
////    }

////    /// <summary>
////    /// Sends a "Join Group" message using Protobuf.
////    /// </summary>
////    private static async Task SendJoinGroupMessage(ClientWebSocket webSocket)
////    {
////        if (webSocket.State == WebSocketState.Open)
////        {
////            var joinMessage = new UpstreamMessage
////            {
////                JoinGroupMessage = new Webpubsub.JoinGroupMessage
////                {
////                    Group = Group
////                }
////            };

////            await webSocket.SendAsync(joinMessage.ToByteArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
////            Console.WriteLine("✅ Sent: Join group message");
////        }
////        else
////        {
////            Console.WriteLine("⚠️ WebSocket closed before sending join message.");
////        }
////    }

////    /// <summary>
////    /// Sends a chat message to the group.
////    /// </summary>
////    private static async Task SendChatMessage(ClientWebSocket webSocket, string message)
////    {
////        if (webSocket.State == WebSocketState.Open)
////        {
////            var chatMessage = new UpstreamMessage
////            {
////                EventMessage = new EventMessage
////                {
////                    EventName = "chatMessage",
////                    Data = new MessageData
////                    {
////                        TextData = message
////                    }
////                }
////            };

////            await webSocket.SendAsync(chatMessage.ToByteArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
////            Console.WriteLine($"✅ Sent: Chat message - \"{message}\"");
////        }
////        else
////        {
////            Console.WriteLine("⚠️ WebSocket closed before sending chat message.");
////        }
////    }
////}
//using System;
//using System.Threading.Tasks;
//using Azure.Messaging.WebPubSub.Clients;
//using WebPubSub.Client.Protobuf;

//class Program
//{
//    private const string Group = "protobuf-client-group";
//    private const string Uri = "wss://rewe456txrrtf.webpubsub.azure.com/client/hubs/Hub?access_token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJ3c3M6Ly9yZXdlNDU2dHhycnRmLndlYnB1YnN1Yi5henVyZS5jb20vY2xpZW50L2h1YnMvSHViIiwiaWF0IjoxNzQwNjUxNDYyLCJleHAiOjE3NDA2NTUwNjIsInJvbGUiOlsid2VicHVic3ViLnNlbmRUb0dyb3VwIiwid2VicHVic3ViLmpvaW5MZWF2ZUdyb3VwIl19.iW0qYqILyEItX9mB4nhQRzArbOFrTsLFavHLdMxNcOc"; // Replace with actual connection string

//    static async Task Main()
//    {
//        // Create the Web PubSub client using the Protobuf protocol
//        var serviceClient = new WebPubSubClient(new Uri(Uri), new WebPubSubClientOptions
//        {
//            Protocol = new WebPubSubProtobufProtocol() // Use WebPubSubProtobufReliableProtocol() for reliable messaging
//        });

//        // Subscribe to connection events
//        serviceClient.Connected += arg =>
//        {
//            Console.WriteLine($"Connected with connection id: {arg.ConnectionId}");
//            return Task.CompletedTask;
//        };

//        serviceClient.Disconnected += arg =>
//        {
//            Console.WriteLine($"Disconnected from connection id: {arg.ConnectionId}");
//            return Task.CompletedTask;
//        };

//        // Subscribe to group messages
//        serviceClient.GroupMessageReceived += arg =>
//        {
//            Console.WriteLine($"Received Protobuf message: {arg.Message.Data}");
//            return Task.CompletedTask;
//        };

//        // Connect to Web PubSub and join the group
//        await serviceClient.StartAsync();
//        await serviceClient.JoinGroupAsync(Group);

//        while (true)
//        {
//            Console.WriteLine("Enter the message to send or just press enter to stop:");
//            var message = Console.ReadLine();

//            if (!string.IsNullOrEmpty(message))
//            {
//                // Send message as Protobuf binary data
//                await serviceClient.SendToGroupAsync(Group, BinaryData.FromString(message), WebPubSubDataType.Binary);
//            }
//            else
//            {
//                // Leave the group and disconnect
//                await serviceClient.LeaveGroupAsync(Group);
//                await serviceClient.StopAsync();
//                break;
//            }
//        }
//    }
//}
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.WebPubSub.Clients;
using Google.Protobuf;
using WebPubSub.Client.Protobuf;
using Webpubsub; // Ensure this namespace contains the correct Protobuf definitions

class Program
{
    private const string Group = "protobuf-client-group";
    private const string Uri = "wss://rewe456txrrtf.webpubsub.azure.com/client/hubs/Hub?access_token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJ3c3M6Ly9yZXdlNDU2dHhycnRmLndlYnB1YnN1Yi5henVyZS5jb20vY2xpZW50L2h1YnMvSHViIiwiaWF0IjoxNzQwNzI3Nzg0LCJleHAiOjE3NDA3MzEzODQsInJvbGUiOlsid2VicHVic3ViLnNlbmRUb0dyb3VwIiwid2VicHVic3ViLmpvaW5MZWF2ZUdyb3VwIl19.IvxpKTwz4X6PAjp_jFAhi02lSD6MZ7Xo5rtitiReqC4"; // Replace with actual connection string

    static async Task Main()
    {
        // Create the Web PubSub client using the Protobuf protocol
        var serviceClient = new WebPubSubClient(new Uri(Uri), new WebPubSubClientOptions
        {
            Protocol = new WebPubSubProtobufProtocol() // Use WebPubSubProtobufReliableProtocol() for reliable messaging
        });

        // Subscribe to connection events
        serviceClient.Connected += arg =>
        {
            Console.WriteLine($"Connected with connection id: {arg.ConnectionId}");
            return Task.CompletedTask;
        };

        serviceClient.Disconnected += arg =>
        {
            Console.WriteLine($"Disconnected from connection id: {arg.ConnectionId}");
            return Task.CompletedTask;
        };

        // Subscribe to group messages
        serviceClient.GroupMessageReceived += arg =>
        {
            Console.WriteLine($"Received Protobuf message: {arg.Message.Data}");
            return Task.CompletedTask;
        };

        // Connect to Web PubSub and join the group
        await serviceClient.StartAsync();
        await serviceClient.JoinGroupAsync(Group);

        // Send a join group message using Protobuf serialization
        UpstreamMessage joiningMessage = new()
        {
            JoinGroupMessage = new Webpubsub.JoinGroupMessage()
            {
                Group = Group,
            }
        };
        await serviceClient.SendToGroupAsync(Group, BinaryData.FromBytes(joiningMessage.ToByteArray()), WebPubSubDataType.Binary);

        Console.WriteLine("Join group message sent");
        await Task.Delay(1000);

        while (true)
        {
            Console.WriteLine("Enter the message to send or just press enter to stop:");
            var message = Console.ReadLine();

            if (!string.IsNullOrEmpty(message))
            {
                // Create and send data message
                UpstreamMessage dataMessage = new()
                {
                    EventMessage = new EventMessage()
                    {
                        Data = new MessageData()
                        {
                            TextData = message
                        }
                    }
                };
                await serviceClient.SendToGroupAsync(Group, BinaryData.FromBytes(dataMessage.ToByteArray()), WebPubSubDataType.Binary);
                Console.WriteLine("Data message sent");
            }
            else
            {
                // Leave the group and disconnect
                await serviceClient.LeaveGroupAsync(Group);
                await serviceClient.StopAsync();
                break;
            }
        }
    }
}
