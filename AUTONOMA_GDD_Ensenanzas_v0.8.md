# AUTONOMA — GDD de Enseñanzas v0.8

Las 15 cosas que le podés enseñar al personaje con la **Posesión Pedagógica** (30 de Fe, entre 30 y 45 s, WASD + E). Los números salen directamente de `SkillTree` en `AutonomaCore.cs`. Las fórmulas están en la Biblia v0.8, sección 6.

## Cómo leer los porcentajes

- **Demo perfecta:** cuánto sube el conocimiento con una demostración de calidad 1.0, Vínculo 100 y sin modificadores de personalidad (DemoGain / Dificultad). El techo es 59%: mostrando nunca llega a Practicando.
- **Demos típicas:** cuántas posesiones hacen falta para llegar a **Imitando (30%)** con una demo de calidad 0.75 y Vínculo 50, contando que cada repetición del mismo día rinde ×0.8.
- **Prácticas:** cuántas ejecuciones exitosas suyas (de calidad 0.8) hacen falta para ir de 59% a **Dominado (100%)**. En la fase Imitando practica solo si se lo susurrás; desde Practicando, solo cuando la necesita.
- **Ojo con la utilidad:** en Practicando hace una habilidad sola solo si su utilidad llega a 70, y Dominada desde 40. Varias habilidades tranquilas (Juntar Palos 50, Talar 45, Trampa 55, Cama 40, Antorcha 55, Agua de Lluvia 45) nunca llegan a 70, así que en Practicando avanzan solo con susurros de "Practicá". Es a propósito: las urgentes (fuego, refugio, esconderse) las aprende sola porque las necesita; las de confort, porque se lo pedís.

## Árbol

```
RECOLECCIÓN                    SUPERVIVENCIA                         EMOCIONAL
Juntar Palos (T0) ─┬─ Talar (T1)                                     Esconderse (T0)
                   ├─ Hacer Fuego (T1) ─┬─ Cocinar en Olla (T2) ─┐       │
                   │                    ├─ Hacer Antorcha (T2)   │   Pedir Ayuda (T1)
                   │                    └────────┐               │       │
                   └─ Refugio de Palos (T1) ─┬─ Techo Fogata (T3)│   Calmarse Solo (T2)
                                             ├─ Cama de Paja (T2)│
                                             └───────────────────┴─ Agua de Lluvia (T3)
Identificar Bayas (T0) ── Rastrear (T1) ──┐
Juntar Palos ─────────────────────────────┴─ Trampa Simple (T2)
```
Para poder enseñar una habilidad, sus requisitos tienen que estar en **Practicando o más**: "cada habilidad desbloquea la siguiente".

## Tabla resumen

| # | Habilidad | Rama · Tier | Requisitos | Materiales (herramienta) | Demo perfecta | Demos típicas | Prácticas a Dominado |
|---|---|---|---|---|---|---|---|
| 1 | Juntar Palos | Recolección · T0 | — | 3 palos sueltos | 50% | 1 | 5 |
| 2 | Identificar Bayas Buenas | Recolección · T0 | — | arbusto conocido + desconocido | 25% | 3 | 7 |
| 3 | Talar | Recolección · T1 | Juntar Palos | árbol joven (piedra afilada) | 27% | 3 | 7 |
| 4 | Rastrear | Recolección · T1 | Identificar Bayas | 3 huellas en barro | 19% | 4 | 8 |
| 5 | Trampa Simple | Recolección · T2 | Rastrear + Juntar Palos | 2 palos, 1 liana, 1 baya | 18% | 4 | 8 |
| 6 | Hacer Fuego | Supervivencia · T1 | Juntar Palos | 3 palos, 1 hojas secas (piedra) | 30% | 2 | 6 |
| 7 | Refugio de Palos | Supervivencia · T1 | Juntar Palos | 4 palos largos, 6 hojas grandes | 30% | 2 | 6 |
| 8 | Cama de Paja | Supervivencia · T2 | Refugio de Palos | 4 paja | 44% | 2 | 5 |
| 9 | Cocinar en Olla | Supervivencia · T2 | Hacer Fuego | 2 ingredientes (olla) | 27% | 3 | 7 |
| 10 | Hacer Antorcha | Supervivencia · T2 | Hacer Fuego | 1 palo, 1 resina | 39% | 2 | 6 |
| 11 | Techo para la Fogata | Supervivencia · T3 | Refugio + Hacer Fuego | 3 palos largos, 4 hojas grandes | 19% | 4 | 8 |
| 12 | Juntar Agua de Lluvia | Supervivencia · T3 | Cocinar en Olla + Refugio | 1 hoja grande (olla) | 33% | 2 | 6 |
| 13 | Esconderse | Emocional · T0 | — | un escondite | 57% | 1 | 4 |
| 14 | Pedir Ayuda | Emocional · T1 | Esconderse | 3 piedras + lugar alto | 30% | 2 | 6 |
| 15 | Calmarse Solo | Emocional · T2 | Pedir Ayuda | fuego + recuerdo personal | 17% | 5 | 9 |

