#include <RTClib.h>
#include <Adafruit_GFX.h>
#include <Adafruit_LEDBackpack.h>
#include <limits.h>

extern volatile bool verbose;

class Clock {
    Adafruit_7segment _matrix;
    unsigned long _last = 0;
    bool _colon;
    RTC_DS1307* _rtc;

    unsigned _hour = 12;
    unsigned _minute = 0;
    unsigned _second = 0;

    void _writeTime() {
      _matrix.print(_hour * 100 + _minute, DEC);
    }

    DateTime _readRtc() {
      if (!_rtc->isrunning()) {
        return;
      }

      DateTime now = _rtc->now();
      _hour = now.twelveHour();
      _minute = now.minute();
      _second = now.second();

      if (verbose) {
        Serial.print("RTC: ");
        Serial.print(_hour, DEC);
        Serial.print(" ");
        Serial.print(_minute, DEC);
        Serial.print(" ");
        Serial.println(_second);
      }
    }

  public:
    Clock(RTC_DS1307 &rtc) {
      _rtc = &rtc;
    }

    void begin() {
      _matrix.begin();
      _matrix.setBrightness(8);
      _readRtc();
    }

    void sync() {
      _readRtc();
    }

    void tick() {
      unsigned long now = millis();
      unsigned long diff = now - _last;
      if (diff < 1000) {
        return;
      }

      // This condition can be met if one of the debugging commands were sent.
      // Some of those commands have long pauses or loops.
      if (diff > 2000) {
        _readRtc();
      }
      else {
        _second++;
        if (_second >= 60) {
          _second = 0;
          _minute++;
          if (_minute >= 60) {
            _minute = 0;
            _hour++;
            if (_hour >= 13) {
              // Sync with the RTC every 12 hours
              _readRtc();
            }
          }
        }
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
