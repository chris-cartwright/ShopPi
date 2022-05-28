#include <RTClib.h>
#include <Adafruit_GFX.h>
#include <Adafruit_LEDBackpack.h>

class Clock {
    Adafruit_7segment _matrix;
    unsigned long _last = 0;
    bool _colon;
    RTC_DS1307* _rtc;

    void _writeTime() {
      if (!_rtc->isrunning()) {
        return;
      }

      DateTime now = _rtc->now();
      int hour = now.hour();
      if (hour > 12) {
        hour -= 12;
      }

      _matrix.print(hour * 100 + now.minute(), DEC);
      if (now.minute() < 10) {
        _matrix.writeDigitNum(3, 0);
      }
    }

  public:
    Clock(RTC_DS1307 &rtc) {
      _rtc = &rtc;
    }

    void begin() {
      _matrix.begin();
      _matrix.setBrightness(8);
    }

    void tick() {
      unsigned long now = millis();
      if (now - _last < 1000) {
        return;
      }

      _last = now;

      // This must come before other write operations!
      // It will override all other changes.
      _writeTime();

      _colon = !_colon;
      _matrix.drawColon(_colon);

      _matrix.writeDisplay();
    }
};
