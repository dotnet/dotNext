using System.Buffers;
using System.CommandLine;
using System.CommandLine.Parsing;
using DotNext.Maintenance.CommandLine;
using DotNext.Maintenance.CommandLine.Binding;
using Microsoft.Extensions.Hosting;

var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
Console.WriteLine($"Print nc -U {path} in separated Terminal session to get access to Application Management Interface");
Console.WriteLine("Then print interactive-mode and press ENTER");

await new HostBuilder()
    .ConfigureServices(services =>
    {
        services
            .UseApplicationMaintenanceInterface(path)
            .RegisterDefaultMaintenanceCommands()
            .RegisterMaintenanceCommand("add", ConfigureAddCommand)
            .RegisterMaintenanceCommand("sub", ConfigureSubtractCommand);
    })
    .Build()
    .RunAsync();

static void ConfigureAddCommand(ApplicationMaintenanceCommand command)
{
    command.Description = "Adds two integers";
    var argX = new Argument<int>("x", parse: ParseInteger, description: "The first operand")
    {
        Arity = ArgumentArity.ExactlyOne
    };
    var argY = new Argument<int>("y", parse: ParseInteger, description: "The second operand")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    command.AddArgument(argX);
    command.AddArgument(argY);
    command.SetHandler(static (x, y, console) =>
    {
        console.Out.Write((x + y).ToString());
        console.Out.Write(Environment.NewLine);
    },
    argX,
    argY,
    DefaultBindings.Console);
}

static void ConfigureSubtractCommand(ApplicationMaintenanceCommand command)
{
    command.Description = "Adds two integers";
    var argX = new Argument<int>("x", parse: ParseInteger, description: "The first operand")
    {
        Arity = ArgumentArity.ExactlyOne
    };
    var argY = new Argument<int>("y", parse: ParseInteger, description: "The second operand")
    {
        Arity = ArgumentArity.ExactlyOne
    };

    command.AddArgument(argX);
    command.AddArgument(argY);
    command.SetHandler(static (x, y, console) =>
    {
        console.Out.Write((x - y).ToString());
        console.Out.Write(Environment.NewLine);
    },
    argX,
    argY,
    DefaultBindings.Console);
}

static int ParseInteger(ArgumentResult result)
{
    var token = result.Tokens.FirstOrDefault()?.Value;

    if (!int.TryParse(token, out var value))
    {
        result.ErrorMessage = $"{token} is not an integer number";
    }

    return value;
}