La curva está pensada así: las tres habilidades base (Juntar Palos, Esconderse y Bayas) se aprenden casi de una, y la más difícil del juego es **Calmarse Solo**, que además es requisito del Hogar Permanente. Aprender a no necesitarte es lo que más cuesta.

---

## Fichas

Cada ficha indica qué hacés en la posesión ("Demostración"), cuándo lo hace solo, qué gana al dominarlo, cómo suele fallar y qué manía tuya suele copiar.

### 1. Juntar Palos · `juntar_palos`
**Demostración (15 s ideales):** recoger 3 palos (E en cada uno) y llevarlos a la pila del campamento (E en la pila).
**Lo hace solo cuando:** tiene menos de 3 palos y hay palos cerca (utilidad 50, así que solo una vez Dominado).
**Al dominarlo:** mantiene sola una pila de 6 palos en el campamento.
**Cómo falla:** se le cae un palo en el camino y vuelve a buscarlo.
**Manía típica:** el *rodeo*. Si fuiste a buscar palos lejos, al principio él también va lejos aunque haya otros más cerca.

### 2. Identificar Bayas Buenas · `identificar_bayas`
**Demostración (20 s):** inspeccionar un arbusto conocido (mantener E 2 s, dos veces), recoger 2 bayas buenas y descartar una baya desconocida (E en el arbusto gris: la huele y la tira).
**Lo hace solo cuando:** Saciedad < 50 y hay arbustos cerca. La utilidad crece con el hambre.
**Al dominarlo:** queda inmune a Bayas de Ceniza (desde Practicando ya no se dispara el evento) y encuentra comida nueva en cada bioma.
**Cómo falla:** duda frente al arbusto desconocido y vuelve a inspeccionarlo.
**Manía típica:** la *pausa pensativa*, si te quedaste mirando el arbusto.

### 3. Talar · `talar`
**Demostración (25 s):** recoger una piedra afilada, golpear el árbol joven 6 veces (E repetido) y recoger el tronco.
**Lo hace solo cuando:** el campamento necesita troncos para subir de nivel, hay un árbol cerca y la Energía es > 50.
**Al dominarlo:** junta los troncos para el nivel 3 sin que se lo pidas. Talar Dominado es requisito del RV fortificado.
**Cómo falla:** la piedra se resbala y tiene que buscar otra.
**Manía típica:** los *golpes de más*.

### 4. Rastrear · `rastrear`
**Demostración (25 s):** inspeccionar 3 huellas seguidas (mantener E 1.5 s en cada una, que aparecen después de la lluvia) y señalar hacia donde van.
**Lo hace solo cuando:** Curiosidad > 50 y hay huellas cerca.
**Al dominarlo:** sigue huellas por su cuenta y encuentra agua, crías (sube la probabilidad de La Cría) y senderos de animales.
**Cómo falla:** pierde el rastro y señala hacia cualquier lado.
**Manía típica:** el *orden invertido*, si inspeccionaste las huellas de atrás para adelante.

