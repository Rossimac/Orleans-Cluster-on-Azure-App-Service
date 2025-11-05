// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

namespace Orleans.ShoppingCart.Abstractions;
using Orleans;
using System;
using System.Collections.Generic;

[GenerateSerializer]
public sealed record PetDetails
{
    [Id(0)]
    public Guid Id { get; set; } // Pet ID

    [Id(1)]
    public string Name { get; set; } = string.Empty;

    [Id(2)]
    public Guid PolicyNumber { get; set; }

    [Id(5)]
    public List<PetClaimDetails> Claims { get; set; } = new();
}

[GenerateSerializer]
public sealed record PetClaimDetails
{
    [Id(0)]
    public Guid Id { get; set; }

    [Id(1)]
    public int TermNumber { get; set; }

    [Id(2)]
    public string ClaimNumber { get; set; } = string.Empty;

    [Id(3)]
    public DateTime DateOfClaim { get; set; }

    [Id(4)]
    public string? Description { get; set; }

    [Id(5)]
    public decimal ClaimedAmount { get; set; }

    [Id(6)]
    public decimal? ApprovedAmount { get; set; }

    [Id(7)]
    public string Status { get; set; } = "Submitted"; // "Submitted", "Approved", etc.

    [Id(8)]
    public string? Veterinarian { get; set; }

    [Id(9)]
    public DateTime CreatedAt { get; set; }
}
