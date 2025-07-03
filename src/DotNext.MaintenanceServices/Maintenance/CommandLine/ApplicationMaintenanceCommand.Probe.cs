using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

namespace DotNext.Maintenance.CommandLine;

using IApplicationStatusProvider = Diagnostics.IApplicationStatusProvider;
using DefaultBindings = Binding.DefaultBindings;

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

        var probeTypeArg = new Argument<string>("type", CommandResources.ProbeCommandProbeTypeArgDescription)
            .FromAmong(ApplicationProbe.StartupProbeName, ApplicationProbe.ReadinessProbeName, ApplicationProbe.LivenessProbeName);

        var timeoutArg = new Argument<TimeSpan>("timeout", parse: ParseTimeout, description: CommandResources.ProbeCommandTimeoutArgDescription);
        timeoutArg.SetDefaultValue(Timeout.InfiniteTimeSpan);

        var successfulResponseOption = new Option<string>("--successful-response", Func.Constant("ok"), CommandResources.ProbeCommandSuccessfulResponseOptionDescription)
        {
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = false,
        };
        successfulResponseOption.AddAlias("-s");
        successfulResponseOption.AddAlias("-ok");

        var failedResponseOption = new Option<string>("--unsuccessful-response", Func.Constant("fail"), CommandResources.ProbeCommandUnsuccessfulResponseOptionDescription)
        {
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = false,
        };
        failedResponseOption.AddAlias("-u");
        failedResponseOption.AddAlias("-f");

        var command = new ApplicationMaintenanceCommand("probe", CommandResources.ProbeCommandDescription)
        {
            probeTypeArg,
            timeoutArg,
            successfulResponseOption,
            failedResponseOption,
        };

        command.SetHandler(provider.InvokeProbeAsync, probeTypeArg, DefaultBindings.Console, successfulResponseOption, failedResponseOption, timeoutArg, DefaultBindings.Token);
        return command;

        static TimeSpan ParseTimeout(ArgumentResult result)
        {
            var token = result.Tokens.FirstOrDefault()?.Value;

            if (!(TimeSpan.TryParse(token, CultureInfo.InvariantCulture, out var timeout) && timeout > TimeSpan.Zero))
            {
                result.ErrorMessage = CommandResources.ProbeCommandInvalidTimeoutArg(token);
            }

            return timeout;
        }
    }
}