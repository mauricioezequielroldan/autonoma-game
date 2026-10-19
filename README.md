# AUTONOMA v1.0 — paquete de referencia

Incluye:
- `AutonomaWeather.cs`: clima de simulación con pronóstico, lluvia, viento, niebla y ventisca.
- `AutonomaSave.cs`: contrato de guardado versionado, checksum, escritura atómica y respaldo.
- `LearningSystem.cs`: sistema de aprendizaje de referencia con agentes compactos, demostración, práctica, olvido, pánico, memoria episódica y firma del maestro.
- `GDD_Extendido_v1.0.md`: propuesta ampliada y criterios de aceptación.

## Importante
El proyecto descrito por el usuario está hecho en Three.js. Los `.cs` no se pueden importar directamente a Three.js. Estos archivos definen el comportamiento y los contratos para un runtime C#; para integrar en el prototipo web hay que portar la lógica a TypeScript/JavaScript o comunicar el juego con un runtime .NET.

`AutonomaSave.cs` recibe un `ISaveCodec` para no asumir qué serializador usa el proyecto. Implementar ese codec con el serializador disponible en el destino.

`LearningSystem.cs` es una base de referencia, no un parche comprobado sobre el archivo existente: no se proporcionó el `LearningSystem.js` actual. Revisar IDs, tiempos y reglas de fase contra el código real antes de sustituirlo.
