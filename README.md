```c#
using Azure.Messaging.ServiceBus;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace ServiceBus.DLQ.Reader
{
    public class Demo
    {
        public static async Task Main(string[] args)
        {
            var reader = new DemoSessionProcessReader(
                new ServiceBusClient("Endpoint=sb://*****.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=*****"), 
                "mikes.test.queue.session");

            try
            {
                await reader.CreateSessionProcessors(10, 10, 10, 30, false);

                Console.WriteLine("Press any key to stop...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// A class for grouping messages for a particular session.
    /// </summary>
    public class GroupedSession
    {
        public DateTime Timestamp { get; set; } // First message timestamp
        public List<ServiceBusReceivedMessage> Messages { get; set; } = new();
        public ProcessSessionMessageEventArgs SessionContext { get; set; } // Store session context
    }

    public class DemoSessionProcessReader
    {
        private readonly List<ServiceBusSessionProcessor> _processorList = new();
        private readonly ServiceBusClient _client;
        private readonly string _queue;
        private static bool _autoComplete = false;
        private static readonly object _lock = new();

        private static readonly IDictionary<string, GroupedSession> _sessions = new Dictionary<string, GroupedSession>();
        private static readonly ConcurrentQueue<(string SessionId, ServiceBusReceivedMessage Message, ProcessSessionMessageEventArgs SessionContext)> _messageQueue = new();
        private static readonly CancellationTokenSource _cts = new();

        public DemoSessionProcessReader(ServiceBusClient client, string queue)
        {
            _client = client;
            _queue = queue;

            // Start background message processor
            Task.Run(() => ProcessMessagesAsync(_cts.Token));
        }

        public async Task CreateSessionProcessors(int numberOfProcessors, int concurrentSessionsPerProcessor, int prefetchCount, int sessionTimeout, bool autoCompleteMessages)
        {
            _autoComplete = autoCompleteMessages;

            for (int i = 0; i < numberOfProcessors; i++)
            {
                var processor = _client.CreateSessionProcessor(_queue, new ServiceBusSessionProcessorOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.PeekLock,
                    PrefetchCount = prefetchCount,
                    AutoCompleteMessages = autoCompleteMessages,
                    SessionIdleTimeout = TimeSpan.FromSeconds(sessionTimeout),
                    MaxConcurrentSessions = concurrentSessionsPerProcessor
                });

                processor.ProcessMessageAsync += MessageHandler;
                processor.ProcessErrorAsync += ErrorHandler;

                _processorList.Add(processor);
            }

            foreach (var processor in _processorList)
            {
                await processor.StartProcessingAsync();
            }
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            Console.WriteLine($"Error in processor: {args.Exception}");
            return Task.CompletedTask;
        }

        private async Task MessageHandler(ProcessSessionMessageEventArgs args)
        {
            var sessionId = args.Message.SessionId;
            Console.WriteLine($"Received message for session: {sessionId}");

            lock (_lock)
            {
                if (!_sessions.ContainsKey(sessionId))
                {
                    _sessions[sessionId] = new GroupedSession
                    {
                        Timestamp = DateTime.UtcNow,
                        SessionContext = args,
                        Messages = new List<ServiceBusReceivedMessage> { args.Message }
                    };
                }
                else
                {
                    _sessions[sessionId].Messages.Add(args.Message);
                }
            }

            // Add message to processing queue
            _messageQueue.Enqueue((sessionId, args.Message, args));
        }

        private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (_messageQueue.TryDequeue(out var item))
                {
                    try
                    {
                        // Simulate processing delay
                        await Task.Delay(500);

                        // Ensure the session is still active before completing
                        if (_sessions.ContainsKey(item.SessionId))
                        {
                            Console.WriteLine($"Completing message for session: {item.SessionId}");
                            await item.SessionContext.CompleteMessageAsync(item.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing message: {ex.Message}");
                    }
                }

                await Task.Delay(100);
            }
        }

        public async Task StopProcessingAsync()
        {
            _cts.Cancel();

            foreach (var processor in _processorList)
            {
                await processor.StopProcessingAsync();
            }
        }
    }
}

```



****Updated code: ****
 Messages are NOT completed as they arrive → They are stored in _messages and processed later in batches.
Messages are grouped by sessionId → The _messages dictionary ensures messages from the same session are stored together.
Batch processing is handled in memory before database updates → The ProcessBatchAsync method updates the database before completing messages.
 Messages are only completed after successful processing → If the app crashes before processing, messages remain in the queue and are not lost.

