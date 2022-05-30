#include <Wire.h>
#include <SPI.h>
#include <RTClib.h>

#include "Clock.h"

/* Commands */
const byte cmd_now = 0x01;
const byte cmd_set_datetime = 0x02;
const byte cmd_cycle_led = 0x03;
const byte cmd_cycle_outputs = 0x04;

/* Pins */
const byte pin_light_sensor = A0;

const byte pin_wake_rpi = 11;

const byte pin_buttons = 12;
const byte pin_button_brightness = 10;

const byte pin_data = 8;
const byte pin_clock = 7;
const byte pin_latch = 4;

const byte pin_led_red = 9;
const byte pin_led_green = 6;
const byte pin_led_blue = 5;

/* Variables */
RTC_DS1307 rtc;
Clock clock = Clock(rtc);

void setup() {
  Serial.begin(115200);
  Wire.begin();
  rtc.begin();
  clock.begin();

  pinMode(pin_data, OUTPUT);
  pinMode(pin_clock, OUTPUT);
  pinMode(pin_latch, OUTPUT);
  pinMode(pin_button_brightness, OUTPUT);
  analogWrite(pin_button_brightness, 0);
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
    outputsOff();

    Serial.print("OUTPUT: ");
    for (int i = 0; i < 8; i++) {
      Serial.print(i, DEC);
      Serial.print(" ");
      digitalWrite(pin_latch, LOW);
      shiftOut(pin_data, pin_clock, MSBFIRST, 1 << i);
      digitalWrite(pin_latch, HIGH);
      delay(3000);
    }

    Serial.println("off.");
    outputsOff();

    Serial.print("BIGHTNESS: ");
    digitalWrite(pin_latch, LOW);
    shiftOut(pin_data, pin_clock, MSBFIRST, 0xFF);
    digitalWrite(pin_latch, HIGH);

    for (int i = 0; i <= 255; i += 25) {
      Serial.print(i, DEC);
      Serial.print(" ");
      analogWrite(pin_button_brightness, i);
      delay(3000);
    }

    Serial.println("off.");
    outputsOff();
  }
  else {
    Serial.println("ERROR: Unknown command");
  }
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

void ledOff() {
  // Common cathode LED; 0 is max brightness.
  analogWrite(pin_led_red, 255);
  analogWrite(pin_led_green, 255);
  analogWrite(pin_led_blue, 255);
}

void outputsOff() {
  digitalWrite(pin_latch, LOW);
  shiftOut(pin_data, pin_clock, MSBFIRST, 0);
  digitalWrite(pin_latch, HIGH);
}
