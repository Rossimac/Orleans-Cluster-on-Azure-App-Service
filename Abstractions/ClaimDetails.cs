// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

namespace Orleans.ShoppingCart.Abstractions;

public class ClaimDetails
{
    public string PolicyNumber { get; set; }
    public Guid PetId { get; set; }
    public string ClaimId { get; set; }
    public string CustomerName { get; set; }
    public decimal ClaimAmount { get; set; }
    public DateTime DateOfLoss { get; set; }
    public DateTime ClaimDate { get; set; }
    public string Status { get; set; }
    public string Description { get; set; }
}