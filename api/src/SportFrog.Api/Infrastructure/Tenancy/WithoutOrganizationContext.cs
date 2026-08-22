namespace SportFrog.Api.Infrastructure.Tenancy;

/// <summary>
/// Marks an endpoint that legitimately runs without an organization context:
/// signing in, registering an organization, the health probe, the public
/// view.
///
/// The default is the opposite — every endpoint requires a context unless it
/// says otherwise — so that forgetting to think about it produces a refusal
/// rather than an operation running with no isolation.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class WithoutOrganizationContextAttribute : Attribute;

public static class WithoutOrganizationContextExtensions
{
    /// <summary>
    /// Declares that this endpoint runs without an organization context.
    /// Reads at the call site as the exception it is.
    /// </summary>
    public static TBuilder WithoutOrganizationContext<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new WithoutOrganizationContextAttribute());
        return builder;
    }
}
