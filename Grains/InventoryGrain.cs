// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace Orleans.ShoppingCart.Grains;

[Reentrant]
internal sealed class InventoryGrain(
    [PersistentState(
            stateName: "Inventory",
            storageName: "grain-storage")]
        IPersistentState<HashSet<string>> state) : Grain, IInventoryGrain
{
    private readonly Dictionary<string, ProductDetails> _productCache = [];
    private readonly StateManager _stateManager = new(state);

    public override async Task OnActivateAsync(CancellationToken cancellationToken) => await PopulateProductCacheAsync(cancellationToken);

    public ValueTask<int> GetProductCount() => new(_productCache.Count);

    public async IAsyncEnumerable<ProductDetails> GetAllProductsAsync()
    {
        // Pick a GUID for a chat room grain and chat room stream
// Get one of the providers which we defined in our config
        var streamProvider = this.GetStreamProvider("PetClaimsStreamProvider");
// Get the reference to a stream
        var streamId = StreamId.Create("PetClaim", 0);
        var stream = streamProvider.GetStream<int>(streamId);

        var evt = new ClaimEvent
        {
            TenantId = "some tenant!",
            RunId = 212112,
            Line = "some line!"
        };

        //await stream.OnNextAsync(JsonSerializer.SerializeToUtf8Bytes(evt));
        //await stream.OnNextAsync(evt);
        await stream.OnNextAsync(Random.Shared.Next());

        // We await this to make the compiler happy.
        await Task.CompletedTask;

        var values = _productCache.Values.ToList();
        foreach (var value in values)
        {
            yield return value;
        }
    }

    public async ValueTask AddOrUpdateProductAsync(ProductDetails product)
    {
        ArgumentNullException.ThrowIfNull(product.Id);
        state.State.Add(product.Id);
        _productCache[product.Id] = product;

        await _stateManager.WriteStateAsync();
    }

    public async ValueTask RemoveProductAsync(string productId)
    {
        state.State.Remove(productId);
        _productCache.Remove(productId);

        await _stateManager.WriteStateAsync();
    }

    private async Task PopulateProductCacheAsync(CancellationToken cancellationToken)
    {
        if (state is not { State.Count: > 0 })
        {
            return;
        }

        await Parallel.ForEachAsync(
            state.State,
            new ParallelOptions
            {
                TaskScheduler = TaskScheduler.Current,
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 32,
            },
            async (id, ct) =>
            {
                var productGrain = GrainFactory.GetGrain<IProductGrain>(id);
                _productCache[id] = await productGrain.GetProductDetailsAsync().WaitAsync(ct);
            });
    }
}
