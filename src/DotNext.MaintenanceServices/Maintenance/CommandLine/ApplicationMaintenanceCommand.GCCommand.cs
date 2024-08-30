using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Runtime;

namespace DotNext.Maintenance.CommandLine;

public partial class ApplicationMaintenanceCommand
{
    private static ApplicationMaintenanceCommand GCCollectCommand()
    {
        var generationArg = new Argument<int>("generation", parse: ParseGeneration, description: CommandResources.GCCollectCommandGenerationArgDescription);

        var blockingOption = new Option<bool>("--blocking", Func.Constant(false), description: CommandResources.GCCollectCommandBlockingOptionDescription)
        {
            Arity = ArgumentArity.Zero,
            IsRequired = false,
        };

        blockingOption.AddAlias("-b");

        var compactingOption = new Option<bool>("--compacting", Func.Constant(false), description: CommandResources.GCCollectCommandCompactingOptionDescription)
        {
            Arity = ArgumentArity.Zero,
            IsRequired = false,
        };

        compactingOption.AddAlias("-c");

        var command = new ApplicationMaintenanceCommand("collect", CommandResources.GCCollectCommandDescription)
        {
            generationArg,
            blockingOption,
            compactingOption,
        };

        command.SetHandler(static (generation, blocking, compacting) => GC.Collect(generation, GCCollectionMode.Forced, blocking, compacting), generationArg, blockingOption, compactingOption);
        return command;

        static int ParseGeneration(ArgumentResult result)
        {
            var token = result.Tokens.FirstOrDefault()?.Value;

            if (!(int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var generation) && generation >= 0 && generation <= GC.MaxGeneration))
            {
                result.ErrorMessage = CommandResources.GCCollectCommandInvalidGenerationArg(token);
            }

            return generation;
        }
    }

    private static Command LohCompactionModeCommand()
    {
        var modeArg = new Argument<GCLargeObjectHeapCompactionMode>("mode", parse: ParseMode, description: CommandResources.GCLohModeCommandModeArgDescription);
        modeArg.FromAmong(Enum.GetNames<GCLargeObjectHeapCompactionMode>());

        var command = new ApplicationMaintenanceCommand("loh-compaction-mode", CommandResources.GCLohModeCommandDescription)
        {
            modeArg,
        };

        command.SetHandler(static mode => GCSettings.LargeObjectHeapCompactionMode = mode, modeArg);
        return command;

        static GCLargeObjectHeapCompactionMode ParseMode(ArgumentResult result)
        {
            var token = result.Tokens.FirstOrDefault()?.Value;

            if (!Enum.TryParse<GCLargeObjectHeapCompactionMode>(token, ignoreCase: true, out var mode))
            {
                result.ErrorMessage = CommandResources.GCLohModeCommandInvalidModeArg(token);
            }

            return mode;
        }
    }

    private static Command RefreshMemoryLimitsCommand()
    {
        var command = new ApplicationMaintenanceCommand("refresh-mem-limits", CommandResources.GCRefreshMemoryLimits);
        command.SetHandler(GC.RefreshMemoryLimit);
        return command;
    }

    /// <summary>
    /// Creates a command that allows to force Garbage Collection.
    /// </summary>
    /// <returns>A new command.</returns>
    public static ApplicationMaintenanceCommand GCCommand()
    {
        var command = new ApplicationMaintenanceCommand("gc", CommandResources.GCCommandDescription);
        command.AddCommand(GCCollectCommand());
        command.AddCommand(LohCompactionModeCommand());
        command.AddCommand(RefreshMemoryLimitsCommand());
        return command;
    }
}