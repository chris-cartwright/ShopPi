#include <EEPROM.h>

extern volatile bool verbose;

class Nightly {
  const unsigned long _min_check_diff = 5 * 60 * 1000;
  unsigned long _last_check;

  const TimeSpan _min_sched_diff = TimeSpan(0, 2, 0, 0);
  DateTime _last_sched;

  bool _sched_set = false;
  uint8_t _sched_hour;
  uint8_t _sched_minute;
  uint16_t _eeprom_addr;
  RTC_DS1307* _rtc;

  void (*_boot_rpi)();

public:
  Nightly(uint16_t eeprom_addr, RTC_DS1307& rtc, void (*boot_rpi)())
    : _eeprom_addr(eeprom_addr), _rtc(&rtc), _boot_rpi(boot_rpi) {
  }

  void begin() {
    auto saved = EEPROM.read(_eeprom_addr);
    auto hour = (saved & 0b11111000) >> 3;
    auto tens_minute = saved & 0b00000111;

    _sched_set = saved > 0;
    if (_sched_set) {
      _sched_hour = hour;
      _sched_minute = tens_minute * 10;
    }
  }

  bool schedule(uint8_t sched) {
    auto hour = (sched & 0b11111000) >> 3;
    auto tens_minute = sched & 0b00000111;

    if (hour < 0 || hour > 23) {
      return false;
    }

    if (tens_minute < 0 || tens_minute > 5) {
      return false;
    }

    EEPROM.write(_eeprom_addr, sched);
    _sched_set = sched > 0;
    if (_sched_set) {
      _sched_hour = hour;
      _sched_minute = tens_minute * 10;

      if(verbose) {
        Serial.print("NIGHTLY: Schedule set to: ");
        Serial.print(_sched_hour, DEC);
        Serial.print(":");
        Serial.println(_sched_minute, DEC);
      }
    }

    return true;
  }

  uint8_t schedule() {
    return ((_sched_hour << 3) & 0b11111000) | ((_sched_minute / 10) & 0b00000111);
  }

  void tick() {
    if (!_sched_set) {
      return;
    }

    auto millis_now = millis();
    if (millis_now - _last_check < _min_check_diff) {
      return;
    }

    _last_check = millis_now;

    if (!_rtc->isrunning()) {
      return;
    }

    auto rtc_now = _rtc->now();
    const auto diff = rtc_now - _last_sched;
    if (diff.totalseconds() < _min_sched_diff.totalseconds()) {
      return;
    }

    if (rtc_now.hour() != _sched_hour || rtc_now.minute() != _sched_minute) {
      return;
    }

    if (verbose) {
      Serial.println("Booting Raspberry Pi for nightly maintenance.");
    }

    _boot_rpi();
    _last_sched = rtc_now;
  }
};