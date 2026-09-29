namespace Brownfield.Api;

/// <summary>
/// Predates adoption, and reads the tenant from the request body - exactly what LOC_01 forbids
/// of a change. It is here to prove that existing code does not block a first cycle: adoption is
/// free, and only a diff that adds another instance is a finding.
/// </summary>
public sealed class OrderHandler
{
    public string Handle(OrderRequest request) => $"{request.TenantId}:{request.OrderId}";
}

public sealed record OrderRequest(string TenantId, string OrderId);
