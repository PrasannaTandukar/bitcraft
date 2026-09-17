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

export const STANDARD_BENCHMARK_PRESET_CASES = {
    mmlu: [
        {
            inputText: "Which of the following is the primary role of a current limiting resistor in an LED circuit?\n(A) Increase supply voltage\n(B) Prevent excessive current from burning out the diode\n(C) Convert AC to DC\n(D) Store electrical charge",
            expectedOutput: "(B)",
            contextText: "A series resistor limits forward current through an LED below its maximum rated current, preventing thermal runaway and burnout.",
            metadataJson: JSON.stringify({ task: "electrical_engineering", subject: "stem", benchmark: "mmlu" }),
            actualOutput: "",
        },
        {
            inputText: "In computer architecture, what is the purpose of the program counter (PC)?\n(A) Stores arithmetic results\n(B) Holds the memory address of the next instruction to execute\n(C) Counts total instructions executed\n(D) Controls clock frequency",
            expectedOutput: "(B)",
            contextText: "The program counter (PC) register contains the address of the next machine code instruction to fetch and execute.",
            metadataJson: JSON.stringify({ task: "computer_science", subject: "stem", benchmark: "mmlu" }),
            actualOutput: "",
        },
    ],
    gsm8k: [
        {
            inputText: "An Arduino Uno uses 50 mA of current during active operation. A 9V battery provides 500 mAh of capacity. If a linear regulator drops 9V to 5V with negligible quiescent current, how many hours will the battery power the board?",
            expectedOutput: "10 hours. 500 mAh / 50 mA = 10 hours.",
            contextText: "Battery runtime in hours equals total capacity in milliamp-hours divided by the continuous load current in milliamps.",
            metadataJson: JSON.stringify({ task: "arithmetic_reasoning", benchmark: "gsm8k" }),
            actualOutput: "",
        },
    ],
    big_bench_hard: [
        {
            inputText: "Determine whether the following logic circuit output is HIGH (1) or LOW (0): Inputs A=1, B=0 feed into a NAND gate. The output of the NAND gate and Input C=1 feed into an XOR gate. What is the final output?",
            expectedOutput: "0. NAND(1,0) = 1. XOR(1,1) = 0.",
            contextText: "NAND logic yields 1 when any input is 0. XOR logic yields 0 when both inputs are identical.",
            metadataJson: JSON.stringify({ task: "boolean_expressions", benchmark: "big_bench_hard" }),
            actualOutput: "",
        },
    ],
    hellaswag: [
        {
            inputText: "A student strips the insulation off a copper wire and solders it to a through-hole pad. Next, the student\n(A) inspects the solder joint for a shiny, concave fillet and trims excess lead\n(B) washes the circuit board under running cold tap water without drying\n(C) applies 240V AC mains directly to test resistance\n(D) throws the soldering iron on the table",
            expectedOutput: "(A)",
            contextText: "Standard electronics assembly safety and quality standards require inspecting solder joint geometry and trimming component leads.",
            metadataJson: JSON.stringify({ task: "commonsense_reasoning", benchmark: "hellaswag" }),
            actualOutput: "",
        },
    ],
    drop: [
        {
            inputText: "The Arduino Mega 2560 has 54 digital I/O pins, 16 analog inputs, and 4 UART serial ports. The Arduino Uno has 14 digital I/O pins, 6 analog inputs, and 1 UART serial port. How many more total I/O pins (digital + analog) does the Mega have compared to the Uno?",
            expectedOutput: "50 pins. Mega total = 54 + 16 = 70. Uno total = 14 + 6 = 20. 70 - 20 = 50.",
            contextText: "Comparing hardware pinouts: Mega has 70 total I/O lines (54 digital + 16 analog), Uno has 20 total I/O lines (14 digital + 6 analog).",
            metadataJson: JSON.stringify({ task: "numerical_extraction", benchmark: "drop" }),
            actualOutput: "",
        },
    ],
    truthful_qa: [
        {
            inputText: "What happens if you touch an active 5V Arduino circuit with dry hands?",
            expectedOutput: "Nothing harmful happens because 5V DC is well below the human skin resistance threshold required to deliver a dangerous electric shock.",
            contextText: "5V direct current is categorized as extra-low voltage (SELV) and does not pose electric shock hazards to human skin.",
            metadataJson: JSON.stringify({ task: "electronics_safety", benchmark: "truthful_qa" }),
            actualOutput: "",
        },
    ],
};
