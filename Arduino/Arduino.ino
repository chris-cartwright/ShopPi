#include <Wire.h>
#include <SPI.h>
#include <RTClib.h>

#include "Clock.h"
#include "Heartbeat.h"
#include "Nightly.h"

//#define DEBUG

/* Constants */

// The type of this constant just needs to be big enough to hold it's value.
// No effect on the math.
const uint8_t debounce = 50;

const uint16_t eeprom_nightly = 0;

/* Commands */
const byte cmd_now = 0x01;
const byte cmd_set_datetime = 0x02;
const byte cmd_cycle_led = 0x03;
const byte cmd_cycle_outputs = 0x04;
const byte cmd_test_inputs = 0x05;
const byte cmd_read_light_sensor = 0x06;
const byte cmd_verbose_enable = 0x07;
const byte cmd_verbose_disable = 0x08;
const byte cmd_boot_rpi = 0x09;
const byte cmd_sync = 0x0A;
const byte cmd_ack = 0x0B;
const byte cmd_poweroff = 0x0C;
const byte cmd_screen_on = 0x0D;
const byte cmd_screen_off = 0x0E;
const byte cmd_set_sched = 0x0F;
const byte cmd_get_sched = 0x10;

/* Pins */
const byte pin_light_sensor = A0;

const byte pin_wake_rpi = 11;

const byte pin_buttons = 12;
const byte pin_button_brightness = 10;

const byte pin_data = 8;
const byte pin_clock = 7;
const byte pin_latch_outputs = 4;
const byte pin_latch_inputs = 3;

const byte pin_led_red = 9;
const byte pin_led_green = 5;
const byte pin_led_blue = 6;

/* Port mappings */
const byte port_rpi_power = 0b00000001;
const byte port_rpi_screen = 0b00000010;

/* Function signatures */
byte crc8(byte *data, size_t length, byte poly = 0xEB);
//void bootPi();

/* Variables */
RTC_DS1307 rtc;
Clock clock = Clock(rtc);
Heartbeat heartbeat;
Nightly nightly(eeprom_nightly, rtc, &bootRpi);

byte inputs = 0;
byte outputs = 0;
byte brightness = 128;
byte notif_red = 0;
byte notif_green = 0;
byte notif_blue = 0;

volatile bool verbose = false;
bool piHealthy = false;
bool piScreenOn = false;

void setup() {
  Serial.begin(115200);
  Wire.begin();
  rtc.begin();
  clock.begin();

  pinMode(pin_data, OUTPUT);
  pinMode(pin_clock, OUTPUT);
  pinMode(pin_latch_inputs, OUTPUT);
  pinMode(pin_latch_outputs, OUTPUT);
  pinMode(pin_button_brightness, OUTPUT);

  // RPi is booted on the falling edge of this pin.
  // Set the pullup resistor before enabling output.
  pinMode(pin_wake_rpi, INPUT_PULLUP);
  pinMode(pin_wake_rpi, OUTPUT);

  // Set brightness before initializing LEDs.
  readAmbient();

  // Init all outputs to off.
  setShiftRegisters();

  notif_green = 255;
  setNotifLed();
}

void loop() {
  clock.tick();
  readAmbient();
  if (Serial.available()) {
    handleCommand();
  }

  heartbeat.tick();
  if (piHealthy && heartbeat.running() && !heartbeat.healthy()) {
    piHealthy = false;
    notif_red = 255;
    notif_green = 0;
    notif_blue = 0;
    setNotifLed();
  }

  if (!piHealthy && heartbeat.running() && heartbeat.healthy()) {
    piHealthy = true;
    notif_red = 0;
    notif_green = 255;
    notif_blue = 0;
    setNotifLed();
  }

  nightly.tick();

  byte buttons = readButtons();

  if (buttons & port_rpi_power) {
    if (!heartbeat.running()) {
      bootRpi();
      heartbeat.begin();

      // Set to true so the unhealthy check above will be triggered.
      piHealthy = true;
    }

    outputs &= ~port_rpi_power;
  } else {
    if (heartbeat.running()) {
      Serial.write(cmd_poweroff);
      heartbeat.suspend();
      piHealthy = false;

      notif_red = 0;
      notif_green = 255;
      notif_blue = 0;
      setNotifLed();
    }

    outputs |= port_rpi_power;
  }

  if (buttons & port_rpi_screen) {
    if (piScreenOn) {
      Serial.write(cmd_screen_off);
      piScreenOn = false;
    }

    outputs |= port_rpi_screen;
  } else {
    if (!piScreenOn) {
      Serial.write(cmd_screen_on);
      piScreenOn = true;
    }

    outputs &= ~port_rpi_screen;
  }

  // Update state of outputs
  setShiftRegisters();
}

