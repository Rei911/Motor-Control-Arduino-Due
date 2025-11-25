#include <LiquidCrystal.h>
#include <Encoder.h>
#define ENCODER_OPTIMIZE_INTERRUPTS

// ======================== Encoder & LCD ========================
Encoder encMotor(50, 52);       // Encoder Motor
Encoder encModule(24, 25);      // Potensio Encoder
LiquidCrystal lcd(8, 9, 4, 5, 6, 7);

// ======================== Pin Motor ============================
const int dirMotor = 2;
const int pwmMotor = 3;
const int PULSE_PER_REV = 400;

// ======================== Variabel =============================
unsigned long lastTime = 0;
unsigned long lastSend = 0;

long oldPosition = 0;
float rpm = 0;

int pwmValue = 0;
long potValue = 0;
int setpointRPM = 0;

bool motorOn = false;
String statusText = "";

// Interval kirim ke PC
const unsigned long SEND_INTERVAL = 100;

void setup() {
  lcd.begin(16, 2);
  lcd.print("SISTEM MOTOR DC");
  delay(1500);
  lcd.clear();

  pinMode(dirMotor, OUTPUT);
  pinMode(pwmMotor, OUTPUT);
  digitalWrite(dirMotor, LOW);

  Serial.begin(9600);
  Serial.println("SETPOINT,RPM,STATUS,VOLT");
}

void loop() {
  unsigned long currentTime = millis();

  // ===================== INPUT SERIAL =======================
  if (Serial.available()) {
    String cmd = Serial.readStringUntil('\n');
    cmd.trim();

    if (cmd == "ON") {
      motorOn = true;
      Serial.println("STATUS: MOTOR ON");
    }
    else if (cmd == "OFF") {
      motorOn = false;
      pwmValue = 0;
      analogWrite(pwmMotor, 0);
      Serial.println("STATUS: MOTOR OFF");
    }
  }

  // ===================== BACA POT & TENTUKAN SETPOINT =======================
  long newPotValue = encModule.read();
  if (abs(newPotValue - potValue) >= 5) {
    potValue = newPotValue;
    setpointRPM = map(abs(potValue), 0, 800, 0, 4500);
    setpointRPM = constrain(setpointRPM, 0, 4500);
  }

  // ===================== SET PWM BERDASARKAN SETPOINT =======================
  if (motorOn) {
    pwmValue = map(setpointRPM, 0, 2000, 0, 255);
    analogWrite(pwmMotor, pwmValue);
  } else {
    analogWrite(pwmMotor, 0);
    pwmValue = 0;
  }

  // ===================== HITUNG RPM =======================
  if (currentTime - lastTime >= 1000) {
    long newPosition = encMotor.read();
    long pulseCount = newPosition - oldPosition;
    rpm = (abs(pulseCount) * 60.0) / PULSE_PER_REV;

    oldPosition = newPosition;
    lastTime = currentTime;
  }

  // ===================== TENTUKAN STATUS =======================
  if (!motorOn) statusText = "STOP";
  else if (rpm < setpointRPM - 100) statusText = "RAMPING";
  else if (rpm > setpointRPM + 100) statusText = "OVERSHOOT";
  else statusText = "STABLE";

  // ===================== DISPLAY LCD =======================
  lcd.setCursor(0, 0);
  lcd.print("SP:");
  lcd.print(setpointRPM);
  lcd.print("  ");

  lcd.setCursor(0, 1);
  lcd.print("RPM:");
  lcd.print((int)rpm);
  lcd.print(" ");

  // ===================== KIRIM SERIAL CSV =======================
  if (currentTime - lastSend >= SEND_INTERVAL) {
    Serial.print(setpointRPM);
  Serial.print(",");
  Serial.print((int)rpm);
  Serial.print(",");
  Serial.print(pwmValue);
  Serial.print(",");
  Serial.print("9.0");
  Serial.print(",");
  Serial.println(statusText);


    lastSend = currentTime;
  }
}
