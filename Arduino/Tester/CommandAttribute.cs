namespace Tester;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class CommandAttribute : Attribute
{
    public Commands Command { get; }
    public string? Description { get; }

    public CommandAttribute(Commands command, string? description = null)
    {
        Command = command;
        Description = description;
    }
}