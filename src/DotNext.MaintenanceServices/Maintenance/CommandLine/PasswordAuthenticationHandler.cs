using System.CommandLine;
using System.CommandLine.Invocation;
using System.Security.Principal;

namespace DotNext.Maintenance.CommandLine;

/// <summary>
/// Provides simple authentication model based on login/password.
/// </summary>
public abstract class PasswordAuthenticationHandler : IAuthenticationHandler
{
    private readonly Option<string> loginOption, secretOption;

    /// <summary>
    /// Initializes a new handler.
    /// </summary>
    protected PasswordAuthenticationHandler()
    {
        loginOption = LoginOption();
        secretOption = SecretOption();
    }

    /// <inheritdoc />
    ValueTask<IPrincipal?> IAuthenticationHandler.ChallengeAsync(InvocationContext context, IIdentity identity, CancellationToken token)
    {
        var login = context.ParseResult.GetValueForOption(loginOption);
        var secret = context.ParseResult.GetValueForOption(secretOption);
        return login is { Length: > 0 } && secret is { Length: > 0 } ? ChallengeAsync(login, secret, token) : new(default(IPrincipal));
    }

    /// <summary>
    /// Performs authentication using provided login and password.
    /// </summary>
    /// <param name="login">The name of the user.</param>
    /// <param name="secret">The password.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Authentication result; or <see langword="null"/> in case of failed authentication.</returns>
    protected abstract ValueTask<IPrincipal?> ChallengeAsync(string login, string secret, CancellationToken token);

    /// <inheritdoc />
    IEnumerable<Option> IAuthenticationHandler.GetGlobalOptions()
    {
        yield return loginOption;
        yield return secretOption;
    }

    private static Option<string> LoginOption() => new("--login", CommandResources.LoginOptionName)
    {
        IsRequired = true,
        Arity = ArgumentArity.ExactlyOne,
    };

    private static Option<string> SecretOption() => new("--secret", CommandResources.SecretOptionName)
    {
        IsRequired = true,
        Arity = ArgumentArity.ExactlyOne,
    };
}