using System.Runtime.CompilerServices;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Options;

public interface IServiceBus
{
    public string Topic { get; }
    public string SubscriptionWithSessions { get; }
    public string SubscriptionSessionless { get; }
    ServiceBusClient Client { get; }

    ServiceBusSender Sender { get; }
    ServiceBusReceiver ReceiverSessionless { get; }

    Task Reply(ServiceBusReceivedMessage message, ServiceBusMessage reply);
    Task Send(ServiceBusMessage message);
    Task<ServiceBusReceivedMessage> SendAndReceive(ServiceBusMessage message);
}
public class ServiceBusSettings
{
    public string ConnectionString { get; set; }
    public string Topic { get; set; }
    public string Subscription { get; set; }
}

public class ServiceBus : IServiceBus
{
    public string SessionIdSuffix { get; set; } = "";

    public string Topic { get; }
    public string ReplyTopic => $"{Topic}-reply";
    public string SubscriptionSessionless { get; }
    public string SubscriptionWithSessions => $"{SubscriptionSessionless}-sessions";

    public ServiceBusSender Sender { get; }
    public ServiceBusSender ReplySender { get; }
    public ServiceBusClient Client { get; }

    public ServiceBusReceiver ReceiverSessionless { get; }

    // TODO: use message.To or message.SessionId to only receive messages from the same process if in test mode
    public ServiceBus(IOptions<ServiceBusSettings> settings)
    {
        Topic = settings.Value.Topic;
        SubscriptionSessionless = settings.Value.Subscription;

        Client = new ServiceBusClient(settings.Value.ConnectionString);

        Sender = Client.CreateSender(Topic);
        ReplySender = Client.CreateSender(ReplyTopic);
        ReceiverSessionless = Client.CreateReceiver(Topic, SubscriptionSessionless);
    }

    public async Task Send(ServiceBusMessage msg)
    {
        msg.SessionId = msg.SessionId + SessionIdSuffix;
        await Sender.SendMessageAsync(msg);
    }

    public async Task<ServiceBusReceivedMessage> SendAndReceive(ServiceBusMessage msg)
    {
        msg.ReplyToSessionId = $"reply-{Guid.NewGuid().ToString()}";

        await Send(msg);

        await using var session = await Client.AcceptSessionAsync(ReplyTopic, SubscriptionWithSessions, msg.ReplyToSessionId, new ServiceBusSessionReceiverOptions()
        {
            PrefetchCount = 0,
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        });

        if (session is null) return null;

        var reply = await session.ReceiveMessageAsync();

        return reply;
    }

    public async Task Reply(ServiceBusReceivedMessage message, ServiceBusMessage reply)
    {
        if (message.ReplyToSessionId != null)
        {
            reply.SessionId = message.ReplyToSessionId;

            await ReplySender.SendMessageAsync(reply);
        }
    }
}
}

public static class ServiceBusExtensions
{
    public static async Task<ServiceBusReceivedMessage> ReceiveSessionMessage(this ServiceBusClient client, string topic, string subscription, string sessionId)
    {
        await using var sessionReceiver = await client.AcceptSessionAsync(topic, subscription, sessionId);

        if (sessionReceiver == null)
        {
            throw new InvalidOperationException($"no session with id {sessionId} received.");
        }

        var msg = await sessionReceiver.ReceiveMessageAsync();

        if (msg == null)
        {
            throw new InvalidOperationException($"no message for session {sessionId} received.");
        }
        if (msg.ReplyToSessionId == sessionId)
        {
            // skip own message
            msg = await sessionReceiver.ReceiveMessageAsync();
            await sessionReceiver.CompleteMessageAsync(msg);
        }
        return msg;
    }

    public static async IAsyncEnumerable<ServiceBusReceivedMessage> ReceiveAnySessionMessages(
        this ServiceBusClient client, string topic, string subscription, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var session = await client.AcceptNextSessionAsync(topic, subscription);
            // if we accepted a session, it should mean there's a message available, thus short timeout
            var msg = await session.ReceiveMessageAsync(TimeSpan.FromSeconds(1), cancellationToken);

            yield return msg;
        }
    }
