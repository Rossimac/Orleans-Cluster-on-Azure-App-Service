using System.Text.Json;
using Azure.Messaging.EventHubs;
using Orleans.Serialization;
using Orleans.Streaming.EventHubs;
using Orleans.Streams;

namespace Orleans.ShoppingCart.Silo;

// Custom EventHubDataAdapter that serialize event using System.Text.Json
public class CustomDataAdapter : EventHubDataAdapter
{
    public CustomDataAdapter(Serializer serializer) : base(serializer)
    {
    }

    public override string GetPartitionKey(StreamId streamId)
        => streamId.ToString();

    public override StreamId GetStreamIdentity(EventData queueMessage)
    {
        var partition = Guid.Parse(queueMessage.PartitionKey);
        var ns = (string) queueMessage.Properties["StreamNamespace"];
        var streamIdentity = StreamId.Create(ns, partition);

        return streamIdentity;
    }

    public override EventData ToQueueMessage<T>(StreamId streamId, IEnumerable<T> events, StreamSequenceToken token, Dictionary<string, object> requestContext)
    {
        return null!;
        throw new NotSupportedException("This adapter only supports read");
    }

    protected override IBatchContainer GetBatchContainer(EventHubMessage eventHubMessage)
        => new CustomBatchContainer(eventHubMessage);
}

[GenerateSerializer, Immutable]
public sealed class CustomBatchContainer : IBatchContainer
{
    [Id(0)]
    private readonly EventHubMessage _eventHubMessage;

    [Id(1)]
    public StreamSequenceToken SequenceToken { get; }

    public StreamId StreamId => _eventHubMessage.StreamId;

    public CustomBatchContainer(EventHubMessage eventHubMessage)
    {
        _eventHubMessage = eventHubMessage;
        SequenceToken = new EventHubSequenceTokenV2(_eventHubMessage.Offset, _eventHubMessage.SequenceNumber, 0);
    }

    public IEnumerable<Tuple<T, StreamSequenceToken>> GetEvents<T>()
    {
        try
        {
            var evt = JsonSerializer.Deserialize<T>(_eventHubMessage.Payload)!;
            return new[] { Tuple.Create(evt, SequenceToken) };
        }
        catch (Exception)
        {
            return Array.Empty<Tuple<T, StreamSequenceToken>>();
        }
    }

    public bool ImportRequestContext() => false;
}
