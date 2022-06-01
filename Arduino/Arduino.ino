#include <Wire.h>
#include <SPI.h>
#include <RTClib.h>

#include "Clock.h"

/* Commands */
const byte cmd_now = 0x01;
const byte cmd_set_datetime = 0x02;
const byte cmd_cycle_led = 0x03;
const byte cmd_cycle_outputs = 0x04;
const byte cmd_test_inputs = 0x05;

/* Pins */
const byte pin_light_sensor = A0;

const byte pin_wake_rpi = 11;

const byte pin_buttons = 12;
const byte pin_button_brightness = 10;

const byte pin_data = 8;
const byte pin_clock = 7;
const byte pin_latch = 4;

const byte pin_led_red = 6;
const byte pin_led_green = 5;
const byte pin_led_blue = 9;

/* Variables */
RTC_DS1307 rtc;
Clock clock = Clock(rtc);

byte inputs = 0;
byte outputs = 0;
byte brightness = 128;

void setup() {
  Serial.begin(115200);
  Wire.begin();
  rtc.begin();
  clock.begin();

  pinMode(pin_data, OUTPUT);
  pinMode(pin_clock, OUTPUT);
  pinMode(pin_latch, OUTPUT);
  pinMode(pin_button_brightness, OUTPUT);
  analogWrite(pin_button_brightness, brightness);

  // Init all outputs to off.
  setShiftRegisters();

  setNotifLed(0, 255, 0);
}

void loop() {
  clock.tick();

  if (Serial.available()) {
    handleCommand();
  }
}

void handleCommand() {
  byte cmd = Serial.read();
  if (cmd == cmd_now) {
    sendTime();
  }
  else if (cmd ==  cmd_set_datetime) {
    byte buffer[6];
    Serial.readBytes(buffer, 6);

    DateTime newTime = DateTime(
                         buffer[0] + 2000,
                         buffer[1],
                         buffer[2],
                         buffer[3],
                         buffer[4],
                         buffer[5]
                       );
    rtc.adjust(newTime);

    sendTime();
  }
  else if (cmd == cmd_cycle_led) {
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
  }
  else if (cmd == cmd_cycle_outputs) {
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

    Serial.print("BIGHTNESS: ");
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
  }
  else if (cmd_test_inputs) {
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
  }
  else {
    Serial.println("ERROR: Unknown command");
  }
}

byte readButtons() {
  byte ret = 0;
  for (int i = 0; i < 8; i++) {
    inputs = 1 << i;
    setShiftRegisters();
    delay(10);
    
    if (digitalRead(pin_buttons) == HIGH) {
      ret += inputs;
    }
  }

  //Serial.print("BUTTONS: "); Serial.println(ret, BIN);
  return ret;
}

void sendTime() {
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
  // First byte shifted out ends up on the chip farthest down the chain.
  shiftOut(pin_data, pin_clock, MSBFIRST, inputs);
  shiftOut(pin_data, pin_clock, MSBFIRST, outputs);
  
  digitalWrite(pin_latch, HIGH);
  digitalWrite(pin_latch, LOW);
}

void setNotifLed(byte red, byte green, byte blue) {
  // Notification LED is bright. Constrained!
  byte bright = map(brightness, 0, 255, 0, 127);

  // Map the colours.
  bright = map(bright, 0, 255, 255, 0);
  byte r = map(red, 0, 255, 255, bright);
  byte g = map(green, 0, 255, 255, bright);
  byte b = map(blue, 0, 255, 255, bright);
  
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
