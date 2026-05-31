#include <WiFi.h>
#include <PubSubClient.h>

#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>

#include <OneWire.h>
#include <DallasTemperature.h>

// ================= OLED =================

#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 64

Adafruit_SSD1306 display(
  SCREEN_WIDTH,
  SCREEN_HEIGHT,
  &Wire,
  -1
  );

// ================= WIFI =================

const char* ssid = "Wokwi-GUEST";
const char* password = "";

// ================= MQTT =================

// Broker público de pruebas
const char* mqtt_server = "broker.hivemq.com";

WiFiClient espClient;
PubSubClient client(espClient);

// ================= PINES =================

#define PIN_TEMP 4

#define PIN_LED_GREEN 15
#define PIN_LED_YELLOW 2
#define PIN_LED_RED 13

#define PIN_BUTTON 27

#define PIN_TRIG 5
#define PIN_ECHO 18

#define PIN_RPM 34

// ================= TEMPERATURA =================

OneWire oneWire(PIN_TEMP);

DallasTemperature sensors(&oneWire);

// ================= VARIABLES =================

float temperatura = 0;
int rpm = 0;
float nivel = 0;

bool alarma = false;

String estado = "RUNNING";

// ======================================================

void setup()
{
  Serial.begin(115200);

  // LEDs
  pinMode(PIN_LED_GREEN, OUTPUT);
  pinMode(PIN_LED_YELLOW, OUTPUT);
  pinMode(PIN_LED_RED, OUTPUT);

  // Botón
  pinMode(PIN_BUTTON, INPUT_PULLUP);

  // Ultrasonido
  pinMode(PIN_TRIG, OUTPUT);
  pinMode(PIN_ECHO, INPUT);

  // OLED
  if (!display.begin(SSD1306_SWITCHCAPVCC, 0x3C))
  {
    Serial.println("OLED ERROR");
    while (true);
  }

  display.clearDisplay();
  display.setTextColor(WHITE);

  // Sensor temperatura
  sensors.begin();

  // WIFI
  conectarWiFi();

  // MQTT
  client.setServer(mqtt_server, 1883);

  mostrarMensaje("Sistema iniciado");
}

// ======================================================

void loop()
{
  
  verificarWiFi();

  if (!client.connected())
  {
    reconnectMQTT();
  }

  client.loop();

  leerSensores();

  verificarAlarmas();

  actualizarLEDs();

  actualizarOLED();

  publicarMQTT();

  delay(1000);
}

// ======================================================
//WIFI


void conectarWiFi()
{
  WiFi.begin(ssid, password);

  while (WiFi.status() != WL_CONNECTED)
  {
    delay(500);
  }
}

void verificarWiFi()
{
  if (WiFi.status() == WL_CONNECTED)
  {
    return;
  }

  Serial.println("WiFi desconectado. Reconectando...");

  WiFi.disconnect();
  WiFi.begin(ssid, password);

  int intentos = 0;

  while (WiFi.status() != WL_CONNECTED && intentos < 20)
  {
    delay(500);
    Serial.print(".");
    intentos++;
  }

  if (WiFi.status() == WL_CONNECTED)
  {
    Serial.println("\nWiFi reconectado");
  }
  else
  {
    Serial.println("\nNo fue posible reconectar WiFi");
  }
}

// ======================================================



void reconnectMQTT()
{
    if(WiFi.status() != WL_CONNECTED)
    {
      return;
    }
    
    if(client.connected())
        return;

    String clientId = "VisualIoT_MOTOR_02_";
    clientId += WiFi.macAddress();
    clientId.replace(":", "");

    if(client.connect(clientId.c_str()))
    {
        Serial.println("MQTT conectado");
    }
    else
    {
        Serial.print("Error MQTT: ");
        Serial.println(client.state());
    }
}
// ======================================================

void leerSensores()
{
  // ===== RPM =====

  int valorPot = analogRead(PIN_RPM);

  rpm = map(valorPot, 0, 4095, 0, 2000);

  // ===== TEMPERATURA =====

  sensors.requestTemperatures();

  temperatura = sensors.getTempCByIndex(0);

  // ===== NIVEL =====

  digitalWrite(PIN_TRIG, LOW);
  delayMicroseconds(2);

  digitalWrite(PIN_TRIG, HIGH);
  delayMicroseconds(10);

  digitalWrite(PIN_TRIG, LOW);

  long duration = pulseIn(PIN_ECHO, HIGH);

  nivel = duration * 0.034 / 2;

  nivel = map(nivel, 2, 400, 100, 0);

  // ===== BOTON =====

  if (digitalRead(PIN_BUTTON) == LOW)
  {
    alarma = true;
    estado = "FAULT";
  }
}

// ======================================================

void verificarAlarmas()
{
  if (temperatura > 80)
  {
    alarma = true;
    estado = "WARNING";
  }

  if (temperatura > 100)
  {
    alarma = true;
    estado = "FAULT";
  }

  if (!alarma)
  {
    estado = "RUNNING";
  }
}

// ======================================================

void actualizarLEDs()
{
  digitalWrite(PIN_LED_GREEN, LOW);
  digitalWrite(PIN_LED_YELLOW, LOW);
  digitalWrite(PIN_LED_RED, LOW);

  if (estado == "RUNNING")
  {
    digitalWrite(PIN_LED_GREEN, HIGH);
  }
  else if (estado == "WARNING")
  {
    digitalWrite(PIN_LED_YELLOW, HIGH);
  }
  else if (estado == "FAULT")
  {
    digitalWrite(PIN_LED_RED, HIGH);
  }
}

// ======================================================

void actualizarOLED()
{
  display.clearDisplay();

  display.setTextSize(1);

  display.setCursor(0, 0);
  display.println("VisualIoT Node");

  display.setCursor(0, 15);
  display.print("RPM: ");
  display.println(rpm);

  display.setCursor(0, 28);
  display.print("Temp: ");
  display.println(temperatura);

  display.setCursor(0, 41);
  display.print("Nivel: ");
  display.println(nivel);

  display.setCursor(0, 54);
  display.print("Estado: ");
  display.println(estado);

  display.display();
}

// ======================================================

void publicarMQTT()
{
  String payload = "{";

  payload += "\"device\":\"MOTOR_02\",";
  payload += "\"rpm\":" + String(rpm) + ",";
  payload += "\"temp\":" + String(temperatura) + ",";
  payload += "\"nivel\":" + String(nivel) + ",";
  payload += "\"estado\":\"" + estado + "\",";
  payload += "\"alarma\":" + String(alarma ? "true" : "false");

  payload += "}";

  client.publish(
    "visualiot/motores/motor02",
    payload.c_str()
  );

  Serial.println(payload);
}

// ======================================================

void mostrarMensaje(String texto)
{
  display.clearDisplay();

  display.setCursor(0, 20);

  display.println(texto);

  display.display();
}
