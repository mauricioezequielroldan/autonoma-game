# AUTONOMA — GDD extendido v1.0
## Ampliación técnica: clima, aprendizaje, guardado y rendimiento

**Base de diseño:** AUTONOMA — Biblia v0.8 y GDD de Enseñanzas v0.8.  
**Objetivo:** extender el prototipo sin alterar las reglas núcleo.

## 1. Invariantes que no se deben romper

- El conocimiento obtenido por demostración tiene un techo de **59%**. Practicar es necesario para alcanzar Dominado (100%).
- La Posesión Pedagógica cuesta **30 Fe**. Duración base objetivo: **35 s**; el diseño v0.8 también describe duración escalable por Vínculo y modificadores. En la integración, definir explícitamente si 35 s es el valor fijo de v1.0 o el valor base de la fórmula.
- El olvido aplica una reducción relativa del **10% por cada 48 horas de juego** a habilidades no dominadas. No se aplica a Dominado salvo que diseño lo cambie expresamente.
- El pánico resta **20 puntos** de conocimiento a habilidades no dominadas. Resiliencia puede mitigar la pérdida según el diseño existente; no debe borrar habilidades Dominadas.
- La **firma del maestro** persiste como una manía observable por habilidad (rodeos, golpes extra, pausa pensativa, orden invertido, etc.).
- Las fases son: No aprendido 0–29, Imitando 30–59, Practicando 60–99, Dominado 100. Dominado se conserva hasta que una regla de olvido explícita lo permita caer por debajo de 85; el sistema base no lo decae automáticamente.
- La progresión autónoma debe seguir la utilidad y los requisitos del GDD de Enseñanzas.

## 2. Clima ampliado

### Estados
- **Despejado:** visibilidad normal; secado gradual.
- **Viento:** empuja partículas, reduce ligeramente la precisión de fuego y puede aumentar exposición térmica.
- **Niebla:** baja visibilidad y aumenta incertidumbre de navegación; no debería infligir daño por sí sola.
- **Lluvia:** moja al personaje, reduce la temperatura y apaga una fogata expuesta. La regla heredada es aproximadamente 25 segundos reales a intensidad máxima.
- **Ventisca:** combina frío, viento y visibilidad muy baja. Debe ser peligrosa fuera de refugio, pero anunciarse con antelación.

### Pronóstico y respuesta aprendida
- El pronóstico se presenta **1,5 horas de juego** antes de la transición.
- Los episodios de tormenta duran entre **3 y 6 horas de juego**.
- El personaje solo construye/refuerza refugio si dispone del conocimiento requerido y materiales; no conceder comportamiento aprendido por mera presencia del sistema climático.
- `Refugio de Palos` permite buscar refugio y reaccionar a lluvia pronosticada; `Techo para la Fogata` protege el fuego cuando se cumplan sus requisitos.
- La exposición, humedad y frío deben ser variables continuas de 0–100. El clima visual (niebla, partículas, shader de lluvia) debe consumir un snapshot de simulación, no ser la fuente de verdad del gameplay.
- Los valores de daño/enfermedad de viento, niebla y ventisca quedan como parámetros de balance, no como valores canónicos de la Biblia v0.8.

## 3. Más refugios

| Refugio | Función | Requisitos sugeridos | Contrapartida |
|---|---|---|---|
| Alero de ramas | Protección mínima contra lluvia | Palos + hojas | No protege bien del viento |
| Refugio de palos | Refugio de aprendizaje base | Skill `refugio_palos` | Puede gotear si la demostración fue de baja calidad |
| Refugio reforzado | Protección contra viento/ventisca | Refugio de palos + `talar` Dominado + troncos | Costoso en energía y materiales |
| Cueva | Protección natural | Encontrar/descubrir entrada | Oscuridad, posible riesgo de eventos |
| Techo para fogata | Mantiene encendido el fuego | `techo_fogata` + materiales | Requiere mantenimiento |

Estos refugios son propuestas v1.0 y no reemplazan el refugio de palos canónico sin aprobación de diseño.

## 4. Arquitectura de aprendizaje compacta

### Agentes como datos
Cada una de las 15 habilidades es un registro de datos: `id`, conocimiento, timestamps, fase derivada, memoria episódica limitada y firma del maestro. No tiene su propio `Update`, árbol de comportamiento, nodo de escena ni suscripción global.

### Tres nodos fijos por personaje
1. `NeedsAndTraits`: necesidades, rasgos, Vínculo, Fe y estados afectivos.
2. `LearningSystem`: 15 registros de habilidad, memoria episódica compacta, olvido y firmas.
3. `SkillExecutor`: única cola/ejecutor de acciones autónomas; recibe una habilidad elegida por utilidad y valida recursos, requisitos y entorno.

En Three.js estos “nodos” son módulos/objetos de composición, no nodos de escena de Godot. Si el proyecto se mantiene en Three.js, las clases C# entregadas aquí deben portarse a TypeScript/JavaScript o ejecutarse en un servicio/runtime .NET separado.

