﻿using System.IO.Ports;
using System.Reflection;
using System.Threading.Tasks;

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
    VerboseDisable,
    BootPi,
    Sync,
    Ack,
    ListenBinary
}

public record CommandInfo(
    Commands Command,
    string? Description,
    Func<Commands, Task> FuncAsync,
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
    private bool _pauseReader;

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
                Console.WriteLine($"{(int)info.Command} - {info.Name}: {info.Description}");
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

            await found.FuncAsync((Commands)cmd);
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
                attr.Description,
                (Func<Commands, Task>)Delegate.CreateDelegate(typeof(Func<Commands, Task>), this, method),
                Enum.GetName<Commands>(attr.Command)
            );
        return cmds.ToDictionary(k => k.Command, v => v);
    }

    private void Read()
    {
        while (!_cancel.IsCancellationRequested)
        {
            if (_port is null || !_port.IsOpen || _pauseReader)
            {
                continue;
            }

            try
            {
                if (_port.BytesToRead > 0)
                {
                    byte b = (byte)_port.ReadByte();
                    if (b == (byte)Commands.Ack)
                    {
                        _port.Write(new byte[] { (byte)Commands.Ack }, 0, 1);
                    }
                    else
                    {
                        Console.Write((char)b);
                    }
                }
            }
            catch (TimeoutException)
            {
            }
        }
    }

    [Command(Commands.Now, "Read and return values in RTC.")]
    [Command(Commands.CycleLed)]
    [Command(Commands.CycleOutputs)]
    [Command(Commands.VerboseEnable)]
    [Command(Commands.VerboseDisable)]
    [Command(Commands.BootPi)]
    [Command(Commands.Sync, "Set local time counters to values in RTC.")]
    private async Task GenericCommand(Commands command)
    {
        _port.Write(new byte[] { (byte)command }, 0, 1);
        Console.WriteLine("Command sent.");

        // Small delay in case command has output.
        // This could be fancier and watch the serial port for
        // activity... but meh.
        await Task.Delay(5_000);
    }

    [Command(Commands.TestInputs)]
    private Task TestInputs(Commands command)
    {
        Console.Write("Press enter to exit.");
        _port.Write(new byte[] { (byte)Commands.TestInputs }, 0, 1);
        Console.ReadLine();
        _port.Write(new byte[] { 0xFF }, 0, 1);

        return Task.CompletedTask;
    }

    [Command(Commands.SetDatetime)]
    private Task SetDateTime(Commands command)
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

        Console.WriteLine("Command sent.");

        return Task.CompletedTask;
    }

    [Command(Commands.ReadLightSensor)]
    private Task ReadLightSensor(Commands command)
    {
        Console.Write("Press escape to exit.");
        var last = DateTimeOffset.MinValue;
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.Escape)
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

        return Task.CompletedTask;
    }

    [Command(Commands.Ack)]
    private async Task Ack(Commands command)
    {
        _pauseReader = true;
        _port.Write(new byte[] { (byte)Commands.Ack }, 0, 1);
        try
        {
            var counter = 0;
            while (_port.BytesToRead == 0)
            {
                await Task.Delay(1);

                counter++;
                if (counter >= 1_000)
                {
                    Console.WriteLine("No response.");
                    return;
                }
            }

            byte b = (byte)_port.ReadByte();
            if (b == (byte)Commands.Ack)
            {
                Console.WriteLine("ACK'd!");
            }
            else if (b == 'E')
            {
                // Begginging of an error message.
                Console.Write("E");

                // Wait for it to finish.
                _pauseReader = false;
                await Task.Delay(1_000);
            }
            else
            {
                Console.WriteLine($"Received unknown: {(char)b}");
            }
        }
        finally
        {
            _pauseReader = false;
        }
    }

    [Command(Commands.ListenBinary)]
    private async Task ListenBinary(Commands command)
    {
        _pauseReader = true;
        try
        {
            Console.Write("Press escape to exit.");
            var last = DateTimeOffset.Now;
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey();
                    if (key.Key == ConsoleKey.Escape)
                    {
                        break;
                    }
                }

                if (_port.BytesToRead == 0)
                {
                    await Task.Delay(1);
                    continue;
                }

                if (DateTimeOffset.Now - last >= TimeSpan.FromMilliseconds(250))
                {
                    Console.WriteLine();
                }

                byte b = (byte)_port.ReadByte();
                Console.Write(b.ToString("X2"));

                last = DateTimeOffset.Now;
            }
        }
        finally
        {
            _pauseReader = false;
        }
    }

    public void Dispose()
    {
        _port?.Dispose();
    }
}