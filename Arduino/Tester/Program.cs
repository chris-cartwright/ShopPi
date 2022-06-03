using System.IO.Ports;
using System.Reflection;

namespace Tester;

public enum Commands : byte
{
    Now = 1,
    SetDatetime,
    CycleLed,
    CycleOutputs,
    TestInputs,
    ReadLightSensor,
    VerboseEnable,
    VerboseDisable
}

public record CommandInfo(
    Commands Command,
    Action<Commands> Action,
    string Name
);

public class Program : IDisposable
{
    public static async Task Main()
    {
        using var prg = new Program();
        await prg.RunAsync();
    }

    private SerialPort? _port;
    private CancellationTokenSource _cancel = new();

    public async Task RunAsync()
    {
        var ports = SerialPort.GetPortNames();
        string? selectedPort = null;

        do
        {
            Console.WriteLine($"Available ports: {string.Join(", ", ports)}");
            Console.Write("Please select a port: ");
            selectedPort = Console.ReadLine();
            Console.WriteLine();
        }
        while (selectedPort == null || ports.All(p => p != selectedPort));

        _port = new SerialPort(selectedPort, 115200, Parity.None, 8, StopBits.One);
        _port.Open();

        var readThread = new Thread(Read);
        readThread.IsBackground = true;
        readThread.Start();

        var commands = GetCommands();

        while (true)
        {
            Console.WriteLine("Available commands:");
            foreach (var info in commands.Values)
            {
                Console.WriteLine($"{(int)info.Command} - {info.Name}");
            }

            Console.Write("Enter a command: ");
            var raw = Console.ReadLine();
            if (raw is null)
            {
                continue;
            }

            if (!byte.TryParse(raw, out var cmd))
            {
                Console.WriteLine("Could not parse command.");
                continue;
            }

            if (!commands.TryGetValue((Commands)cmd, out var found))
            {
                Console.WriteLine("Unknown command.");
                continue;
            }

            found.Action((Commands)cmd);
            Console.WriteLine("Command sent.");
        }
    }

    private IReadOnlyDictionary<Commands, CommandInfo> GetCommands()
    {
        var cmds =
            from method in GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            where !method.IsStatic
            let attrs = method.GetCustomAttributes(false).OfType<CommandAttribute>()
            where attrs.Any()
            from attr in attrs
            orderby attr.Command
            select new CommandInfo(
                attr.Command,
                (Action<Commands>)Delegate.CreateDelegate(typeof(Action<Commands>), this, method),
                Enum.GetName<Commands>(attr.Command)
            );
        return cmds.ToDictionary(k => k.Command, v => v);
    }

    private void Read()
    {
        while (!_cancel.IsCancellationRequested)
        {
            if (_port is null || !_port.IsOpen)
            {
                continue;
            }

            try
            {
                if (_port.BytesToRead > 0)
                {
                    Console.Write((char)_port.ReadByte());
                }
            }
            catch (TimeoutException)
            {
            }
        }
    }

    [Command(Commands.Now)]
    [Command(Commands.CycleLed)]
    [Command(Commands.CycleOutputs)]
    [Command(Commands.VerboseEnable)]
    [Command(Commands.VerboseDisable)]
    private void GenericCommand(Commands command)
    {
        _port.Write(new byte[] { (byte)command }, 0, 1);
    }

    [Command(Commands.TestInputs)]
    private void TestInputs(Commands command)
    {
        Console.Write("Press enter to exit.");
        _port.Write(new byte[] { (byte)Commands.TestInputs }, 0, 1);
        Console.ReadLine();
        _port.Write(new byte[] { 0xFF }, 0, 1);
    }

    [Command(Commands.SetDatetime)]
    private void SetDateTime(Commands command)
    {
        var now = DateTimeOffset.Now;
        var send = new byte[] {
            (byte)Commands.SetDatetime,
            (byte)(now.Year - 2000),
            (byte)now.Month,
            (byte)now.Day,
            (byte)now.Hour,
            (byte)now.Minute,
            (byte)now.Second
        };
        _port.Write(send, 0, send.Length);
    }

    [Command(Commands.ReadLightSensor)]
    private void ReadLightSensor(Commands command)
    {
        Console.Write("Press enter to exit.");
        var last = DateTimeOffset.MinValue;
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.Enter)
                {
                    break;
                }
            }

            var now = DateTimeOffset.Now;
            if (now - last > TimeSpan.FromSeconds(3))
            {
                last = now;
                _port.Write(new byte[] { (byte)Commands.ReadLightSensor }, 0, 1);
            }
        }
    }

    public void Dispose()
    {
        _port?.Dispose();
    }
}