### Memoria episódica
- Máximo sugerido: 64 tokens por habilidad activa; preferiblemente un presupuesto global de 64 por personaje para reducir memoria.
- Un token almacena acción, etiqueta del objetivo, posición relativa cuantizada, calidad y minuto de juego. No almacenar transformaciones completas ni referencias vivas del motor.
- La firma del maestro almacena un `quirkId` y una intensidad, no una grabación de animación.
- Comprimir coordenadas relativas a bytes y deduplicar acciones repetidas.

## 5. Balance de aprendizaje

- Las demostraciones deben capear el resultado final a 59%, independientemente de personalidad o calidad.
- Los requisitos de una habilidad se cumplen si cada requisito está en Practicando o Dominado (conocimiento ≥60).
- Imitando practica solo si recibe el susurro “Practicá”.
- Practicando: utilidad ≥70 o instrucción explícita. Dominado: umbral de utilidad bajo (40) para ejecutar la rutina con más autonomía.
- Evitar una contradicción en el código: la condición “utilidad ≥70” y la excepción de susurro explícito deben evaluarse en un único lugar.
- El olvido es multiplicativo: `conocimiento *= 0.9` por cada bloque completo de 48 horas de juego. Ejemplo: 60 → 54 → 48,6.
- El pánico resta 20 puntos absolutos a cada habilidad no dominada. La mitigación por Resiliencia debe estar definida en una única función y probada para que no existan dos implementaciones distintas.
- La progresión debe usar el tiempo del mundo simulado, no el reloj del sistema operativo.

## 6. Guardado y recuperación

Guardar:
- versión de esquema y versión del build;
- semilla del mundo y tiempo de juego;
- necesidades, rasgos, Vínculo, Fe, humedad y exposición al frío;
- nivel y materiales del campamento;
- estado del clima y duración restante;
- habilidades, timestamps, firmas del maestro y memoria episódica;
- ids de eventos y memorias narrativas.

Reglas:
- Guardado atómico mediante archivo temporal y reemplazo.
- Mantener copia `.bak` del último guardado válido.
- Verificar integridad con checksum.
- Migrar por `SchemaVersion`; nunca asumir que una versión antigua tiene campos nuevos.
- Guardar en hitos: cambio de día, evento terminal, avance de campamento, habilidad Dominada y al salir; añadir autosave periódico configurable.
- No guardar objetos del motor, meshes, referencias de escena ni funciones. Guardar IDs y datos serializables.
- En navegador, usar IndexedDB o un adaptador equivalente; no usar rutas de archivo de escritorio. El `AutonomaSave.cs` es un contrato de referencia para runtime C#.

## 7. Rendimiento y perfiles

Objetivos de implementación, a validar con profiling real:
- 15 agentes de habilidad como registros ligeros; no 15 procesos autónomos.
- Un único planificador por personaje, con evaluación de utilidad a 1 Hz.
- Sensores compartidos por personaje y caché de resultados del entorno.
- Actualizar necesidades/clima a frecuencias menores que renderizado; no atar simulación al framerate.
- Evitar LINQ y asignaciones temporales en bucles calientes si el profiler muestra presión de GC.
- Para Three.js, mantener la simulación separada del render loop; interpolar la representación visual entre ticks.

## 8. Pruebas de aceptación

1. Una demostración perfecta repetida no lleva la habilidad por encima de 59%.
2. Una habilidad con requisito por debajo de 60 no se puede enseñar.
3. Posesión sin 30 Fe o con Vínculo <25 no inicia.
4. Una habilidad no dominada pierde exactamente 10% relativo tras 48 horas simuladas.
5. Una habilidad Dominada no se degrada en el ciclo de olvido base.
6. El pánico resta 20 puntos absolutos a habilidades no dominadas; las Dominadas permanecen intactas.
7. Se conserva la firma del maestro después de guardar/cargar.
8. Con lluvia máxima y fuego expuesto, el contador llega a cero en aproximadamente 25 segundos reales.
9. Refugio conocido + pronóstico válido provoca decisión de refugiarse; sin habilidad no se otorga esa respuesta automáticamente.
10. Un guardado corrupto intenta recuperar la copia de respaldo.
11. La carga de una versión anterior inicializa campos faltantes sin borrar memorias conocidas.
12. El número de ejecutores de habilidades por personaje es exactamente uno.

## 9. Notas de integración y límites

Los archivos `.cs` son un núcleo de referencia independiente del motor. El texto compartido describe un prototipo Three.js, pero no incluye el código actual `LearningSystem.js` ni el esquema de guardado existente. Por eso el archivo `LearningSystem.cs` es una implementación de referencia y no un parche verificado contra el repositorio. Antes de reemplazar el sistema actual, mapear sus IDs reales y ejecutar las pruebas de aceptación.
