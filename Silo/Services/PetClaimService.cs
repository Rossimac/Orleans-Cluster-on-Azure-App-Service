// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

namespace Orleans.ShoppingCart.Silo.Services;

public sealed class PetClaimService : BaseClusterService
{
    public PetClaimService(
        IHttpContextAccessor httpContextAccessor, IClusterClient client) :
        base(httpContextAccessor, client)
    {
    }

    public Task CreateOrUpdatePetClaimsAsync(PetDetails petDetails) =>
        _client.GetGrain<IPetClaimsGrain>(petDetails.Id).CreateOrUpdatePetClaimsAsync(petDetails);

    public Task CreateOrUpdatePetClaimsAsync(Guid petId) =>
        _client.GetGrain<IPetClaimsGrain>(petId).GetPetClaimsDetailsAsync();
}
