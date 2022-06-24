using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Globalization;

namespace DotNext.Maintenance.CommandLine;

using IApplicationStatusProvider = Diagnostics.IApplicationStatusProvider;

public static partial class DefaultCommands
{
    /// <summary>
    /// Creates a command that can be used to execute members of <see cref="IApplicationStatusProvider"/>.
    /// </summary>
    /// <param name="provider">The status provider.</param>
    /// <returns>A new command.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    public static ApplicationManagementCommand CreateCommand(this IApplicationStatusProvider provider)
    {
        const string startupProbeName = "startup";
        const string readinessProbeName = "readiness";
        const string livenessProbeName = "liveness";

        ArgumentNullException.ThrowIfNull(provider);
        var command = new ApplicationManagementCommand("probe", CommandResources.ProbeCommandDescription);

        var probeTypeArg = new Argument<string>("type", CommandResources.ProbeCommandProbeTypeArgDescription).FromAmong(startupProbeName, readinessProbeName, livenessProbeName);
        command.AddArgument(probeTypeArg);

        var timeoutArg = new Argument<TimeSpan>("timeout", parse: ParseTimeout, description: CommandResources.ProbeCommandTimeoutArgDescription);
        command.AddArgument(timeoutArg);

        var successfulResponseOption = new Option<string>("--successful-response", Func.Constant("ok"), CommandResources.ProbeCommandSuccessfulResponseOptionDescription)
        {
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = false,
        };
        successfulResponseOption.AddAlias("-s");
        successfulResponseOption.AddAlias("-ok");
        command.AddOption(successfulResponseOption);

        var failedResponseOption = new Option<string>("--unsuccessful-response", Func.Constant("fail"), CommandResources.ProbeCommandUnsuccessfulResponseOptionDescription)
        {
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = false,
        };
        failedResponseOption.AddAlias("-u");
        failedResponseOption.AddAlias("-f");
        command.AddOption(failedResponseOption);

        command.SetHandler(ExecuteProbeAsync);
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

        async Task ExecuteProbeAsync(InvocationContext context)
        {
            const int timeoutExitCode = 75; // EX_TEMPFAIL from sysexits.h
            var probeName = context.ParseResult.GetValueForArgument(probeTypeArg);
            var token = context.GetCancellationToken();

            var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            bool success;
            try
            {
                timeoutSource.CancelAfter(context.ParseResult.GetValueForArgument(timeoutArg));
                success = await ExecuteProbeByNameAsync(provider, probeName, timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                // timeout occurred
                context.Console.Error.Write(CommandResources.ProbeCommandTimeoutOccurred(probeName));
                context.ExitCode = timeoutExitCode;
                return;
            }
            finally
            {
                timeoutSource.Dispose();
            }

            context.Console.Out.Write(context.ParseResult.GetValueForOption(success ? successfulResponseOption : failedResponseOption));
        }

        static Task<bool> ExecuteProbeByNameAsync(IApplicationStatusProvider provider, string probeName, CancellationToken token) => probeName switch
        {
            livenessProbeName => provider.LivenessProbeAsync(token),
            readinessProbeName => provider.ReadinessProbeAsync(token),
            startupProbeName => provider.StartupProbeAsync(token),
            _ => Task.FromResult(true),
        };
    }
}