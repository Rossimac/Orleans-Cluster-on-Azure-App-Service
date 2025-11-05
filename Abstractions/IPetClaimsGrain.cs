// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

namespace Orleans.ShoppingCart.Abstractions;

public interface IPetClaimsGrain : IGrainWithGuidKey
{
    Task CreateOrUpdatePetClaimsAsync(PetDetails petDetails);

    Task<PetDetails> GetPetClaimsDetailsAsync();
}
