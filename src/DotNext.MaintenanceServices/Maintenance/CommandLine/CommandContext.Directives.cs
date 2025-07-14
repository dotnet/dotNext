using System.CommandLine;
using System.CommandLine.Invocation;

namespace DotNext.Maintenance.CommandLine;

partial class CommandContext
{
    private sealed class DirectiveAction : SynchronousCommandLineAction
    {
        internal readonly Action<CommandContext> Action;

        public DirectiveAction(Action<CommandContext> action)
        {
            Action = action;
            Terminating = false;
        }

        public override int Invoke(ParseResult parseResult)
        {
            int result;
            if (parseResult.Configuration is CommandContext context)
            {
                Action(context);
                result = 0;
            }
            else
            {
                result = InvalidArgumentExitCode;
            }

            return result;
        }
    }

    private sealed class PrintErrorCodeDirective : Directive
    {
        private new const string Name = "prnec";

        public PrintErrorCodeDirective()
            : base(Name)
        {
            Action = new DirectiveAction(static context => context.printExitCode = true);
        }
    }
    
    private sealed class SuppressStandardOutputDirective : Directive
    {
        private new const string Name = "supout";

        public SuppressStandardOutputDirective()
            : base(Name)
        {
            Action = new DirectiveAction(static context => context.suppressOutputBuffer = true);
        }
    }
    
    private sealed class SuppressStandardErrorDirective : Directive
    {
        private new const string Name = "superr";

        public SuppressStandardErrorDirective()
            : base(Name)
        {
            Action = new DirectiveAction(static context => context.suppressErrorBuffer = true);
        }
    }

    internal static void RegisterDirectives(RootCommand root)
    {
        root.Add(new PrintErrorCodeDirective());
        root.Add(new SuppressStandardOutputDirective());
        root.Add(new SuppressStandardErrorDirective());
    }
}