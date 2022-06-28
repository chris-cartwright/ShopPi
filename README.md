# Enable UART on pins

```
sudo raspi-config
```

- 3 - Interface options
- P6 - Serial port

Disable the shell and enable the hardware.

# Use `/dev/serial0`

https://www.raspberrypi.com/documentation/computers/configuration.html#configuring-uarts

Summary: `/dev/ttyS0` and `/dev/AMA0` can point to different hardware devices, dependent on the Pi.
`/dev/serial0` always points to the hardware pins.

# Publishing to Pi

## Server
`dotnet publish`
`rsync --progress -e "ssh -i ~/.ssh/chris-cartwright" Server/bin/Debug/net6.0/linux-arm/publish/* pi@shoppi.d.chris-cartwright.com:ShopPi`

## Client
`npm run build`
`rsync --progress -e "ssh -i ~/.ssh/chris-cartwright" -r Client/public/* pi@shoppi.d.chris-cartwright.com:ShopPi/public`

# Debugging Chromium

Add `--remote-debugging-port=9222` to `/etc/chromium-browser/customizations/00-rpi-vars`.

Open tunnel from RPi: `ssh -i ~/.ssh/chris-cartwright -L 9222:localhost:9222 pi@shoppi.d.chris-cartwright.com`

Open `localhost:9222` in any Chromium based browser. Edge comes installed with Windows.

# Screen on/off

Done this way because the redirection ( `>` ) is handled by the shell, losing sudo.
There is a way to do it with `tee` as well, can't remember exactly.

```bash
sudo bash -c "echo 1 > /sys/class/backlight/rpi_backlight/bl_power"
```