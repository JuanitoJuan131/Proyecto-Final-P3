#include <WiFi.h>
#include <PubSubClient.h>

#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>

#include <OneWire.h>
#include <DallasTemperature.h>

 // OLED
 
#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 64

Adafruit_SSD1306 display(
  SCREEN_WIDTH,
  SCREEN_HEIGHT,
  &Wire,
  -1
);

 // WIFI
 
const char* ssid = "Wokwi-GUEST";
const char* password = "";

 // MQTT
 
const char* mqtt_server = "broker.hivemq.com";
const char* mqtt_topic = "visualiot/motores/motor01";
const char* device_id = "MOTOR_01_TANQUE";

WiFiClient espClient;
PubSubClient client(espClient);

 // PINES
 
#define PIN_TEMP 4

#define PIN_LED_GREEN 15
#define PIN_LED_YELLOW 2
#define PIN_LED_RED 13

#define PIN_BUTTON 27

#define PIN_TRIG 5
#define PIN_ECHO 18

#define PIN_RPM 34

 // TEMPERATURA
 
OneWire oneWire(PIN_TEMP);

DallasTemperature sensors(&oneWire);

 // VARIABLES
 
float temperatura = 0;

int rpm = 0;

float nivelTanque = 0;

bool alarma = false;

String estado = "RUNNING";

 // SETUP
 
void setup()
{
  Serial.begin(115200);

  // LEDs

  pinMode(PIN_LED_GREEN, OUTPUT);
  pinMode(PIN_LED_YELLOW, OUTPUT);
  pinMode(PIN_LED_RED, OUTPUT);

  // Botón emergencia

  pinMode(PIN_BUTTON, INPUT_PULLUP);

  // Ultrasonido

  pinMode(PIN_TRIG, OUTPUT);
  pinMode(PIN_ECHO, INPUT);

  // OLED

  if (!display.begin(SSD1306_SWITCHCAPVCC, 0x3C))
  {
    Serial.println("ERROR OLED");

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
  client.setKeepAlive(30);
  client.setSocketTimeout(10);

  mostrarMensaje("VisualIoT Ready");
}

 // LOOP
 
void loop()
{
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

 // WIFI
 
void conectarWiFi()
{
  WiFi.begin(ssid, password);

  while (WiFi.status() != WL_CONNECTED)
  {
    delay(500);

    Serial.print(".");
  }

  Serial.println("");

  Serial.println("WIFI CONECTADO");
}

 // MQTT
 
void reconnectMQTT()
{
  while (!client.connected())
  {
    Serial.println("Conectando MQTT...");

    String clientId = "VisualIoT_";
    clientId += device_id;
    clientId += "_";
    clientId += WiFi.macAddress();
    clientId.replace(":", "");

    if (client.connect(clientId.c_str()))
    {
      Serial.println("MQTT CONECTADO");
    }
    else
    {
      Serial.println("Error MQTT");

      delay(2000);
    }
  }
}

 // LEER SENSORES
 
void leerSensores()
{
   // RPM
 
  int valorPot = analogRead(PIN_RPM);

  rpm = map(valorPot, 0, 4095, 0, 2000);

   // TEMPERATURA
 
  sensors.requestTemperatures();

  temperatura = sensors.getTempCByIndex(0);

   // NIVEL TANQUE
 
  digitalWrite(PIN_TRIG, LOW);

  delayMicroseconds(2);

  digitalWrite(PIN_TRIG, HIGH);

  delayMicroseconds(10);

  digitalWrite(PIN_TRIG, LOW);

  long duration = pulseIn(PIN_ECHO, HIGH);

  nivelTanque = duration * 0.034 / 2;

  nivelTanque = map(nivelTanque, 2, 400, 100, 0);

  // Limitar valores

  if (nivelTanque > 100)
  {
    nivelTanque = 100;
  }

  if (nivelTanque < 0)
  {
    nivelTanque = 0;
  }

   // BOTON EMERGENCIA
 
  if (digitalRead(PIN_BUTTON) == LOW)
  {
    alarma = true;

    estado = "FAULT";
  }
}

 // VERIFICAR ALARMAS
 
void verificarAlarmas()
{
  alarma = false;

  estado = "RUNNING";

   // TEMPERATURA
 
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

   // NIVEL TANQUE
 
  if (nivelTanque < 40)
  {
    alarma = true;

    estado = "WARNING";
  }

  if (nivelTanque < 20)
  {
    alarma = true;

    estado = "FAULT";
  }

   // BOTON EMERGENCIA
 
  if (digitalRead(PIN_BUTTON) == LOW)
  {
    alarma = true;

    estado = "FAULT";
  }
}

 // LEDS
 
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

 // OLED
 
void actualizarOLED()
{
  display.clearDisplay();

  display.setTextSize(1);

  display.setCursor(0, 0);
  display.println("VisualIoT MOTOR_01");

  display.setCursor(0, 14);
  display.print("RPM: ");
  display.println(rpm);

  display.setCursor(0, 26);
  display.print("Temp: ");
  display.print(temperatura);
  display.println(" C");

  display.setCursor(0, 38);
  display.print("Tanque: ");
  display.print(nivelTanque);
  display.println("%");

  display.setCursor(0, 50);
  display.print("Estado: ");
  display.println(estado);

  display.display();
}

 // MQTT JSON
 
void publicarMQTT()
{
  String payload = "{";

  payload += "\"device\":\"MOTOR_01\",";
  
  payload += "\"nombre\":\"Motor Principal\",";
  
  payload += "\"tipo\":\"Tanque Lubricacion\",";
  
  payload += "\"rpm\":" + String(rpm) + ",";
  
  payload += "\"temperatura\":" + String(temperatura) + ",";
  
  payload += "\"nivelTanque\":" + String(nivelTanque) + ",";
  
  payload += "\"estado\":\"" + estado + "\",";
  
  payload += "\"alarma\":" + String(alarma ? "true" : "false");

  payload += "}";

  client.publish(mqtt_topic, payload.c_str());

  Serial.println(payload);
}

 // OLED MENSAJE
 
void mostrarMensaje(String texto)
{
  display.clearDisplay();

  display.setTextSize(1);

  display.setCursor(0, 20);

  display.println(texto);

  display.display();
}
