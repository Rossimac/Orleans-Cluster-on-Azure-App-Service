using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace Orleans.ShoppingCart.Grains;

[ImplicitStreamSubscription("PetClaim")]
public class PetClaimStreamProcessingGrain(ILogger<PetClaimStreamProcessingGrain> logger)
    : Grain, IPetClaimStreamProcessingGrain
{
    private IAsyncStream<PetClaimsEvent> _stream = null!;

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider("PetClaimsStreamProvider");
        var streamId = StreamId.Create("PetClaim", this.GetPrimaryKeyString());
        _stream = streamProvider.GetStream<PetClaimsEvent>(streamId);

        await _stream.SubscribeAsync(OnNextAsync);

        await base.OnActivateAsync(cancellationToken);
    }

    private async Task OnNextAsync(PetClaimsEvent petClaimsEvent, StreamSequenceToken token)
    {
        try
        {
            var petClaimsDetails = JsonSerializer.Deserialize<PetDetails>(petClaimsEvent.Data);

            if (petClaimsDetails != null)
            {
                var grain = GrainFactory.GetGrain<IPetClaimsGrain>(petClaimsDetails.Id);
                await grain.CreateOrUpdatePetClaimsAsync(petClaimsDetails);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while processing the stream {StreamId}", _stream.StreamId);
            throw;
        }
    }
}

public class PetClaimsEvent
{
    public string Data { get; set; }
}