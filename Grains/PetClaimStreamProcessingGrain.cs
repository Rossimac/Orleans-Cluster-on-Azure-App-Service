using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace Orleans.ShoppingCart.Grains;

[ImplicitStreamSubscription("PetClaim")]
public class PetClaimStreamProcessingGrain : Grain, IPetClaimStreamProcessingGrain
{
    private IAsyncStream<ClaimEvent> _stream = null!;
    private readonly ILogger<PetClaimStreamProcessingGrain> _logger;

    public PetClaimStreamProcessingGrain(ILogger<PetClaimStreamProcessingGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider("PetClaimsStreamProvider");
        var streamId = StreamId.Create("PetClaim", this.GetPrimaryKeyString());
        _stream = streamProvider.GetStream<ClaimEvent>(streamId);

        await _stream.SubscribeAsync(OnNextAsync);

        await base.OnActivateAsync(cancellationToken);
    }

    private async Task OnNextAsync(ClaimEvent petClaimsEvent, StreamSequenceToken token)
    {
        try
        {
            _logger.LogInformation("Received {Line} on the stream {StreamId}", petClaimsEvent.Line, _stream.StreamId);

            //var petClaimsDetails = JsonSerializer.Deserialize<ClaimEvent>(petClaimsEvent);

            // if (petClaimsDetails != null)
            // {
            //     var grain = GrainFactory.GetGrain<IPetClaimsGrain>(petClaimsDetails.Line);
            //     await grain.CreateOrUpdatePetClaimsAsync(petClaimsDetails);
            // }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while processing the stream {StreamId}", _stream.StreamId);
            throw;
        }
    }
}