```c#
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

public class SessionMessageProcessor
{
    private static readonly object _lock = new object();

    // Dictionary to store session messages
    private static IDictionary<string, (GroupedSession session, SemaphoreSlim semaphore)> _messages
        = new Dictionary<string, (GroupedSession, SemaphoreSlim)>();

    private readonly ServiceBusClient _client;
    private readonly ServiceBusSessionProcessor _processor;

    public SessionMessageProcessor(string connectionString, string queueName)
    {
        _client = new ServiceBusClient(connectionString);
        _processor = _client.CreateSessionProcessor(queueName, new ServiceBusSessionProcessorOptions
        {
            AutoCompleteMessages = false, // Ensure manual completion
            MaxConcurrentSessions = 5,
            MaxConcurrentCallsPerSession = 1,
            SessionIdleTimeout = TimeSpan.FromSeconds(30)
        });

        _processor.ProcessMessageAsync += MessageHandler;
        _processor.ProcessErrorAsync += ErrorHandler;
    }

    public async Task StartAsync()
    {
        await _processor.StartProcessingAsync();
        Task.Run(() => BackgroundBatchProcessing());
    }

    public async Task StopAsync()
    {
        await _processor.StopProcessingAsync();
        await _client.DisposeAsync();
    }

    private async Task MessageHandler(ProcessSessionMessageEventArgs args)
    {
        string sessionId = args.SessionId;
        var message = args.Message;

        lock (_lock)
        {
            if (!_messages.ContainsKey(sessionId))
            {
                _messages[sessionId] = (new GroupedSession
                {
                    Timestamp = DateTime.Now,
                    Messages = new List<ServiceBusReceivedMessage> { message },
                    Args = args //  Store args here to prevent null references!
                }, new SemaphoreSlim(1, 1));
            }
            else
            {
                _messages[sessionId].session.Messages.Add(message);
            }
        }
    }

    private async Task ProcessBatchAsync(string sessionId)
    {
        if (!_messages.ContainsKey(sessionId)) return;

        var (session, semaphore) = _messages[sessionId];

        await semaphore.WaitAsync();
        try
        {
            // Ensure session.Args is not null before calling RenewSessionLockAsync()
            if (session.Args == null)
            {
                Console.WriteLine($"Session Args is null for sessionId: {sessionId}");
                return;
            }

            await session.Args.RenewSessionLockAsync();

            // Simulate batch processing (e.g., updating database)
            await UpdateDatabaseAsync(session.Messages);

            //  Complete messages only after processing is successful
            foreach (var msg in session.Messages)
            {
                await session.Args.CompleteMessageAsync(msg);
            }

            //  Remove session from dictionary after processing
            lock (_lock)
            {
                _messages.Remove(sessionId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing batch for session {sessionId}: {ex.Message}");
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task BackgroundBatchProcessing()
    {
        while (true)
        {
            List<string> sessionIds;
            lock (_lock)
            {
                sessionIds = _messages.Keys.ToList();
            }

            foreach (var sessionId in sessionIds)
            {
                await ProcessBatchAsync(sessionId);
            }

            await Task.Delay(1000); // Adjust delay as needed
        }
    }

    private async Task UpdateDatabaseAsync(List<ServiceBusReceivedMessage> messages)
    {
        // Simulate database update
        await Task.Delay(500);
        Console.WriteLine($"Processed {messages.Count} messages.");
    }

    private Task ErrorHandler(ProcessErrorEventArgs args)
    {
        Console.WriteLine($"Error: {args.Exception.Message}");
        return Task.CompletedTask;
    }
}

public class GroupedSession
{
    public DateTime Timestamp { get; set; }
    public List<ServiceBusReceivedMessage> Messages { get; set; } = new List<ServiceBusReceivedMessage>();
    public ProcessSessionMessageEventArgs Args { get; set; } //  Ensure this is stored correctly
}

// Usage Example
class Program
{
    static async Task Main(string[] args)
    {
        // Securely replace with your Service Bus details
        string connectionString = "Your_Secure_Connection_String";
        string queueName = "YourQueueName";

        var processor = new SessionMessageProcessor(connectionString, queueName);
        await processor.StartAsync();

        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();

        await processor.StopAsync();
    }
}
```

