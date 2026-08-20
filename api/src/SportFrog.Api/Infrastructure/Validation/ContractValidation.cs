using FluentValidation;

namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>
/// Marks an endpoint whose request must not be checked against a validator.
///
/// Signing in is the case this exists for. Its rejections are deliberately
/// indistinguishable, and a validation failure would break that: a 400 saying
/// the address is malformed tells the caller, by contrast, that any 401 they
/// receive came from an address that *was* well formed. That difference is
/// enough to ask which addresses are registered.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class WithoutContractValidationAttribute : Attribute;

public static class ContractValidation
{
    /// <summary>
    /// Validates every request that has a validator, for every endpoint in the
    /// group.
    /// </summary>
    /// <remarks>
    /// Applied to the group rather than endpoint by endpoint, so a new
    /// operation is covered by existing rather than by its author having
    /// remembered (DD-07). Writing a validator is what turns validation on;
    /// nothing has to be wired at the endpoint.
    ///
    /// The alternative — an explicit call per route — is validation that
    /// depends on discipline, which is the arrangement this design rejects
    /// everywhere else.
    /// </remarks>
    public static RouteGroupBuilder ValidateContracts(this RouteGroupBuilder group)
    {
        group.AddEndpointFilterFactory((context, next) =>
        {
            var isService = context.ApplicationServices
                .GetRequiredService<IServiceProviderIsService>();

            // Which arguments could be validated is decided once, while the
            // endpoint is built, rather than by reflecting on every request.
            var validatable = context.MethodInfo
                .GetParameters()
                .Select((parameter, index) => (parameter, index))
                .Where(entry => isService.IsService(ValidatorTypeFor(entry.parameter.ParameterType)))
                .ToArray();

            if (validatable.Length == 0)
            {
                return next;
            }

            return async invocation =>
            {
                if (OptedOut(invocation.HttpContext))
                {
                    return await next(invocation);
                }

                var failures = new Dictionary<string, string[]>();

                foreach (var (parameter, index) in validatable)
                {
                    var argument = invocation.Arguments[index];

                    if (argument is null)
                    {
                        continue;
                    }

                    var validator = (IValidator)invocation.HttpContext.RequestServices
                        .GetRequiredService(ValidatorTypeFor(parameter.ParameterType));

                    var result = await validator.ValidateAsync(
                        new ValidationContext<object>(argument),
                        invocation.HttpContext.RequestAborted);

                    foreach (var group in result.Errors.GroupBy(error => error.PropertyName))
                    {
                        failures[group.Key] = [.. group.Select(error => error.ErrorMessage)];
                    }
                }

                // Every problem at once, rather than the first one found: a
                // client fixing a form should not have to submit it five times
                // to be told five things.
                return failures.Count == 0
                    ? await next(invocation)
                    : Results.ValidationProblem(failures);
            };
        });

        return group;
    }

    /// <summary>
    /// Declares that this endpoint's request is not validated by contract.
    /// Reads at the call site as the exception it is.
    /// </summary>
    public static TBuilder WithoutContractValidation<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new WithoutContractValidationAttribute());
        return builder;
    }

    private static bool OptedOut(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<WithoutContractValidationAttribute>() is not null;

    private static Type ValidatorTypeFor(Type contract) =>
        typeof(IValidator<>).MakeGenericType(contract);
}
