using System.Diagnostics;
using System.IO.Ports;
using Serilog;
using ShopPi.Optional;

namespace ShopPi
{
    public class Manager : BackgroundService, IDisposable
    {
        public enum Commands : byte
        {
            Now = 0x01,
            SetDatetime = 0x02,
            Ack = 0x0B,
            PowerOff = 0x0C,
            ScreenOn = 0x0D,
            ScreenOff = 0x0E
        }

        private readonly Serilog.ILogger _logger = Log.ForContext<Manager>();
        private readonly SerialPort _port;
        private readonly PeriodicTimer _openCheckTimer;

        public bool IsConnected => _port.IsOpen;

        public Manager(IConfiguration config)
        {
            _logger.Debug("Created manager class.");

            _port = new SerialPort(config["Controller:Port"], int.Parse(config["Controller:Speed"]));
            _port.DataReceived += DataReceived;
            _openCheckTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            MaybeOpenPort();
            if(_port.IsOpen)
            {
                UpdateTime();
            }

            await base.StartAsync(cancellationToken);
        }

        public string? UpdateTime()
        {
            DateTimeOffset micro = default;
            switch(GetTime())
            {
                case Result<DateTimeOffset, string>.Ok ok:
                    micro = ok.Value;
                    break;
                
                case Result<DateTimeOffset, string>.Error error:
                    return error.Value;
            }

            var diff = micro - DateTimeOffset.Now;
            if(diff.TotalMinutes < 1) {
                return null;
            }

            _port.DataReceived -= DataReceived;
            try
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

                // Chew up newline added for clarity in interactive mode
                _ = _port.ReadUntil('\n');

                var recv = _port.ReadUntil('\n');
                var set = _port.ReadUntil('\n');

                _logger
                    .ForContext("recv", recv)
                    .ForContext("set", set)
                    .Debug("Time updated.");

                return null;
            }
            finally
            {
                _port.DataReceived += DataReceived;
            }
        }

        public Result<DateTimeOffset, string> GetTime()
        {
            _port.DataReceived -= DataReceived;
            try
            {
                _port.Write(new[] { (byte)Commands.Now }, 0, 1);
                var line = _port.ReadLine();
                if(!DateTimeOffset.TryParse(line, out var micro))
                {
                    return new Result<DateTimeOffset, string>.Error(line);
                }

                return new Result<DateTimeOffset, string>.Ok(micro);
            }
            finally
            {
                _port.DataReceived += DataReceived;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (await _openCheckTimer.WaitForNextTickAsync())
            {
                _logger.Debug("Checking port.");
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
                _logger.Debug("Port is open.");
                return;
            }

            try
            {
                _port.Open();
                _logger.Debug("Port has been opened.");
            }
            catch (Exception ex)
            {
                _logger
                    .ForContext("Exception", ex)
                    .Error("Could not connect to serial port {SerialPort}.", _port.PortName);
            }
        }

        private void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            while (_port.BytesToRead > 0)
            {
                var raw = _port.ReadByte();
                _logger.Debug("Received command {Command}.", raw.ToString("X8"));
                try
                {
                    HandleCommand((Commands)raw);
                }
                catch (Exception ex)
                {
                    _logger
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

                    _logger
                        .ForContext("Script", script)
                        .ForContext("stderr", error)
                        .ForContext("stdout", output)
                        .Error("Script exited with {ExitCode}", proc.ExitCode);
                }
            }

            switch (cmd)
            {
                case Commands.Ack:
                    _logger.Debug("Pong.");
                    _port.WriteBytes((byte)Commands.Ack);
                    break;

                case Commands.PowerOff:
                    _logger.Information("Power off.");
                    await StartAndWaitAsync("./Scripts/poweroff.sh");
                    break;

                case Commands.ScreenOn:
                    _logger.Information("Screen on.");
                    await StartAndWaitAsync("./Scripts/screen_on.sh");
                    break;

                case Commands.ScreenOff:
                    _logger.Information("Screen off.");
                    await StartAndWaitAsync("./Scripts/screen_off.sh");
                    break;

                default:
                    // TODO: Something intelligent? No idea how to handle this right now.
                    _logger.Error("Unknown command received: {Command}.", ((byte)cmd).ToString("X8"));
                    break;
            }
        }
    }
}
