using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

namespace DotNext.Maintenance.CommandLine;

using IApplicationStatusProvider = Diagnostics.IApplicationStatusProvider;

partial class ApplicationMaintenanceCommand
{
    /// <summary>
    /// Creates a command that can be used to execute members of <see cref="IApplicationStatusProvider"/>.
    /// </summary>
    /// <param name="provider">The status provider.</param>
    /// <returns>A new command.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    public static ApplicationMaintenanceCommand Create(IApplicationStatusProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var probeTypeArg = new Argument<string>("type")
            {
                Description = CommandResources.ProbeCommandProbeTypeArgDescription,
            }
            .AcceptOnlyFromAmong(ApplicationProbe.StartupProbeName, ApplicationProbe.ReadinessProbeName, ApplicationProbe.LivenessProbeName);

        var timeoutArg = new Argument<TimeSpan>("timeout")
        {
            Description = CommandResources.ProbeCommandTimeoutArgDescription,
            CustomParser = ParseTimeout,
            DefaultValueFactory = static _ => Timeout.InfiniteTimeSpan,
        };

        var successfulResponseOption = new Option<string>("--successful-response", "-s", "-ok")
        {
            Arity = ArgumentArity.ExactlyOne,
            Required = false,
            Description = CommandResources.ProbeCommandSuccessfulResponseOptionDescription,
            DefaultValueFactory = static _ => "ok"
        };

        var failedResponseOption = new Option<string>("--unsuccessful-response", "-u", "-f")
        {
            Arity = ArgumentArity.ExactlyOne,
            Required = false,
            Description = CommandResources.ProbeCommandUnsuccessfulResponseOptionDescription,
            DefaultValueFactory = static _ => "fail",
        };

        var command = new ApplicationMaintenanceCommand("probe")
        {
            probeTypeArg,
            timeoutArg,
            successfulResponseOption,
            failedResponseOption,
        };
        
        command.Description = CommandResources.ProbeCommandDescription;

        command.SetAction(InvokeAsync);
        return command;

        static TimeSpan ParseTimeout(ArgumentResult result)
        {
            var token = result.Tokens.FirstOrDefault()?.Value;

            if (!(TimeSpan.TryParse(token, CultureInfo.InvariantCulture, out var timeout) && timeout > TimeSpan.Zero))
            {
                result.AddError(CommandResources.ProbeCommandInvalidTimeoutArg(token));
            }

            return timeout;
        }

        Task InvokeAsync(ParseResult result, CancellationToken token) => provider.InvokeProbeAsync(
            result.GetRequiredValue(probeTypeArg),
            result.GetRequiredValue(successfulResponseOption),
            result.GetRequiredValue(failedResponseOption),
            result.GetRequiredValue(timeoutArg),
            result.InvocationConfiguration.Output,
            token
        );
    }
}