### 5. Trampa Simple · `trampa_simple`
**Demostración (30 s):** recoger 2 palos y 1 liana, ir a un sendero de animales, colocar los 2 palos, poner la baya de cebo (agacharse) y ajustar con 2 golpes.
**Lo hace solo cuando:** Saciedad < 60 y conoce un sendero.
**Al dominarlo:** arma trampas y las revisa al amanecer: es comida que no depende de las bayas.
**Cómo falla:** la trampa queda floja (calidad baja) y el animal se escapa.
**Manía típica:** los *golpes de más*.

### 6. Hacer Fuego · `hacer_fuego` (obra: fogata)
**Demostración (25 s):** recoger 3 palos, hojas secas y una piedra; colocar los 3 palos en círculo y las hojas en el centro; encender golpeando la piedra 4 veces.
**Lo hace solo cuando:** es de noche, está atardeciendo, tiene miedo o está mojado, y no hay un fuego a menos de 8 m.
**Al dominarlo:** prende el fuego solo al atardecer y ya no necesita tus antorchas. La primera vez que lo hace solo se dispara Primer Fuego Propio.
**Cómo falla:** la chispa no prende y vuelve a intentar. Con calidad baja, el fuego dura menos.
**Manía típica:** la *pausa pensativa* antes de encender.
**Consejo:** si le tirás una antorcha mientras intenta su primer fuego, se lo arruinás.

### 7. Refugio de Palos · `refugio_palos` (obra: 4 palos verticales + techo de hojas)
**Demostración (40 s):** recoger 4 palos largos; clavarlos (4 veces E sobre la obra fantasma); martillar cada uno; recoger 6 hojas grandes; poner el techo en 2 capas.
**Lo hace solo cuando:** llueve y no hay refugio a menos de 30 m (utilidad 90), o hay lluvia en el pronóstico para las próximas 2 horas (60, solo si está Dominado).
**Al dominarlo:** ve venir la lluvia y construye o va a su refugio antes de mojarse.
**Cómo falla:** un palo queda torcido. Con calidad < 0.5, **el refugio gotea**.
**Manía típica:** el *rodeo* y los *golpes de más*. Es la habilidad en la que más se nota la firma del maestro.
**Consejo:** enseñarla durante el pronóstico de lluvia es lo más natural, porque al rato la va a necesitar.

### 8. Cama de Paja · `cama_paja` (obra adentro del refugio)
**Demostración (25 s):** recoger 4 paja, ponerla adentro del refugio en 2 montones y alisarla (mantener E 2 s).
**Lo hace solo cuando:** tiene refugio pero no cama, y hay paja cerca.
**Al dominarlo:** dormir rinde +30% de Energía y de noche el miedo sube 20% menos.
**Cómo falla:** la paja queda despareja y el sueño rinde menos.
**Manía típica:** la *pausa pensativa*.

### 9. Cocinar en Olla · `olla_cocinar`
**Demostración (25 s):** recoger la olla del campamento, ponerla sobre el fuego, agregar 2 ingredientes y revolver (mantener E 3 s).
**Lo hace solo cuando:** hay fuego a menos de 8 m, tiene ingredientes y tiene hambre o está enfermo (con enfermedad, la utilidad es 80).
**Al dominarlo:** cada comida cocida da +35 de Saciedad, y el caldo le saca 12 horas a la enfermedad.
**Cómo falla:** se le quema y rinde la mitad.
**Manía típica:** el *orden invertido* (ingredientes antes que la olla).

### 10. Hacer Antorcha · `antorcha`
**Demostración (20 s):** recoger un palo, sacar resina de un árbol (mantener E 1.5 s), envolver el palo junto al fuego y encenderlo en el fuego.
**Lo hace solo cuando:** está atardeciendo o es de noche, no tiene antorcha, hay fuego y resina cerca.
**Al dominarlo:** se hace su propia antorcha y las antorchas divinas casi no hacen falta.
**Cómo falla:** la resina no agarra y la antorcha dura poco.

