using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Runtime;

namespace DotNext.Maintenance.CommandLine;

partial class ApplicationMaintenanceCommand
{
    private static ApplicationMaintenanceCommand GCCollectCommand()
    {
        var generationArg = new Argument<int>("generation")
        {
            Description = CommandResources.GCCollectCommandGenerationArgDescription,
            CustomParser = ParseGeneration,
        };

        var blockingOption = new Option<bool>("--blocking", "-b")
        {
            Description = CommandResources.GCCollectCommandBlockingOptionDescription,
            Arity = ArgumentArity.Zero,
            Required = false,
            DefaultValueFactory = False,
        };

        var compactingOption = new Option<bool>("--compacting", "-c")
        {
            Arity = ArgumentArity.Zero,
            Required = false,
            Description = CommandResources.GCCollectCommandCompactingOptionDescription,
            DefaultValueFactory = False,
        };

        var command = new ApplicationMaintenanceCommand("collect", CommandResources.GCCollectCommandDescription)
        {
            generationArg,
            blockingOption,
            compactingOption,
        };

        command.SetAction(result =>
        {
            GC.Collect(
                result.GetRequiredValue(generationArg),
                GCCollectionMode.Forced,
                result.GetRequiredValue(blockingOption),
                result.GetRequiredValue(compactingOption));
        });

        return command;

        static int ParseGeneration(ArgumentResult result)
        {
            var token = result.Tokens.FirstOrDefault()?.Value;

            if (!(int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var generation) && generation >= 0 && generation <= GC.MaxGeneration))
            {
                result.AddError(CommandResources.GCCollectCommandInvalidGenerationArg(token));
            }

            return generation;
        }
    }

    private static Command LohCompactionModeCommand()
    {
        var modeArg = new Argument<GCLargeObjectHeapCompactionMode>("mode")
            {
                Description = CommandResources.GCLohModeCommandModeArgDescription,
                CustomParser = ParseMode,
            }
            .AcceptOnlyFromAmong(Enum.GetNames<GCLargeObjectHeapCompactionMode>());

        var command = new ApplicationMaintenanceCommand("loh-compaction-mode", CommandResources.GCLohModeCommandDescription)
        {
            modeArg,
        };

        command.SetAction(result => GCSettings.LargeObjectHeapCompactionMode = result.GetRequiredValue(modeArg));
        return command;

        static GCLargeObjectHeapCompactionMode ParseMode(ArgumentResult result)
        {
            var token = result.Tokens.FirstOrDefault()?.Value;

            if (!Enum.TryParse<GCLargeObjectHeapCompactionMode>(token, ignoreCase: true, out var mode))
            {
                result.AddError(CommandResources.GCLohModeCommandInvalidModeArg(token));
            }

            return mode;
        }
    }

    private static Command RefreshMemoryLimitsCommand()
    {
        var command = new ApplicationMaintenanceCommand("refresh-mem-limit")
        {
            Description = CommandResources.GCRefreshMemoryLimit,
        };

        command.SetAction(static _ => GC.RefreshMemoryLimit());
        return command;
    }

    /// <summary>
    /// Creates a command that allows to force Garbage Collection.
    /// </summary>
    /// <returns>A new command.</returns>
    public static ApplicationMaintenanceCommand GCCommand()
    {
        var command = new ApplicationMaintenanceCommand("gc")
        {
            GCCollectCommand(),
            LohCompactionModeCommand(),
            RefreshMemoryLimitsCommand(),
        };

        command.Description = CommandResources.GCCommandDescription;
        return command;
    }
}