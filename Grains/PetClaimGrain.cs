namespace Orleans.ShoppingCart.Grains;

internal sealed class PetClaimsGrain(
    [PersistentState(
            stateName: "PetClaim",
            storageName: "pet-claim-grain-storage")]
        IPersistentState<PetDetails> state) : Grain, IPetClaimsGrain
{
    private readonly StateManager _stateManager = new(state);

    public async Task CreateOrUpdatePetClaimsAsync(PetDetails petDetails)
    {
        state.State = petDetails;

        await _stateManager.WriteStateAsync();
    }

    public Task<PetDetails> GetPetClaimsDetailsAsync() => Task.FromResult(state.State);
}
