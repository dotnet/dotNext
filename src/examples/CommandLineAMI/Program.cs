using System.CommandLine;
using System.CommandLine.Parsing;
using DotNext.Maintenance.CommandLine;

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
    var argX = new Argument<int>("x")
    {
        Arity = ArgumentArity.ExactlyOne,
        Description = "The first operand",
        CustomParser = ParseInteger
    };
    var argY = new Argument<int>("y")
    {
        Arity = ArgumentArity.ExactlyOne,
        Description = "The second operand",
        CustomParser = ParseInteger,
    };

    command.Add(argX);
    command.Add(argY);
    command.SetAction(result =>
    {
        var x = result.GetRequiredValue(argX);
        var y = result.GetRequiredValue(argY);
        result.InvocationConfiguration.Output.WriteLine(x + y);
    });
}

static void ConfigureSubtractCommand(ApplicationMaintenanceCommand command)
{
    command.Description = "Subtracts two integers";
    var argX = new Argument<int>("x")
    {
        Arity = ArgumentArity.ExactlyOne,
        Description = "The first operand",
        CustomParser = ParseInteger
    };
    var argY = new Argument<int>("y")
    {
        Arity = ArgumentArity.ExactlyOne,
        Description = "The second operand",
        CustomParser = ParseInteger,
    };

    command.Add(argX);
    command.Add(argY);
    command.SetAction(result =>
    {
        var x = result.GetRequiredValue(argX);
        var y = result.GetRequiredValue(argY);
        result.InvocationConfiguration.Output.WriteLine(x - y);
    });
}

static int ParseInteger(ArgumentResult result)
{
    var token = result.Tokens.FirstOrDefault()?.Value;

    if (!int.TryParse(token, out var value))
    {
        result.AddError($"{token} is not an integer number");
    }

    return value;
}