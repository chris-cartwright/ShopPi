using System.Diagnostics;
using System.IO.Ports;

namespace ShopPi
{
    public class Manager : IDisposable
    {
        public enum Commands : byte
        {
            Ack = 0x0B,
            PowerOff = 0x0C,
            ScreenOn = 0x0D,
            ScreenOff = 0x0E
        }
        private readonly SerialPort _port;
        private readonly PeriodicTimer _openCheckTimer;

        public bool IsConnected => _port.IsOpen;

        public Manager(IConfiguration config)
        {
            _port = new SerialPort(config["ControllerPort"]);
            _port.DataReceived += DataReceived;

            _openCheckTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            Task.Run(async () =>
            {
                do
                {
                    if (_port.IsOpen)
                    {
                        return;
                    }

                    try
                    {
                        _port.Open();
                    }
                    catch
                    {
                        // TODO: Something sane. Same error mechanism as below.
                    }
                } while (await _openCheckTimer.WaitForNextTickAsync());
            });
        }

        private void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var b = (Commands)_port.ReadByte();
            if (!Enum.IsDefined(b))
            {
                // TODO: Something intelligent? No idea how to handle this right now.
                return;
            }

            switch (b)
            {
                case Commands.Ack:
                    _port.WriteBytes((byte)Commands.Ack);
                    break;

                case Commands.PowerOff:
                    Process.Start("poweroff");
                    break;

                case Commands.ScreenOn:
                    Process.Start("sudo bash -c \"echo 1 > / sys /class/backlight/rpi_backlight/bl_power\"");
                    break;

                case Commands.ScreenOff:
                    Process.Start("sudo bash -c \"echo 0 > / sys /class/backlight/rpi_backlight/bl_power\"");
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Dispose()
        {
            _port.Dispose();
            _openCheckTimer.Dispose();
        }
    }
}
