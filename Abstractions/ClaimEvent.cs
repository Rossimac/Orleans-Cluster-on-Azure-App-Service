namespace Orleans.ShoppingCart.Abstractions;

[GenerateSerializer]
public class ClaimEvent
{
    [Id(0)]
    public long RunId { get; set; }
    [Id(1)]
    public string TenantId { get; set; }
    [Id(2)]
    public string Line { get; set; }     // raw CSV line
}