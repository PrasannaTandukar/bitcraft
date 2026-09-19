export const ARDUINO_PRESET_CASES = [
    {
        inputText: "My LED connected to Pin 13 is not blinking even though the sketch uploaded successfully. Here is the schematic and sketch: void setup() { pinMode(13, OUTPUT); } void loop() { digitalWrite(13, HIGH); }",
        expectedOutput: "The LED needs to be toggled with a delay (LOW then HIGH) to blink. Also check if the cathode is connected to GND with an appropriate current-limiting resistor.",
        contextText: "Arduino Uno digital output pins provide 5V. LEDs require a current-limiting resistor (e.g. 220 ohm) in series to ground. Blinking requires state toggling with delay().",
        metadataJson: JSON.stringify({
            category: "arduino_visual_diagnostic",
            difficulty: "beginner",
            circuitFlaw: "Missing delay and state toggle in loop; verify GND path",
            targetComponent: "LED / Pin 13",
        }),
        actualOutput: "",
    },
    {
        inputText: "I have a push button connected to Pin 2, but digitalRead(2) keeps returning random HIGH and LOW values when not pressed.",
        expectedOutput: "The pin is floating because there is no pull-up or pull-down resistor. Use pinMode(2, INPUT_PULLUP) to enable the internal pull-up resistor.",
        contextText: "Digital input pins on ATmega328P are high-impedance. A floating pin picks up electromagnetic noise unless tied to VCC or GND via a pull-up/down resistor or internal pull-up.",
        metadataJson: JSON.stringify({
            category: "arduino_visual_diagnostic",
            difficulty: "intermediate",
            circuitFlaw: "Floating input pin without pull-up/down reference",
            targetComponent: "Push Button / Pin 2",
        }),
        actualOutput: "",
    },
    {
        inputText: "My I2C OLED display (SSD1306) is not showing anything on Arduino Uno. Connected SDA to Pin 4 and SCL to Pin 5.",
        expectedOutput: "On an Arduino Uno, hardware I2C pins are A4 (SDA) and A5 (SCL), not digital pins 4 and 5. Reconnect SDA to A4 and SCL to A5.",
        contextText: "Arduino Uno (ATmega328P) dedicated TWI/I2C pins are mapped to Analog pins A4 (SDA) and A5 (SCL). Digital pins 4 and 5 cannot be used for standard Wire library hardware I2C.",
        metadataJson: JSON.stringify({
            category: "arduino_visual_diagnostic",
            difficulty: "intermediate",
            circuitFlaw: "Incorrect I2C bus wiring on Uno (Digital 4/5 instead of A4/A5)",
            targetComponent: "SSD1306 OLED / I2C Bus",
        }),
        actualOutput: "",
    },
];

// Standardized benchmark suites no longer use preset cases. They run against
// the real DeepEval datasets via the sidecar (see StandardBenchmarkFeatures).

