extern const byte cmd_ack;
extern volatile bool verbose;

class Heartbeat {
    bool _running = false;
    bool _healthy = false;
    unsigned long _last = 0;
    const unsigned long _frequency = 1000; // milliseconds

  public:
    void begin() {
      _running = true;
      // Start with a check.
      _last = 0;

      if(verbose) {
        Serial.println("HEARTBEAT: Start");
      }
    }

    void suspend() {
      _running = false;
      _healthy = false;

      if(verbose) {
        Serial.println("HEARTBEAT: Suspend");
      }
    }

    bool running() {
      return _running;
    }

    bool healthy() {
      return _healthy;
    }

    void tick() {
      if(!_running) {
        return;
      }
      
      if (millis() - _last < _frequency) {
        return;
      }

      if(Serial.available()) {
        // Let the main logic handle the incoming command first.
        return;
      }

      Serial.write(cmd_ack);
      unsigned counter = 0;
      while(!Serial.available()) {
        delay(1);

        counter++;
        if(counter >= 1000) {
          _healthy = false;
          return;
        }
      }

      byte b = Serial.read();
      _healthy = b == cmd_ack;
      _last = millis();
    }
};