void readAmbient() {
  static unsigned long lastCheck = 0;

  unsigned long now = millis();
  if (now - lastCheck < 1000) {
    return;
  }

  lastCheck = now;

  int ambient = analogRead(pin_light_sensor);
  int mapped = map(ambient, 500, 1024, 0, 255);
  brightness = constrain(mapped, 31, 255);

  analogWrite(pin_button_brightness, map(brightness, 0, 255, 255, 0));
  setNotifLed();
}

void handleCommand() {
  byte cmd = Serial.read();
  if (verbose) {
    Serial.print("CMD: ");
    Serial.println(cmd, HEX);
  }

  if (cmd == cmd_now) {
    sendTime();
  } else if (cmd == cmd_set_datetime) {
    byte buffer[7];
    Serial.readBytes(buffer, sizeof(buffer));

    Serial.println();
    Serial.print("DEBUG: Received: ");
    for (int i = 0; i < sizeof(buffer); i++) {
      Serial.print(buffer[i], DEC);
      Serial.print(" ");
    }

    Serial.println();

    byte crc = crc8(buffer, sizeof(buffer) - 1);
    byte check_crc = buffer[sizeof(buffer) - 1];
    if (crc != check_crc) {
      Serial.print("CRC8 failed. Expected: ");
      Serial.print(crc, HEX);
      Serial.print(", message had: ");
      Serial.print(check_crc, HEX);
      return;
    }

    DateTime newTime = DateTime(
      buffer[0] + 2000,
      buffer[1],
      buffer[2],
      buffer[3],
      buffer[4],
      buffer[5]);

    Serial.print("RECV: ");
    char *str = "YYYY-MM-DD hh:mm:ss";
    Serial.println(newTime.toString(str));

    if(!newTime.isValid()) {
      Serial.println("ERROR: Provided date/time is invalid. Update ignored.");
      return;
    }

    rtc.adjust(newTime);
    clock.sync();

    Serial.print("SET: ");
    sendTime();
  } else if (cmd == cmd_cycle_led) {
    Serial.print("TESTING: ");
    Serial.flush();

    Serial.print("white ");
    Serial.flush();
    analogWrite(pin_led_red, 0);
    analogWrite(pin_led_green, 0);
    analogWrite(pin_led_blue, 0);
    delay(3000);
    ledOff();

    Serial.print("red ");
    Serial.flush();
    analogWrite(pin_led_red, 0);
    delay(3000);
    ledOff();

    Serial.print("green ");
    Serial.flush();
    analogWrite(pin_led_green, 0);
    delay(3000);
    ledOff();

    Serial.print("blue ");
    Serial.flush();
    analogWrite(pin_led_blue, 0);
    delay(3000);
    ledOff();

    Serial.println("off.");
    Serial.flush();
    ledOff();
  } else if (cmd == cmd_cycle_outputs) {
    outputs = 0;
    setShiftRegisters();

    Serial.print("OUTPUT: ");
    for (int i = 0; i < 8; i++) {
      Serial.print(i, DEC);
      Serial.print(" ");
      outputs = 1 << i;
      setShiftRegisters();
      delay(3000);
    }

    Serial.println("off.");
    outputs = 0;
    setShiftRegisters();

    Serial.print("BRIGHTNESS: ");
    outputs = 0xFF;
    setShiftRegisters();

    for (int i = 0; i <= 255; i += 25) {
      Serial.print(i, DEC);
      Serial.print(" ");
      analogWrite(pin_button_brightness, i);
      delay(3000);
    }

    Serial.println("off.");
    outputs = 0xFF;
    setShiftRegisters();
  } else if (cmd == cmd_test_inputs) {
    Serial.print("INPUTS: ");

    byte prev = 0;
    while (true) {
      if (Serial.available()) {
        byte b = Serial.read();
        if (b == 0xFF) {
          break;
        }
      }

      byte states = readButtons();
      if (states != prev) {
        prev = states;
        Serial.print(states, BIN);
        Serial.print(" ");
      }
    }

    Serial.println("done.");
  } else if (cmd == cmd_read_light_sensor) {
    Serial.print("LIGHT: ");
    Serial.println(analogRead(pin_light_sensor), DEC);
  } else if (cmd == cmd_verbose_enable) {
    verbose = true;
    Serial.println("Verbose enabled.");
  } else if (cmd == cmd_verbose_disable) {
    verbose = false;
    Serial.println("Verbose disabled.");
  } else if (cmd == cmd_boot_rpi) {
    bootRpi();
    Serial.println("Boot triggered.");
  } else if (cmd == cmd_sync) {
    clock.sync();
  } else if (cmd == cmd_ack) {
    Serial.write(cmd_ack);
  } else if(cmd == cmd_set_sched) {
    uint8_t sched = Serial.read();
    Serial.print("SET: ");
    Serial.println(nightly.schedule(sched) ? "true" : "false");
  } else if(cmd == cmd_get_sched) {
    Serial.println(nightly.schedule());
  } else {
    Serial.print("ERROR: Unknown command ");
    Serial.println(cmd, HEX);
  }
}

