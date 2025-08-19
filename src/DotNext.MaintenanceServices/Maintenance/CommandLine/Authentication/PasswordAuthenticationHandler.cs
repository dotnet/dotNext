using System.CommandLine;
using System.Security.Principal;

namespace DotNext.Maintenance.CommandLine.Authentication;

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
    ValueTask<IPrincipal?> IAuthenticationHandler.ChallengeAsync(ParseResult result, IIdentity identity, CancellationToken token)
    {
        var login = result.GetRequiredValue(loginOption);
        var secret = result.GetRequiredValue(secretOption);
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

    private static Option<string> LoginOption() => new("--login")
    {
        Required = true,
        Arity = ArgumentArity.ExactlyOne,
        Description = CommandResources.LoginOptionName,
        Recursive = true,
    };

    private static Option<string> SecretOption() => new("--secret")
    {
        Required = true,
        Arity = ArgumentArity.ExactlyOne,
        Description = CommandResources.SecretOptionName,
        Recursive = true,
    };
}