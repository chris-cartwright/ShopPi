namespace Tester;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class CommandAttribute : Attribute
{
    public Commands Command { get; }
    public CommandAttribute(Commands command)
    {
        Command = command;
    }
}