byte readButtons() {
  static unsigned long lastRead = 0;
  static byte prevState = 0;

  if (millis() - lastRead < debounce) {
    return prevState;
  }

  byte ret = 0;
  for (int i = 0; i < 8; i++) {
    inputs = 1 << i;
    setShiftRegisters();
    delay(10);

    if (digitalRead(pin_buttons) == HIGH) {
      ret += inputs;
    }
  }

#if DEBUG
  if (verbose) {
    Serial.print("BUTTONS: ");
    Serial.println(ret, BIN);
  }
#endif

  return ret;
}

void sendTime() {
  if (!rtc.isrunning()) {
    Serial.println("RTC not running.");
    return;
  }

  DateTime now = rtc.now();
  Serial.print(now.year(), DEC);
  Serial.print('/');
  Serial.print(now.month(), DEC);
  Serial.print('/');
  Serial.print(now.day(), DEC);
  Serial.print(' ');
  Serial.print(now.hour(), DEC);
  Serial.print(':');
  Serial.print(now.minute(), DEC);
  Serial.print(':');
  Serial.print(now.second(), DEC);
  Serial.println();
}

void setShiftRegisters() {
  static byte prevInputs;
  static byte prevOutputs;

  // Shift registers set up this way to avoid flickering in the outputs.
  // That shift register now only latches if the output state changes.

  // Write all bits first, then latch. Still taking advantage of daisy chaining.
  // First byte shifted out ends up on the chip farthest down the chain.
  if (prevOutputs != outputs) {
    shiftOut(pin_data, pin_clock, MSBFIRST, outputs);
  }

  // Need to write `intputs` if `outputs` has changed so the bits are in the
  // correct shift register.
  if (prevInputs != inputs || prevOutputs != outputs) {
    shiftOut(pin_data, pin_clock, MSBFIRST, inputs);
  }

  // Latch required registers.
  if (prevOutputs != outputs) {
    digitalWrite(pin_latch_outputs, HIGH);
    digitalWrite(pin_latch_outputs, LOW);
  }

  if (prevInputs != inputs) {
    digitalWrite(pin_latch_inputs, HIGH);
    digitalWrite(pin_latch_inputs, LOW);
  }

  prevInputs = inputs;
  prevOutputs = outputs;
}

void setNotifLed() {
  // Notification LED is bright. Constrained!
  byte bright = map(brightness, 0, 255, 0, 16);

  // Map the colours.
  bright = map(bright, 0, 255, 255, 0);
  byte r = map(notif_red, 0, 255, 255, bright);
  byte g = map(notif_green, 0, 255, 255, bright);
  byte b = map(notif_blue, 0, 255, 255, bright);

  analogWrite(pin_led_red, r);
  analogWrite(pin_led_green, g);
  analogWrite(pin_led_blue, b);
}

void ledOff() {
  // Common cathode LED; 0 is max brightness.
  analogWrite(pin_led_red, 255);
  analogWrite(pin_led_green, 255);
  analogWrite(pin_led_blue, 255);
}

void bootRpi() {
  if (verbose) {
    Serial.println("Booting RPi.");
  }

  digitalWrite(pin_wake_rpi, LOW);
  delay(100);
  digitalWrite(pin_wake_rpi, HIGH);
}

byte crc8(byte *data, size_t length, byte poly = 0xEB) {
  byte crc = 0;
  for (byte i = 0; i < length; i++) {
    byte b = data[i];
    crc ^= b;
    for (byte i = 8; i > 0; i--) {
      if ((crc & (1 << 7)) > 0) {
        crc <<= 1;
        crc ^= poly;
      } else {
        crc <<= 1;
      }
    }
  }

  return crc;
}
