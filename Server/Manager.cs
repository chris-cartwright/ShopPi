using System.Diagnostics;
using System.IO.Ports;
using Serilog;

namespace ShopPi
{
    public class Manager : BackgroundService, IDisposable
    {
        public enum Commands : byte
        {
            Ack = 0x0B,
            PowerOff = 0x0C,
            ScreenOn = 0x0D,
            ScreenOff = 0x0E
        }

        private readonly Serilog.ILogger logger = Log.ForContext<Manager>();
        private readonly SerialPort _port;
        private readonly PeriodicTimer _openCheckTimer;

        public bool IsConnected => _port.IsOpen;

        public Manager(IConfiguration config)
        {
            logger.Debug("Created manager class.");

            _port = new SerialPort(config["Controller:Port"], int.Parse(config["Controller:Speed"]));
            _port.DataReceived += DataReceived;
            _openCheckTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            MaybeOpenPort();
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (await _openCheckTimer.WaitForNextTickAsync())
            {
                logger.Debug("Checking port.");
                MaybeOpenPort();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _port.Dispose();
            _openCheckTimer.Dispose();
        }

        private void MaybeOpenPort()
        {
            if (_port.IsOpen)
            {
                logger.Debug("Port is open.");
                return;
            }

            try
            {
                _port.Open();
                logger.Debug("Port has been opened.");
            }
            catch (Exception ex)
            {
                logger
                    .ForContext("Exception", ex)
                    .Error("Could not connect to serial port {SerialPort}.", _port.PortName);
            }
        }

        private void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            while (_port.BytesToRead > 0)
            {
                var raw = _port.ReadByte();
                logger.Debug("Received command {Command}.", raw.ToString("X8"));
                try
                {
                    HandleCommand((Commands)raw);
                }
                catch (Exception ex)
                {
                    logger
                        .ForContext("Exception", ex)
                        .Error("Failed to process command.");
                }
            }
        }

        private async void HandleCommand(Commands cmd)
        {
            async Task StartAndWaitAsync(string script)
            {
                var proc = new Process();
                proc.StartInfo.FileName = script;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.Start();
                await proc.WaitForExitAsync();

                if (proc.ExitCode != 0)
                {
                    var error = await proc.StandardError.ReadToEndAsync();
                    var output = await proc.StandardOutput.ReadToEndAsync();

                    logger
                        .ForContext("Script", script)
                        .ForContext("stderr", error)
                        .ForContext("stdout", output)
                        .Error("Script exited with {ExitCode}", proc.ExitCode);
                }
            }

            switch (cmd)
            {
                case Commands.Ack:
                    logger.Debug("Pong.");
                    _port.WriteBytes((byte)Commands.Ack);
                    break;

                case Commands.PowerOff:
                    logger.Information("Power off.");
                    await StartAndWaitAsync("./Scripts/poweroff.sh");
                    break;

                case Commands.ScreenOn:
                    logger.Information("Screen on.");
                    await StartAndWaitAsync("./Scripts/screen_on.sh");
                    break;

                case Commands.ScreenOff:
                    logger.Information("Screen off.");
                    await StartAndWaitAsync("./Scripts/screen_off.sh");
                    break;

                default:
                    // TODO: Something intelligent? No idea how to handle this right now.
                    logger.Error("Unknown command received: {Command}.", ((byte)cmd).ToString("X8"));
                    break;
            }
        }
    }
}
