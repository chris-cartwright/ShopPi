# Publishing to Pi

## Server
`dotnet publish -r linux-arm --self-contained true`
`rsync --progress -e "ssh -i ~/.ssh/chris-cartwright" Server/bin/Debug/net6.0/linux-arm/publish/* pi@shoppi.d.chris-cartwright.com:ShopPi`

## Client
`npm run build`
`rsync --progress -e "ssh -i ~/.ssh/chris-cartwright" -r Client/public/* pi@shoppi.d.chris-cartwright.com:ShopPi/public`

## Debugging Chromium

Add `--remote-debugging-port=9222` to `/etc/chromium-browser/customizations/00-rpi-vars`.

Open tunnel from RPi: `ssh -i ~/.ssh/chris-cartwright -L 9222:localhost:9222 pi@shoppi.d.chris-cartwright.com`

Open `localhost:9222` in any Chromium based browser. Edge comes installed with Windows.