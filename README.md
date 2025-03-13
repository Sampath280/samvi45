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