### 11. Techo para la Fogata · `techo_fogata` (obra sobre la fogata)
**Demostración (35 s):** recoger 3 palos largos, colocarlos inclinados alrededor del fuego (como un trípode), recoger 4 hojas grandes, ponerlas como techo y ajustar con un golpe.
**Lo hace solo cuando:** hay lluvia en el pronóstico o ya llueve, hay un fuego cerca y no tiene techo (utilidad 75).
**Al dominarlo:** la fogata ya no se apaga con la lluvia. Es requisito del nivel 3 del campamento.
**Cómo falla:** el techo queda corrido y el agua igual llega al fuego por un costado (calidad baja).
**Manía típica:** los *golpes de más*.

### 12. Juntar Agua de Lluvia · `agua_lluvia` (obra: colector)
**Demostración (25 s):** llevar la olla a un claro, poner una hoja grande como embudo y sentarse a esperar (mantener E 3 s).
**Lo hace solo cuando:** llueve con intensidad > 0.2 y no tiene agua guardada.
**Al dominarlo:** la lluvia deja de asustarlo tanto (Miedo +10 en vez de +30 cuando empieza, y Ánimo +5). Es el cambio emocional más lindo del árbol: aprende que la lluvia también da.
**Cómo falla:** la hoja se vuela.

### 13. Esconderse · `esconderse`
**Demostración (15 s):** llevarlo a un escondite y mantener E 5 s (agachado y quieto).
**Lo hace solo cuando:** hay una amenaza a menos de 25 m (utilidad 85) o una tormenta sin refugio (60).
**Al dominarlo:** escondido, el miedo por la amenaza sube la mitad y las bestias pierden su rastro.
**Cómo falla:** se mueve antes de tiempo y lo ven.
**Consejo:** es la que más rápido aprende un Miedoso (×1.25) y la que más le cuesta a un Valiente (×0.85).

### 14. Pedir Ayuda · `pedir_ayuda`
**Demostración (25 s):** recoger 3 piedras, apilarlas en un lugar alto y señalar al cielo (mantener E 2 s).
**Lo hace solo cuando:** Miedo > 70 o Saciedad < 20, Vínculo > 30 y no recibió influencias en las últimas 2 horas.
**Al dominarlo:** cuando está en crisis te llama con una señal visible en el mapa. Responderle cuesta la mitad de Fe. Si lo ignorás, se siente abandonado (cuenta para el Silencio del Dios).
**Cómo falla:** la pila se cae.

### 15. Calmarse Solo · `calmarse_solo`
**Demostración (25 s):** sentarlo junto al fuego (mantener E 1.5 s), respirar 4 veces (mantener E 1.5 s cada vez, siguiendo el ritmo de un círculo que se expande y se contrae) y mirar el recuerdo personal (mantener E 2.5 s: un objeto del RV, como una foto o un juguete).
**Lo hace solo cuando:** Miedo > 60 y pasaron 6 horas desde la última vez.
**Al dominarlo:** se calma sin vos (−30 de Miedo). Es requisito del Hogar Permanente.
**Cómo falla:** se distrae y la respiración se corta.
**Por qué es la más difícil:** con Dificultad 1.5, cada demo rinde poco y el miedo alto empeora el aprendizaje (×0.7). Hay que enseñarla en momentos de calma para que sirva en los de pánico. Es la lección que el juego quiere que el jugador entienda.

---

## Ritmo esperado de una partida

| Días | Qué pasa |
|---|---|
| 1–3 | Le enseñás Juntar Palos, Esconderse y Hacer Fuego. Te imita en todo, se le apaga el fuego con la primera lluvia y lo pasa mal. |
| 4–8 | Refugio de Palos y Cocinar en Olla llegan a Practicando. Aparecen los primeros "Lo Hizo Solo". La segunda lluvia ya no lo agarra tan desprevenido. Campamento en nivel 1. |
| 9–15 | Cama de Paja, Talar y Pedir Ayuda. Las primeras Dominadas. Olvida algo después de un susto (Lo Olvidó). Campamento en nivel 2. |
| 15–25 | Techo para la Fogata, Agua de Lluvia y Calmarse Solo. Construye antes de que llueva. Las posesiones se vuelven raras. Nivel 3 y Hogar cerca del 100%. |
