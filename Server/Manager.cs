using System.Diagnostics;
using System.IO.Ports;
using System.Text.RegularExpressions;
using Serilog;
using ShopPi.Optional;

namespace ShopPi
{
	public class Manager : BackgroundService
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
		private readonly Regex _ntpSync = new(
			"^\\s*NTPSynchronized=((yes)|(no))\\s*$",
			RegexOptions.IgnoreCase | RegexOptions.Multiline
		);

		// Automatically update time once per run of application
		private bool _timeUpdated;

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
			await base.StartAsync(cancellationToken);
		}

		public string? UpdateTime()
		{
			DateTimeOffset micro = default;
			switch (GetTime())
			{
				case Result<DateTimeOffset, string>.Ok ok:
					micro = ok.Value;
					break;

				case Result<DateTimeOffset, string>.Error error:
					return error.Value;
			}

			var diff = micro - DateTimeOffset.Now;
			// `diff` can be negative.
			if (Math.Abs(diff.TotalMinutes) < 1)
			{
				return null;
			}

			_port.DataReceived -= DataReceived;
			try
			{
				var now = DateTimeOffset.Now;
				var send = new[] {
					(byte)(now.Year - 2000),
					(byte)now.Month,
					(byte)now.Day,
					(byte)now.Hour,
					(byte)now.Minute,
					(byte)now.Second
				};
				var crc8 = Crc8.ComputeChecksum(send);

				_port.Write(new[] { (byte)Commands.SetDatetime }, 0, 1);
				_port.Write(send, 0, send.Length);
				_port.Write(new[] { crc8 }, 0, 1);

				// Chew up newline added for clarity in interactive mode
				_ = _port.ReadUntil('\n');

				var recv = _port.ReadUntil('\n');
				var set = _port.ReadUntil('\n');

				_logger
					.ForContext(nameof(recv), recv)
					.ForContext(nameof(set), set)
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
				if (!DateTimeOffset.TryParse(line, out var micro))
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

		public async Task<bool> IsSystemTimeSynchronizedAsync(CancellationToken cancellationToken = default)
		{
			using var proc = new Process();
			proc.StartInfo.UseShellExecute = false;
			proc.StartInfo.CreateNoWindow = true;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.StartInfo.FileName = "timedatectl";
			proc.StartInfo.Arguments = "show";

			try
			{
				proc.Start();
				await proc.WaitForExitAsync(cancellationToken);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Could not determine if time has been synchronized.");
				return false;
			}

			var timeInfo = await proc.StandardOutput.ReadToEndAsync();
			var syncMatch = _ntpSync.Match(timeInfo);
			if (!syncMatch.Success)
			{
				_logger
					.ForContext("Response", timeInfo)
					.Error("Unknown response returned from `timedatectl`.");
				return false;
			}

			return string.Equals(syncMatch.Groups[1].Value, "yes", StringComparison.OrdinalIgnoreCase);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (await _openCheckTimer.WaitForNextTickAsync(stoppingToken))
			{
				_logger
					.ForContext(nameof(_timeUpdated), _timeUpdated)
					.Debug("Checking port.");
				if (!MaybeOpenPort() || _timeUpdated)
				{
					continue;
				}

				_logger.Debug("RTC time has not been verified.");
				if (await IsSystemTimeSynchronizedAsync(stoppingToken))
				{
					var msg = UpdateTime();
					if (msg is not null)
					{
						_logger
							.ForContext("Message", msg)
							.Error("Could not update time on Arduino.");
					}
					else
					{
						_timeUpdated = true;
					}
				}
				else
				{
					_logger.Debug("System time has not been synchronized.");
				}
			}

			if (stoppingToken.IsCancellationRequested)
			{
				_logger.Information("Manager has been stopped.");
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			_port.Dispose();
			_openCheckTimer.Dispose();
		}

		private bool MaybeOpenPort()
		{
			if (_port.IsOpen)
			{
				_logger.Debug("Port is open.");
				return true;
			}

			try
			{
				_port.Open();
				_logger.Debug("Port has been opened.");
				return true;
			}
			catch (Exception ex)
			{
				_logger
					.ForContext("Exception", ex)
					.Error("Could not connect to serial port {SerialPort}.", _port.PortName);
			}

			return false;
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
