# AUTONOMA — Biblia v0.8

*"Un personaje que se automaneja por dependencia para sobrevivir. Podés tomar el control por un tiempo para enseñarle cosas, y lo va aprendiendo."*

Tamagotchi + Don't Starve + El Niño Salvaje + aprendizaje por imitación. Este documento reúne personalidad, eventos, aprendizaje, clima, Fe, arte y arquitectura técnica. Las 15 habilidades enseñables, con materiales y porcentajes, están en el documento aparte **AUTONOMA_GDD_Ensenanzas_v0.8.md**. Los ids (`en_este_formato`) son los mismos que usa el código de la carpeta `scripts/`.

## Novedades de v0.8

- **Sistema de aprendizaje por agentes** (sección 6): Posesión Pedagógica, 15 agentes de habilidad, memoria episódica, olvido y la "firma del maestro".
- **Lluvia completa** (sección 7): pronóstico, humedad, enfermedad, fogatas que se apagan y el refugio como respuesta aprendida.
- **Fe y Hogar 0–100%** implementados (sección 8).
- **25 eventos**: cinco nuevos (21–25) y cuatro ajustados (4, 5, 13 y 18) para que convivan con el aprendizaje.
- **Dirección de arte** (sección 9) y **propuesta técnica** para que los agentes no pesen en Godot (sección 10).

---

## 1. Ajustes al GDD antes de construir

Hay seis cosas del GDD v0.1 que se rompen al programarlas. Las resolví así, pero son decisiones de diseño y se pueden discutir.

**Hambre → Saciedad.** En el GDD, "Hambre" baja con el tiempo y sube comiendo, y con menos de 20 el personaje cojea. Entonces en realidad mide saciedad, porque 100 significa lleno. La renombré para que no haya confusión en el código: Saciedad 100 = lleno, Saciedad < 20 = cojea y se mueve un 50% más lento.

**Ánimo no puede ser "el promedio de todo".** El Miedo alto es malo y la Curiosidad no es buena ni mala, así que un promedio directo da cualquier cosa. La fórmula nueva es: Ánimo objetivo = 0.30·Saciedad + 0.25·Energía + 0.30·(100 − Miedo) + 0.15·Vínculo. El Ánimo real se mueve suavizado hacia ese objetivo: sube rápido y baja lento, y la Resiliencia frena la caída. Sin ese suavizado, un susto de dos segundos lo mandaría directo a la rendición.

**"Necesidad más alta" → "necesidad más urgente".** Si se toma la necesidad con el valor más alto, un personaje lleno y descansado (100/100) saldría a "comer". La urgencia se calcula así: Comer = 100 − Saciedad, Dormir = 100 − Energía (+20 de noche), Buscar luz/refugio = Miedo, Explorar = Curiosidad × (0.5 + rasgo Curiosidad/100). Gana la urgencia más alta.

**Fórmula de aceptación de susurros.** La original (Curiosidad·0.5 + Vínculo·0.3 − Miedo·0.4) sirve solo para "explorá", y da negativo con facilidad. Ahora queda así: Aceptación = 20 + Afinidad + Vínculo·0.3·(0.5 + Lealtad/100) − Miedo·0.4 (+15 si ocurrió El Nombre). El resultado se limita a un rango de 5%–95%, así que nunca es seguro ni imposible. La Afinidad depende de qué le susurrás:

| Susurro | Afinidad |
|---|---|
| Explorar | Curiosidad·0.5·(0.5 + rasgo Curiosidad/100) |
| Comer | (100 − Saciedad)·0.5 |
| Dormir | (100 − Energía)·0.5 |
| Refugiarse | Miedo·0.5·(1.5 − Valentía/100) — en este susurro el miedo **no** resta |
| Acercarse | Empatía·0.3 + Curiosidad·0.2 |
| Evitar | Miedo·0.3 + (100 − Valentía)·0.2 — el miedo tampoco resta |

Con Vínculo < 20, la aceptación final se multiplica por 0.25 (casi no te escucha). En estado Rendido (Ánimo < 15), acepta el susurro pero no lo ejecuta: dice que sí y se queda quieto.

**"Curiosidad" aparece dos veces.** Es una necesidad (impulso que sube y baja) y también un rasgo (personalidad estable). Las dos se mantienen, pero en el documento las llamo *Curiosidad (estado)* y *rasgo Curiosidad*, y en el código son `Needs.Curiosidad` y `Traits.Curiosidad`.

**Las influencias cuestan Fe (implementado en v0.8).** Ver sección 8.

---

## 2. Sistema de personalidad

### 2.1 Cómo funcionan los rasgos

Cada personaje tiene cinco rasgos estables de 0 a 100: **Valentía, Curiosidad, Lealtad, Empatía, Resiliencia**. Los rasgos afectan de forma continua a las fórmulas (velocidad con la que sube el miedo, peso del vínculo, caída del ánimo). Además, definen **perfiles de reacción** para los eventos:

| Perfil | Condición | Fuerza |
|---|---|---|
| Valiente | Valentía ≥ 65 | Valentía − 50 |
| Miedoso | Valentía ≤ 35 | 50 − Valentía |
| Curioso | rasgo Curiosidad ≥ 65 | Curiosidad − 50 |
| Leal | Lealtad ≥ 65 | Lealtad − 50 |
| Empático | Empatía ≥ 65 | Empatía − 50 |
| Resiliente | Resiliencia ≥ 65 | Resiliencia − 50 |

**Regla de resolución:** cada evento define reacciones solo para algunos perfiles. Entre los perfiles que el evento define y que el personaje cumple, gana el de mayor fuerza. Si no cumple ninguno, se usa la reacción *Neutral*, que es el comportamiento por defecto del árbol de IA. Gracias a esto, una personalidad generada al azar también funciona y no hace falta escribir reacciones para cada combinación.

**Disonancia moral:** cuando una Decisión Moral contradice un rasgo fuerte, el personaje obedece, pero el Vínculo baja. Elegir Huir con Valentía > 65 resta (Valentía − 65)·0.3. Elegir Atacar con Empatía > 65 resta (Empatía − 65)·0.4. Con Empatía ≥ 85, el personaje **se niega** a Atacar y paga la disonancia igual. Así el jugador puede influir, pero no puede convertir al personaje en otra persona.

### 2.2 Las 5 personalidades base

Son presets. El código también permite generar variantes con ±10 de ruido sobre cada preset, para que dos partidas con "La Errante" no sean idénticas.

---

#### El Centinela
| Valentía | Curiosidad | Lealtad | Empatía | Resiliencia |
|---|---|---|---|---|
| 85 | 35 | 70 | 45 | 75 |

**Perfil dominante:** Valiente (secundarios: Resiliente, Leal).
**Quién es:** un guardián que no sabe estar quieto si algo lo amenaza. Su miedo sube un 40% más lento que el de los demás. Explora poco, pero defiende mucho.
**Cómo se juega:** acepta bien los susurros de acción y te tiene cariño. Lo que no soporta es que lo hagas huir: cada Huir cuesta unos 6 puntos de Vínculo. Aceptación típica de "¿Y si explorás?": ~56%.
**Riesgo principal:** se mete solo en peleas. Cazado y las heridas son su forma más común de morir.
**Final natural:** Hogar Permanente (con empalizada) o Guía de la Tribu como protector.
**Final difícil:** La Carta de Despedida, porque no tiene ganas de buscar fragmentos de mapa.
**Idle:** afila un palo, vigila el horizonte y se para entre el fuego y la oscuridad.
**Cómo aprende:** rápido en Supervivencia (×1.1) y confía en lo que le mostrás (×1.15 por Leal). Esconderse le cuesta (×0.85) porque no le ve sentido. Al ser Resiliente, el pánico no le borra nada.

---

#### La Errante
| Valentía | Curiosidad | Lealtad | Empatía | Resiliencia |
|---|---|---|---|---|
| 55 | 90 | 30 | 50 | 55 |

**Perfil dominante:** Curioso.
**Quién es:** se va del fuego para ver qué hay detrás de la colina. Su Curiosidad (estado) sube un 40% más rápido que lo normal.
**Cómo se juega:** con Lealtad baja, el Vínculo pesa poco, así que te hace caso cuando lo que le decís le interesa. "Explorá" casi siempre funciona y "Volvé al fuego" casi nunca. Aceptación típica de "¿Y si explorás?": ~66%.
**Riesgo principal:** Bayas de Ceniza (las prueba antes que nadie), Grieta en el Hielo y quedarse lejos del fuego cuando cae la noche.
**Final natural:** La Carta de Despedida.
**Final difícil:** Hogar Permanente (no se queda en un lugar) y El Nombre (no se apega).
**Idle:** junta piedras raras, dibuja en la tierra y se queda mirando a lo lejos.
**Cómo aprende:** la más rápida de todas (×1.2 por Curiosa) y aprende más haciendo que mirando (demos ×0.85, práctica ×1.25). Durante la posesión se te escapa hacia lo que le llama la atención (nodos del grupo `curiosidad`).

---

#### El Devoto
| Valentía | Curiosidad | Lealtad | Empatía | Resiliencia |
|---|---|---|---|---|
| 30 | 40 | 95 | 70 | 35 |

**Perfil dominante:** Leal (secundarios: Empático, Miedoso).
**Quién es:** cree en vos. Para él sos la única cosa buena del mundo. Es asustadizo y frágil por dentro.
**Cómo se juega:** te hace caso casi siempre y es el más fácil de vincular. Su Vínculo se erosiona despacio, pero si lo descuidás se desmorona: El Silencio del Dios lo golpea fuerte y la Rendición está cerca. Aceptación típica de "¿Y si explorás?": ~61%.
**Riesgo principal:** La Rendición y el Último Abrazo a la Luz.
**Final natural:** El Nombre, seguido de Guía de la Tribu como sacerdote o de una Carta larga y agradecida.
**Final difícil:** Hogar Permanente, porque dejar de necesitarte va contra lo que es. Primer Fuego Propio también le cuesta.
**Idle:** mira al cielo, apila piedras en forma de altar y le habla a la antorcha.
**Cómo aprende:** absorbe tus demos (×1.15) y te deja la posesión más larga, casi sin resistencia. El problema es que es frágil: con Miedo > 50 aprende peor y cada ataque de pánico le borra 20 puntos de lo que no domina. Es el que más copia tus manías.

---

#### La Cuidadora
| Valentía | Curiosidad | Lealtad | Empatía | Resiliencia |
|---|---|---|---|---|
| 45 | 50 | 55 | 95 | 60 |

**Perfil dominante:** Empático.
**Quién es:** vive para algo o alguien. Si tiene a quién cuidar, florece; si está sola, se apaga despacio.
**Cómo se juega:** se niega a Atacar, sin excepción. Comparte la comida, así que su Saciedad es su punto débil. Es la única que puede ver la opción "Curar" con buena probabilidad en Cazado. Aceptación típica de "¿Y si explorás?": ~58%.
**Riesgo principal:** pasar hambre por darle comida a la cría o al extraño, y deprimirse si la cría muere.
**Final natural:** Guía de la Tribu como curandera.
**Final difícil:** cualquiera en el que termine sola. Sin La Cría ni El Extraño Herido, su Ánimo no tiene de dónde sostenerse.
**Idle:** acaricia animales, junta hierbas y venda cosas que no lo necesitan.
**Cómo aprende:** las habilidades Emocionales le salen solas (×1.15). Es la candidata natural para el evento 25, Le Enseña a Otro: lo que vos le enseñás, ella se lo enseña a la cría.

---

#### El Superviviente
| Valentía | Curiosidad | Lealtad | Empatía | Resiliencia |
|---|---|---|---|---|
| 40 | 20 | 20 | 25 | 95 |

**Perfil dominante:** Resiliente.
**Quién es:** ya perdió todo una vez y no piensa volver a confiar. Es metódico y seco. Es casi imposible de romper.
**Cómo se juega:** te hace poco caso, y su Vínculo baja rápido si no estás. A cambio, su Ánimo cae muy lento y nunca se vuelve feral: en ese caso se va lúcido. Con Empatía baja y hambre, la cría puede convertirse en comida. Aceptación típica de "¿Y si explorás?": ~44%.
**Riesgo principal:** que se vaya por su cuenta. Morir, casi nunca muere.
**Final natural:** Hogar Permanente. Es el que mejor representa "ya no te necesita".
**Final difícil:** El Nombre y Guía de la Tribu.
**Idle:** repara el refugio, come de forma ordenada y cuenta las provisiones.
**Cómo aprende:** casi no aprende de demos (×0.85, y su posesión dura 8 s menos), pero cada práctica le rinde ×1.25 y los fallos ×1.5. El pánico no le borra nada. Enseñarle es mostrarle una vez y después dejarlo equivocarse.

*(Las aceptaciones típicas se calcularon con Curiosidad (estado) 60, Vínculo 50 y Miedo 20.)*

---

## 3. Reglas comunes de los eventos

**Puntaje.** Mide qué tan buena o mala es la experiencia para el personaje y para la historia. No es dificultad. Se usa para tres cosas: el Narrador, la crónica de la partida y la carta de despedida.

**Narrador.** El sistema evalúa los disparadores cada pocos segundos y deja al menos 2 horas de juego entre eventos que no sean finales. Si el promedio de los últimos 3 eventos es menor que 4, favorece los eventos de puntaje alto, para que no se encadenen tres desgracias. Si el promedio es mayor que 7, favorece los de puntaje bajo. Es lo mismo que hace el storyteller de RimWorld.

**Memorias.** Cada evento guarda su id como memoria con el día en que ocurrió. Algunas reacciones guardan memorias extra (`fuego_propio`, `miedo_hielo`, etc.). Las memorias son disparadores de otros eventos, y así se forman las cadenas. Las memorias traumáticas son `abandonado`, `miedo_hielo`, `cazado`, `bayas_ceniza_venenosas` y `robo_extrano`.

**Ventana de rescate.** Los finales trágicos (0) no ocurren de golpe. Primero se abre una ventana en tiempo real (por ejemplo, 60 s) durante la cual el jugador puede revertirlos. Los Resilientes tienen una ventana 1.5 veces más larga.

**Finales.** Los eventos de puntaje 0 y 10 son terminales. Si coinciden en el mismo instante, gana el de puntaje 10.

---

## 4. Los 20 eventos

| # | Evento | Puntaje | Tipo |
|---|---|---|---|
| 1 | Último Abrazo a la Luz | 0 | Final |
| 2 | Ojos Salvajes | 0 | Final |
| 3 | La Rendición | 0 | Final |
| 4 | Bayas de Ceniza | 1 | — |
| 5 | La Llama se Apaga | 2 | Repetible |
| 6 | Cazado | 2 | Decisión moral, repetible |
| 7 | Grieta en el Hielo | 3 | Repetible |
| 8 | El Silencio del Dios | 3 | Repetible |
| 9 | El Extraño Herido | 4 | Decisión moral |
| 10 | Pesadilla del Hogar | 4 | Repetible |
| 11 | Tormenta de Ceniza | 5 | Repetible |
| 12 | Ruinas Ancestrales | 5 | Una vez por bioma |
| 13 | Primer Fuego Propio | 6 | Lo dispara el aprendizaje |
| 14 | La Cría | 7 | — |
| 15 | Noche de Estrellas | 7 | Repetible |
| 16 | El Nombre | 8 | — |
| 17 | Ecos de la Tribu | 9 | — |
| 18 | Hogar Permanente | 10 | Final |
| 19 | La Carta de Despedida | 10 | Final |
| 20 | Guía de la Tribu | 10 | Final, decisión moral |
| 21 | La Lluvia | 3 | Clima, repetible |
| 22 | Lo Hizo Solo | 8 | Aprendizaje, uno por habilidad |
| 23 | Lo Olvidó | 2 | Aprendizaje, repetible |
| 24 | Te Imita en Todo | 6 | Aprendizaje |
| 25 | Le Enseña a Otro | 9 | Aprendizaje |

---

### 1. Último Abrazo a la Luz — Puntaje 0 · Final
`ultimo_abrazo`

**Disparadores:** Saciedad o Energía en 0 durante 8 horas de juego · Noche · Tiene la antorcha que le diste (memoria `antorcha_regalada`) · Vínculo ≥ 40.
*(Si no tiene tu antorcha o el Vínculo es bajo, muere igual, pero de forma genérica y sin esta escena.)*

**Descripción:** se recuesta contra una roca con la antorcha que cayó del cielo apretada contra el pecho y mira hacia arriba, hacia vos. La llama se achica al ritmo de su respiración. La cámara sube despacio hasta que solo queda un punto de luz en el mapa, y después ni eso.

**Reacción según personalidad:**
- **Leal:** te habla. Su último gesto es levantar la antorcha hacia la cámara, como si te la devolviera.
- **Valiente:** se sienta erguido, de cara a la oscuridad, con la antorcha clavada en el suelo como un estandarte.
- **Miedoso:** se hace un ovillo alrededor de la llama y tiembla hasta quedarse quieto.
- **Resiliente:** intenta levantarse dos o tres veces antes de caer. La ventana de rescate dura un 50% más.
- **Empático:** si tiene una cría, la deja junto al fuego y la empuja con la mano para que se vaya.

**Cómo puede influir el jugador:** tiene una ventana de 60 s reales (90 s si es Resiliente). Alterar Mundo puede hacer crecer bayas a menos de 3 m, lo que salva al personaje si la causa es el hambre. Recuerdo/Calmar da +10 de Energía y gana tiempo si la causa es el agotamiento. Si la ventana se cierra, el final no se puede evitar.

---

### 2. Ojos Salvajes — Puntaje 0 · Final
`ojos_salvajes`

**Disparadores:** Vínculo < 10 · Miedo > 70 en algún momento de cada noche, durante 3 noches seguidas (5 si es Leal) · Ningún Recuerdo/Calmar en las últimas 72 horas de juego.
*Fase de aviso:* con Vínculo < 20 el personaje entra en estado "Distante". Deja de mirar al cielo, ya no nombra las cosas y tus susurros le llegan como ruido de fondo (con distorsión de audio).

**Descripción:** deja de hablar. A veces camina en cuatro patas, come crudo y apaga el fuego a pisotones porque la luz le molesta. Al final mira directo a la cámara, no te reconoce y se mete en el bosque. Después de ese momento, la cámara ya no puede seguirlo.

**Reacción según personalidad:**
- **Valiente:** se vuelve depredador. Caza de forma agresiva y marca territorio.
- **Miedoso:** se vuelve presa. Se esconde en huecos y solo sale de noche.
- **Curioso:** la curiosidad es lo último que pierde. Sigue juntando objetos brillantes en un nido sin saber para qué. Si en la fase Distante le tirás un Objeto Divino y lo agarra, gana 15 puntos de Vínculo: es su última oportunidad.
- **Leal:** resiste más, porque necesita 5 noches en lugar de 3.
- **Resiliente:** no se vuelve feral. Se va con plena lucidez, mirándote, y elige no volver. Es un final distinto con el mismo puntaje.

**Cómo puede influir el jugador:** solo durante la fase Distante. Recuerdo/Calmar es la única influencia que siempre se acepta, aunque con la mitad de efecto, y además corta el contador de 72 horas. Con un Curioso funciona también el Objeto Divino. Los susurros prácticamente no sirven por la penalización de ×0.25.

---

### 3. La Rendición — Puntaje 0 · Final
`rendicion`

**Disparadores:** Ánimo < 15 de forma continua durante 48 horas de juego (24 si la Resiliencia es < 30, 96 si el personaje es Resiliente).
*Estado previo "Rendido":* empieza en cuanto el Ánimo baja de 15 y se puede revertir.

**Descripción:** deja de buscar comida y de mantener el fuego. Se sienta a mirar el horizonte. Tus susurros se aceptan, porque asiente, pero no se mueve. Si el estado se sostiene, se deja estar en la nieve o en la ceniza, y el juego termina con la cámara alejándose de una figura sentada.

**Reacción según personalidad:**
- **Resiliente:** se rinde en ciclos: intenta, abandona, vuelve a intentar. Tiene la ventana más larga del juego.
- **Leal:** te reprocha en voz alta: "¿Para qué, si vos no…?". Con Vínculo > 50, un solo Recuerdo lo saca del pozo.
- **Empático:** solo se levanta por otros. Si hay una cría o el extraño cerca, se recupera para cuidarlos.
- **Miedoso:** se encierra en el refugio y no sale más. Muere de hambre adentro.

**Cómo puede influir el jugador:** Recuerdo/Calmar da +15 de Ánimo mientras está Rendido. Alterar Mundo puede despejar el cielo y hacer crecer flores o bayas frente a él: cada pequeño acto de belleza da +5 de Ánimo, hasta 3 veces. Los susurros no sirven en este estado.

---

### 4. Bayas de Ceniza — Puntaje 1
`bayas_ceniza`

**Disparadores:** Saciedad < 15 (35 si es Curioso, 8 si es Miedoso) · Hay bayas desconocidas cerca · No hay bayas conocidas a menos de 30 m · Nunca se intoxicó antes · *v0.8:* no tiene Identificar Bayas en Practicando o más.

**Descripción:** está desesperado y come unas bayas grises que no había probado nunca. Se intoxica: Energía −40, Saciedad +10, vómitos y movimiento −30% durante medio día. Guarda la memoria `bayas_ceniza_venenosas`, no vuelve a comerlas y las bayas desconocidas le dan asco por un tiempo.

**Reacción según personalidad:**
- **Curioso:** las prueba antes de estar desesperado, y las huele y aplasta antes de comer. Si sobrevive, aprende a distinguir bayas (memoria `bayas_seguras`, que desbloquea comida nueva en los demás biomas).
- **Miedoso:** espera hasta el límite, come una sola y la escupe. Recibe la mitad del daño y la mitad de la comida.
- **Valiente:** come a puñados y recibe el doble de daño.
- **Resiliente:** se recupera en la mitad del tiempo.

**Cómo puede influir el jugador:** un susurro de "Evitar" antes de que coma, aunque cuanto más hambre tiene, menos caso hace. Alterar Mundo puede hacer crecer bayas conocidas cerca, lo que evita el evento por completo. Un Recuerdo durante la intoxicación reduce su duración a la mitad.

---

### 5. La Llama se Apaga — Puntaje 2 · Repetible (cooldown 24 h)
`llama_apaga`

**Disparadores:** Noche · Lluvia, Tormenta o Ventisca · Tiene antorcha, o está junto a un fuego **sin techo** (v0.8) · No está en un refugio.

**Descripción:** una ráfaga apaga la llama y todo queda a oscuras, salvo por los relámpagos. Miedo +35 de inmediato, y esa noche la Energía no se recupera.

**Reacción según personalidad:**
- **Valiente:** intenta reencenderla a mano. Tiene un 30% de éxito, que llega al 70% si tiene `fuego_propio`.
- **Miedoso:** corre sin rumbo en la oscuridad. Puede caer en una Grieta y, en las Cumbres, corre riesgo de hipotermia.
- **Leal:** se queda quieto mirando al cielo, esperando una señal tuya. Si no llega en 20 s, pierde 5 de Vínculo.
- **Curioso:** se distrae mirando los relámpagos. El Miedo solo sube +20 en lugar de +35.

**Cómo puede influir el jugador:** Objeto Divino (una antorcha nueva), Alterar Mundo (calmar el clima) o Recuerdo/Calmar (−30 de Miedo). Si el personaje es Leal, cualquier influencia dentro de los 20 s cuenta como "señal" y le da +5 de Vínculo.

---

### 6. Cazado — Puntaje 2 · Decisión Moral · Repetible (cooldown 48 h)
`cazado`

**Disparadores:** Bosque Ancestral o Baldío de Ceniza · Noche o niebla · Hay una criatura hostil a menos de 25 m. *Es el doble de probable si la Energía es < 50.*

**Descripción:** algo lo sigue. Se oyen pasos que se detienen cuando él se detiene y aparecen ojos en la niebla. Hay una persecución corta, y en el momento crítico el juego se pausa para la Decisión Moral:
- **Atacar:** se tira contra la Valentía. Si gana, obtiene la memoria `vencio_bestia` y +5 de Valentía permanente. Si pierde, queda herido (Energía −50 y cojea).
- **Curar:** la criatura tiene una trampa vieja clavada. Con Empatía > 50, el personaje se la saca y la bestia lo acompaña durante 2 días. Con Empatía menor, la bestia ataca igual.
- **Huir:** se tira contra la Energía. Si gana, escapa con Miedo +20. Si pierde, escapa pero suelta un objeto.

**Reacción según personalidad (antes de que decidas):**
- **Valiente:** se planta y enfrenta. Si elegís Huir, obedece, pero paga disonancia y te mira con reproche.
- **Miedoso:** sale corriendo de entrada, antes de la pausa. Atacar tiene un 30% menos de éxito.
- **Empático:** es el primero en notar la trampa, y la opción Curar aparece resaltada con su probabilidad real.
- **Curioso:** se queda mirando demasiado, y la criatura llega a la pausa 5 m más cerca.

**Cómo puede influir el jugador:** la Decisión Moral. Antes de la pausa, Alterar Mundo puede espesar la niebla para ayudarlo a huir. Un Objeto Divino (antorcha) asusta a la criatura y cancela el evento, porque las bestias le temen al fuego.

---

### 7. Grieta en el Hielo — Puntaje 3 · Repetible (cooldown 48 h)
`grieta_hielo`

**Disparadores:** Cumbres Heladas · Está caminando sobre un lago o glaciar congelado · Curiosidad (estado) > 60 o está huyendo. *Es el doble de probable de noche o con ventisca.*

**Descripción:** el hielo cede y se hunde hasta la cintura en agua helada. Logra salir, pero pierde un objeto al azar (la antorcha tiene prioridad), pierde 30 de Energía y tiembla, con −20% de movimiento hasta que se calienta en un fuego.

**Reacción según personalidad:**
- **Valiente:** sale solo y rápido, sin pánico. Miedo +10.
- **Miedoso:** se paraliza durante unos segundos y recibe Miedo +40. Guarda la memoria `miedo_hielo`: desde entonces evita los lagos congelados y elige rutas más largas.
- **Curioso:** bajo el hielo ve un brillo. Después de salir, quiere volver. Es una pista hacia el fragmento de mapa de las Cumbres.
- **Resiliente:** se ríe de sí mismo y el Ánimo no se ve afectado.

**Cómo puede influir el jugador:** en prevención, un susurro de "Evitar" ("por el borde") cuando lo ves acercarse, o Alterar Mundo para engrosar el hielo. Después de la caída, un Objeto Divino (antorcha, para calentarse) o un Recuerdo.

---

### 8. El Silencio del Dios — Puntaje 3 · Repetible (cooldown 48 h)
`silencio_dios`

**Disparadores:** pasaron 24 horas de juego sin ninguna influencia aceptada · Vínculo > 30 · Miedo < 50 (es un momento tranquilo).

**Descripción:** se detiene, mira al cielo y te llama. Amontona piedras y dibuja un símbolo en la tierra o en la nieve. Si respondés con cualquier influencia antes de 40 s, gana +10 de Vínculo. Si no respondés, pierde 15 de Vínculo y guarda la memoria `abandonado`.

**Reacción según personalidad:**
- **Leal:** te llama varias veces y se angustia (Ánimo −10 si no respondés). Esa noche se queda esperando junto al fuego.
- **Resiliente:** se encoge de hombros. Solo pierde 5 de Vínculo y guarda la memoria `autosuficiente`.
- **Curioso:** sale a buscar dónde estás y trepa a lugares altos, con algo de riesgo de caída.
- **Empático:** te deja una ofrenda de bayas en un altar de piedras.

**Cómo puede influir el jugador:** cualquier influencia aceptada cuenta como respuesta, y un Recuerdo vale +15 en lugar de +10. **Nota de diseño:** ignorarlo a propósito es una estrategia válida si buscás Hogar Permanente. Este evento le pregunta al jugador qué relación quiere tener.

---

### 9. El Extraño Herido — Puntaje 4 · Decisión Moral · Una vez
`extrano_herido`

**Disparadores:** Día ≥ 4 · Saciedad > 30 (no está en crisis) · Hay un punto de aparición de extraño cerca.

**Descripción:** junto al camino hay otro superviviente herido y desconfiado, con comida en el bolso. El juego se pausa para la Decisión Moral:
- **Curar:** comparte comida (Saciedad −20) y pierde medio día. Obtiene la memoria `salvo_extrano`, que es requisito para Ecos de la Tribu y para Guía de la Tribu, y +3 de Empatía permanente.
- **Atacar (robarle):** Saciedad +40 y un objeto. Obtiene la memoria `robo_extrano`, −5 de Empatía permanente y Ánimo −10 (−25 si es Empático). Se bloquea el final Guía de la Tribu.
- **Huir (ignorarlo):** obtiene la memoria `ignoro_extrano`. Unos días después puede encontrar el cuerpo, con Ánimo −10 si es Empático.

**Reacción según personalidad:**
- **Empático:** corre a ayudar antes de que decidas. Si elegís Atacar y tiene Empatía ≥ 85, se niega.
- **Miedoso:** se esconde a mirar desde lejos. Huir no le genera disonancia.
- **Valiente:** se acerca directo, y si elegís Atacar el éxito está garantizado.
- **Leal:** hace lo que elijas sin dudar, pero lo recuerda: la memoria tiene más peso en su carta de despedida.

**Cómo puede influir el jugador:** la Decisión Moral. Antes de la pausa, un susurro de "Acercarse" o de "Evitar" cambia la distancia desde la que decide, y con eso cambian las opciones visibles.

---

### 10. Pesadilla del Hogar — Puntaje 4 · Repetible (cooldown 24 h)
`pesadilla`

**Disparadores:** está durmiendo · Tenía Miedo > 50 al acostarse · Tiene al menos una memoria traumática.

**Descripción:** se despierta gritando. En el sueño se mezclaban el hogar perdido y lo que le pasó acá. Esa noche la Energía solo se recupera al 50%. A cambio, el diario desbloquea un fragmento de su pasado.

**Reacción según personalidad:**
- **Miedoso:** no vuelve a dormir y se queda mirando el fuego. Miedo +15.
- **Resiliente:** se da vuelta y sigue durmiendo. Recupera la Energía al 80%.
- **Leal:** te busca en el cielo. Si mandás un Recuerdo dentro de los 15 s, la pesadilla se transforma en un sueño bueno (Ánimo +15 y Energía al 100%).
- **Curioso:** al despertar, quiere ir hacia el lugar del sueño. Gana un objetivo autónomo de exploración en una dirección que a veces lleva a unas Ruinas.

**Cómo puede influir el jugador:** Recuerdo/Calmar transforma la pesadilla con cualquier personalidad, aunque solo el Leal tiene la versión completa. Alterar Mundo puede traer una lluvia suave, cuyo sonido calmante resta 10 de Miedo.

---

### 11. Tormenta de Ceniza — Puntaje 5 · Repetible (cooldown 48 h)
`tormenta_ceniza`

**Disparadores:** Baldío de Ceniza · Día ≥ 3 · Es de día y el cielo está despejado. *El Narrador elige el momento, y este evento es el que inicia la tormenta, con un aviso de 60 s en el que el cielo se pone rojo.*

**Descripción:** la visibilidad baja a 5 m y estar a la intemperie cuesta 2 de Energía por segundo. Tiene que encontrar o improvisar un refugio. Si lo logra, la tormenta pasa y sale reforzado (Ánimo +10 y la memoria `sobrevivio_tormenta`).

**Reacción según personalidad:**
- **Valiente:** se tapa la cara y sigue avanzando hacia su objetivo.
- **Miedoso:** se mete en el primer hueco que encuentra. A veces es una cueva con una criatura adentro, y eso dispara Cazado.
- **Curioso:** ve formas en la ceniza. El viento destapa unas Ruinas Ancestrales que quedan reveladas en el mapa.
- **Resiliente:** se tira al suelo detrás de una roca y espera, sin perder Ánimo.

**Cómo puede influir el jugador:** Alterar Mundo (acortar la tormenta un 50%), Objeto Divino (una antorcha que hace de faro y que el personaje sigue) o un susurro de "Refugiarse" (que se acepta mejor cuanto más miedo tiene).

---

### 12. Ruinas Ancestrales — Puntaje 5 · Una vez por bioma
`ruinas`

**Disparadores:** Curiosidad (estado) > 50 · Miedo < 60 · Hay ruinas a menos de 40 m · Es de día · Todavía no tiene el fragmento de mapa de ese bioma.

**Descripción:** encuentra los restos de una civilización: tallas, estatuas sin cara y escaleras que no llevan a ninguna parte. Adentro hay un fragmento de mapa grabado en piedra (memoria `fragmento_mapa_<bioma>`). La Curiosidad (estado) baja a 0 porque queda satisfecha, y el Ánimo sube +10. Hay un riesgo de derrumbe.

**Reacción según personalidad:**
- **Curioso:** recorre todo y encuentra el fragmento más un objeto extra. Tarda el doble, así que puede quedarse ahí cuando cae la noche.
- **Miedoso:** no entra y se queda mirando desde la puerta. Necesita un susurro o una antorcha para entrar.
- **Valiente:** entra directo, sin mirar. Por el apuro tiene un 20% de probabilidad de derrumbe (Energía −20).
- **Leal:** te pregunta en voz alta: "¿Vos conocías esto?". Si mandás un Recuerdo mientras está adentro, interpreta las tallas como un mensaje tuyo (+10 de Vínculo).

**Cómo puede influir el jugador:** Objeto Divino (una antorcha en la entrada, que hace entrar a los Miedosos) o un susurro de "Explorar".

---

### 13. Primer Fuego Propio — Puntaje 6 · Se repite hasta que lo logra
`primer_fuego` → al tener éxito guarda `fuego_propio`

**Disparadores (v0.8):** lo dispara el sistema de aprendizaje la primera vez que **Hacer Fuego** sale bien sin posesión ni susurro. Para eso la habilidad tiene que estar al menos en Practicando, porque recién ahí la usa sola. Si durante ese primer intento recoge una antorcha divina, el intento se interrumpe y no cuenta.

**Descripción:** por primera vez, sin tu ayuda, frota piedras sobre la leña y consigue hacer fuego. Obtiene la memoria permanente `fuego_propio`: desde ahora puede encender fuego solo. Ánimo +20 y Miedo −30. Es el primer paso para dejar de necesitarte.

**Reacción según personalidad:**
- **Resiliente:** lo consigue al primer intento y suelta un grito de triunfo.
- **Valiente:** lo consigue, pero se quema la mano (Energía −10) y se pone orgulloso.
- **Leal:** mira al cielo para mostrártelo. Si respondés con cualquier influencia, gana +10 de Vínculo porque compartiste el logro.
- **Miedoso:** necesita tres intentos y cada fallo le suma 10 de Miedo. Si falla los tres, el evento vuelve a intentarse otra noche.

**Cómo puede influir el jugador:** acá está la paradoja: tirarle una antorcha **cancela** el evento y lo pospone 24 horas. La mejor ayuda es no ayudar. Alterar Mundo puede secar la leña (+50% de éxito) y un susurro de "probá con las piedras" también suma.

---

### 14. La Cría — Puntaje 7 · Una vez
`la_cria`

**Disparadores:** Bosque Ancestral o Cumbres Heladas · Es de día · Miedo < 50 · Hay una cría huérfana cerca. *Aparece más seguido si en Cazado se eligió Atacar y la bestia muerta era la madre.*

**Descripción:** un animalito solo, que tiembla. Si lo adopta (memoria `cria_adoptada`), lo sigue a todas partes: el Miedo nocturno sube un 20% menos, pero la Saciedad baja un 25% más rápido porque comparten la comida. Si la cría muere, Ánimo −30.

**Reacción según personalidad:**
- **Empático:** la adopta sin pensarlo y le pone nombre.
- **Curioso:** la sigue hasta su madriguera y descubre un recurso escondido.
- **Miedoso:** la adopta para no estar solo y se apega demasiado. Si la cría muere, Ánimo −50.
- **Resiliente:** evalúa si le sirve. Si además tiene Empatía < 30 y Saciedad < 30, la caza.

**Cómo puede influir el jugador:** un susurro de "Acercarse" (llevala) o de "Evitar" (dejala), que es lo que puede salvar a la cría con el Superviviente. Alterar Mundo puede hacer crecer más bayas para que alcance para los dos.

---

### 15. Noche de Estrellas — Puntaje 7 · Repetible (cooldown 48 h)
`noche_estrellas`

**Disparadores:** Noche · Cielo despejado · Miedo < 20 · Saciedad > 50 · Energía > 40 · Está junto a un fuego.

**Descripción:** se acuesta a mirar el cielo y habla en voz alta. Te cuenta cómo era su vida antes. Ánimo +15, Vínculo +10 y Curiosidad (estado) +20, y el diario desbloquea una entrada nueva sobre su historia.

**Reacción según personalidad:**
- **Leal:** te habla directamente a vos y dibuja con el dedo una constelación con tu "forma".
- **Curioso:** les pone nombre a las estrellas y aprende a orientarse con ellas (memoria `lee_estrellas`). Con esto, La Carta le pide 2 fragmentos en lugar de 3.
- **Empático:** si tiene una cría o un compañero, lo abraza y le cuenta la historia a él.
- **Resiliente:** se queda en silencio y sonríe. Ánimo +25 en lugar de +15, pero el Vínculo solo sube +5.

**Cómo puede influir el jugador:** Alterar Mundo puede traer una lluvia de estrellas fugaces (+5 de Vínculo extra), y un Recuerdo durante la escena duplica todos los efectos. No hacer nada también es una buena opción.

---

### 16. El Nombre — Puntaje 8 · Una vez
`el_nombre`

**Disparadores:** Vínculo ≥ 80 durante 48 horas de juego seguidas · Aceptó al menos 3 Recuerdos en la partida · No guardó la memoria `abandonado` en los últimos 3 días.

**Descripción:** te pone un nombre y lo talla en una piedra. Desde ahí, la aceptación de tus susurros tiene +15 permanente, y cuando siente miedo te nombra en voz alta y se calma solo (−10 de Miedo, sin que intervengas).

**Reacción según personalidad (el tipo de nombre):**
- **Leal:** un nombre devocional, como "El que cuida". Construye un altar.
- **Curioso:** un nombre en forma de pregunta, como "Quién-sos". Sigue intentando averiguar qué sos.
- **Valiente:** un nombre de compañero, como "Hermano de fuego".
- **Resiliente:** un nombre seco o irónico, como "El que llega tarde". El vínculo es real, pero no hay altar.
- **Empático:** un nombre suave, y deja ofrendas de comida junto a la piedra.

**Cómo puede influir el jugador:** responder con un Recuerdo durante la escena la completa. Si lo ignorás, el nombre queda igual, pero el bonus es +5 en lugar de +15.

---

### 17. Ecos de la Tribu — Puntaje 9 · Una vez
`ecos_tribu`

**Disparadores:** tiene la memoria `salvo_extrano` · Día ≥ 10 · Pasaron al menos 4 días desde que salvó al extraño · Ánimo > 50.

**Descripción:** el extraño al que curó vuelve recuperado, acompañado de dos o tres personas más. Lo reconocen y le ofrecen unirse por un tiempo. Ánimo +25 y comparten recursos. Si durante la convivencia sobreviven juntos a una amenaza (Cazado, Tormenta), obtiene la memoria `defendio_tribu`. Este evento abre el camino hacia el final Guía.

**Reacción según personalidad:**
- **Empático:** se integra de inmediato y cura a otro herido del grupo.
- **Valiente:** se ofrece a hacer guardia esa noche. Es la forma más directa de conseguir `defendio_tribu`.
- **Curioso:** intercambia historias y consigue un fragmento de mapa, como alternativa a las Ruinas.
- **Miedoso:** se mantiene a distancia y necesita dos encuentros más para confiar.
- **Leal:** les habla de vos. Si el Vínculo es > 70, la tribu empieza a dejar ofrendas al cielo y aparece una cultura alrededor tuyo.

**Cómo puede influir el jugador:** un susurro de "quedate con ellos" ("Acercarse"), Alterar Mundo para dar buena caza y bayas al grupo, o un Recuerdo para calmar a un Miedoso.

---

### 18. Hogar Permanente — Puntaje 10 · Final
`hogar_permanente`

**Disparadores (v0.8):** Hogar al 100% (ver sección 8: las 4 habilidades de supervivencia Dominadas, campamento en nivel 3, Calmarse Solo Dominado y 72 horas sin ayuda directa) · Tiene `fuego_propio` · Promedio de Saciedad y de Energía > 60 en los últimos 3 días · Día ≥ 15.

**Descripción:** termina el refugio. Un amanecer, mira al cielo por última vez, más largo que de costumbre, y después deja de buscarte. Tus susurros le siguen llegando, pero ya no los necesita. Es el final agridulce y perfecto: ganaste porque dejaste de ser necesario.

**Reacción según personalidad (epílogo):**
- **Resiliente:** es el final más natural para él. Ni siquiera te mira. El epílogo lo muestra envejeciendo en su valle.
- **Leal:** talla tu símbolo en la puerta y dice que el fuego permanente es "tuyo".
- **Empático:** el hogar se convierte en refugio para animales.
- **Curioso:** lo transforma en una base para expediciones, con mapas en las paredes.
- **Valiente:** levanta una empalizada y se convierte en el protector del valle.

**Cómo puede influir el jugador:** no se puede forzar. Solo se puede no interferir. Cualquier Objeto Divino, Recuerdo o **Posesión** reinicia el contador de 72 horas. Los susurros sí están permitidos.

---

### 19. La Carta de Despedida — Puntaje 10 · Final
`carta_despedida`

**Disparadores:** tiene 3 fragmentos de mapa (o 2 más `lee_estrellas`) · Llega al paso de montaña en las Cumbres Heladas.
*Variante:* si el Vínculo es < 50, se va sin dejar carta. Es "La Salida Silenciosa" y cuenta como puntaje 8.

**Descripción:** encuentra la salida, una puerta de piedra en la cima. Antes de cruzar, escribe una carta para vos en corteza o en piedra. La carta se arma con su memoria: menciona los 3 momentos más fuertes de la partida (los eventos más alejados de 5 en la crónica) y te agradece o te reprocha, según lo que hayas hecho. Después cruza.

**Reacción según personalidad (el tono de la carta):**
- **Leal:** una carta larga y agradecida. Deja tu antorcha en el umbral.
- **Resiliente:** tres líneas secas, del estilo de "Gracias por las bayas."
- **Curioso:** una carta llena de preguntas sobre qué sos.
- **Empático:** te pide que cuides lo que deja atrás, sea la cría o la tribu.
- **Valiente:** cruza sin mirar atrás y deja la carta clavada en una lanza.

**Cómo puede influir el jugador:** un Recuerdo en el umbral agrega una línea final a la carta, siempre dirigida a vos. Durante la partida, el jugador decide qué recuerdos va a tener la carta.

---

### 20. Guía de la Tribu — Puntaje 10 · Final · Decisión Moral
`guia_tribu`

**Disparadores:** ocurrió Ecos de la Tribu · Tiene `salvo_extrano` y `defendio_tribu` · No tiene `robo_extrano` · Empatía ≥ 50 o Valentía ≥ 65 · Ánimo > 60.

**Descripción:** la tribu lo elige como guía y él les enseña lo que aprendió. Cada memoria se convierte en una habilidad que transmite: el fuego, las bayas seguras, las estrellas. Ahora es él quien influye sobre otros. En el epílogo, el jugador lo ve mirar a alguien de la tribu y hacer el mismo gesto que vos le hacías desde el cielo.

**Decisión Moral final (la filosofía que transmite):**
- **Atacar:** una tribu guerrera.
- **Curar:** una tribu sanadora.
- **Huir:** una tribu nómada.

**Reacción según personalidad (qué tipo de guía es):**
- **Empático:** curandero.
- **Valiente:** protector y cazador.
- **Leal:** sacerdote. Funda un culto a vos que es benévolo o temeroso según cuántas veces elegiste Atacar en la partida, al estilo Black & White.
- **Curioso:** explorador, que lleva a la tribu a tierras nuevas.
- **Resiliente:** un guía reacio, que rechaza el puesto dos veces antes de aceptar.

**Cómo puede influir el jugador:** la Decisión Moral, que define el legado. Elegir contra la personalidad también genera disonancia, porque se nota en el epílogo.

---

### 21. La Lluvia — Puntaje 3 · Clima · Repetible (cooldown 30 h)
`la_lluvia`

**Disparadores:** no está lloviendo ni hay pronóstico · Bioma distinto de Baldío de Ceniza (allá está la Tormenta de Ceniza) · Día ≥ 2. En las Cumbres es aguanieve.

**Descripción:** el Narrador anuncia la lluvia con 1.5 horas de juego de pronóstico: se levanta viento, vuelan hojas y el cielo se pone gris. Después llueve entre 3 y 6 horas con intensidad variable. Si lo agarra sin refugio: Miedo +30 de golpe, se moja (Humedad +30/h a intensidad máxima), pierde Energía por el frío y, si pasa 4 horas empapado, puede enfermarse. Las fogatas sin techo se apagan en unos 25 s reales.
**Con aprendizaje:** si domina Refugio de Palos, usa el pronóstico para construir o ir a su refugio *antes* de mojarse. Si lo tiene en Practicando, recién reacciona cuando ya llueve. Si domina Juntar Agua de Lluvia, la lluvia deja de asustarlo (Miedo +10 en lugar de +30, y Ánimo +5).

**Reacción según personalidad:**
- **Valiente:** aguanta bajo el agua y sigue con lo que estaba haciendo hasta empaparse.
- **Miedoso:** se esconde en el primer hueco que encuentra y tirita aunque esté seco.
- **Curioso:** se queda mirando los charcos y las gotas, y pierde tiempo antes de refugiarse.
- **Resiliente:** sigue trabajando y se seca después junto al fuego.

**Cómo puede influir el jugador:** Alterar Mundo puede correr la lluvia (15 de Fe). Un susurro de "Refugiarse" se acepta mejor cuanto más mojado y asustado está. Lo más valioso es usar el pronóstico para una Posesión Pedagógica y enseñarle el refugio *justo antes* de que llueva: es la demostración con más sentido para él.

---

### 22. Lo Hizo Solo — Puntaje 8 · Aprendizaje · Uno por habilidad
`lo_hizo_solo`

**Disparadores:** la primera vez que una habilidad sale bien por iniciativa propia, sin posesión ni susurro (Hacer Fuego dispara el evento 13 en su lugar).

**Descripción:** termina lo que hizo y se queda mirándolo. Es el momento que el juego tiene que celebrar: cámara cercana, música mínima y una entrada en el diario ("Día 6: hizo el refugio solo. No lo miré.").

**Reacción según personalidad:**
- **Leal:** te lo muestra orgulloso, mirando al cielo.
- **Resiliente:** asiente para sí mismo y sigue.
- **Curioso:** lo mira desde todos los ángulos y lo toca.
- **Valiente:** suelta un grito de triunfo.
- **Empático:** se lo muestra a la cría, si la tiene.

**Cómo puede influir el jugador:** responder con cualquier influencia da +5 de Vínculo con Leales. No responder no tiene costo: es su logro.

---

### 23. Lo Olvidó — Puntaje 2 · Aprendizaje · Repetible
`lo_olvido`

**Disparadores:** una habilidad que había llegado a Imitando cae a No Aprendido, por falta de práctica o por un ataque de pánico.

**Descripción:** intenta hacer algo que sabía y no le sale. Se mira las manos. El conocimiento no baja de 15 (el residuo), así que volver a enseñarle es más rápido (×1.3), pero el golpe emocional existe.

**Reacción según personalidad:**
- **Miedoso:** se mira las manos asustado y su Miedo sube.
- **Leal:** te busca para que se lo vuelvas a mostrar (si hay Fe, es el mejor momento para una posesión).
- **Resiliente:** lo intenta igual y practica fallando, que en él rinde ×1.5.
- **Curioso:** lo "reinventa" mal y genera una manía nueva.

**Cómo puede influir el jugador:** Posesión Pedagógica para reenseñar o un susurro de "Practicá" si quedó algo de conocimiento.

---

### 24. Te Imita en Todo — Puntaje 6 · Aprendizaje · Una vez
`te_imita`

**Disparadores:** la primera vez que, en estado Imitando, reproduce un gesto de más de tu demostración: un rodeo, golpes de más o una pausa larga.

**Descripción:** el jugador ve su propia torpeza devuelta. Si rodeaste una roca sin necesidad, él también la rodea. Si martillaste de más, él también. Es gracioso y a la vez tierno: el personaje no sabe distinguir lo importante de lo accesorio. En el diario: "Hace todo como vos. Todo."

**Reacción según personalidad:**
- **Leal:** copia exacta y reverente.
- **Curioso:** exagera el gesto como si fuera parte del ritual.
- **Miedoso:** lo copia temblando, por miedo a hacerlo mal.
- **Resiliente:** lo hace a su manera y la manía dura menos.

**Cómo puede influir el jugador:** hacer una demostración más limpia reemplaza la plantilla (se usa la mejor de las 3 guardadas). La "firma del maestro", un gesto tuyo que conserva para siempre, no se borra.

---

### 25. Le Enseña a Otro — Puntaje 9 · Aprendizaje · Una vez
`ensena_a_otro`

**Disparadores:** tiene la cría o conoció a la tribu · 3 o más habilidades Dominadas · Empatía ≥ 50 o Lealtad ≥ 65 · Miedo < 40 · Es de día.

**Descripción:** el círculo se cierra: ahora él hace con otro lo que vos hiciste con él. Le muestra a la cría o a alguien de la tribu una habilidad Dominada, con tu firma incluida (si martillabas de más, él también lo hace al enseñar). Es la antesala del final Guía de la Tribu.

**Reacción según personalidad:**
- **Empático:** enseña con paciencia y repite las veces que haga falta.
- **Leal:** enseña "como vos", copiando tus gestos a propósito.
- **Valiente:** enseña a defenderse.
- **Curioso:** enseña y aprende al mismo tiempo, y mejora la habilidad.
- **Resiliente:** enseña sin palabras, solo haciendo.

**Cómo puede influir el jugador:** ninguna. Es el único evento en el que el jugador solo mira.

---

## 5. Cómo se encadenan

Estas son las rutas principales. Sirven para testear que todos los finales se pueden alcanzar.

**Ruta del Hogar:** Primer Fuego Propio → (Silencio del Dios ignorado → `autosuficiente`) → refugio nivel 3 → 72 horas sin ayuda divina → Hogar Permanente.

**Ruta de la Salida:** Ruinas ×3 (o Ruinas ×2 + Noche de Estrellas como Curioso, o Ruinas ×2 + Ecos de la Tribu como Curioso) → paso de montaña → La Carta, o La Salida Silenciosa si el Vínculo es bajo.

**Ruta de la Tribu:** El Extraño Herido (Curar) → Ecos de la Tribu → sobrevivir a una amenaza juntos → Guía de la Tribu.

**Rutas trágicas:** Silencio del Dios repetido → fase Distante → Ojos Salvajes. Cría muerta, pesadillas o hambre → Rendido → La Rendición. Antorcha regalada + hambre o agotamiento extremo → Último Abrazo a la Luz.

**Ruta del Maestro (v0.8):** Posesión (Imitando) → susurros de "Practicá" → Practicando → lo hace solo cuando lo necesita (Lo Hizo Solo) → Dominado → Le Enseña a Otro → Guía de la Tribu, o Hogar Permanente si nunca hubo a quién enseñar.

**El Nombre** no es un final, pero modifica todos los finales: con El Nombre, la carta, el culto y el epílogo del hogar hacen referencia a tu nombre.

---

## 6. Sistema de aprendizaje por agentes

### 6.1 La idea en una frase

**Podés mostrarle, pero dominar lo tiene que hacer él.** Una demostración nunca sube una habilidad más allá del 59%. Para pasar a Practicando y Dominado, tiene que hacerla él, primero cuando se lo pedís y después cuando la necesita. Así la mecánica nueva no rompe el pilar original ("no controlás al personaje"): la posesión enseña, pero no reemplaza su autonomía.

Las tres formas de influencia quedan alineadas con las tres fases del aprendizaje:

| Fase | Conocimiento | Qué hace | Qué herramienta del jugador la empuja |
|---|---|---|---|
| No aprendido | 0–29 | No sabe hacerlo | **Posesión Pedagógica** (mostrarle) |
| Imitando | 30–59 | Solo lo hace si se lo susurrás, y copia tu demo al pie de la letra: mismo lugar, mismo recorrido, mismas manías. Si un paso le sale mal, se confunde y abandona | **Susurro "Practicá X"** (motivarlo) |
| Practicando | 60–99 | Lo hace solo cuando la necesidad es urgente (utilidad ≥ 70). Sigue la receta, elige el lugar entre tu demo y el campamento, y si falla un paso lo reintenta | **Nada.** Que lo necesite y se equivoque |
| Dominado | 100 (sigue Dominado mientras esté en 85 o más) | Lo hace de forma proactiva (utilidad ≥ 40), anticipándose: construye el refugio cuando ve el pronóstico de lluvia. Junta todo primero, elige su lugar y es más rápido | **Nada.** Ya es suyo |

### 6.2 Cada enseñanza es un agente

Cada una de las 15 habilidades es un `LearningAgent`, un objeto liviano con lo siguiente:

- **Conocimiento (0–100)** y su fase, con histéresis: llega a Dominado en 100, pero no lo pierde hasta bajar de 85, así un olvido chico no lo degrada enseguida.
- **Memoria episódica:** guarda hasta 3 demostraciones (las mejores) más la primera, que nunca se borra porque se usa en la carta de despedida ("Me enseñaste el refugio el día 3, bajo la lluvia"). Cada demostración guarda los pasos simbólicos en orden (qué hiciste, con qué, dónde y cuándo), el recorrido comprimido (como máximo 64 puntos, solo en giros o cada 1.5 m), los materiales usados, la duración y tus manías.
- **Plantilla:** la demostración de mejor calidad es la que imita en la fase Imitando.
- **Firma del maestro:** de la primera demo que tenga una manía, conserva una para siempre, aunque domine la habilidad. Si siempre le diste dos golpes de más al techo, él también lo va a hacer en el día 40. Es un detalle chico que hace que el personaje se sienta *tuyo*.
- Contadores de práctica, demos del día, última práctica y si ya lo hizo solo alguna vez.

### 6.3 Posesión Pedagógica

**Cómo se activa:** elegís en un menú radial qué querés enseñar. Solo aparecen las habilidades desbloqueadas (con los requisitos en Practicando o más) que todavía no domina. Cuesta **30 de Fe**.

**Requisitos:** Vínculo ≥ 25 ("no te deja entrar" si es menor), que no esté durmiendo y que alcance la Fe.

**Duración:** 30 s + 15 s × Vínculo/100, o sea entre 30 y 45 s. Si la Lealtad es ≤ 30, dura 8 s menos. Con Miedo > 80, la mitad.

**Controles:** WASD relativo a la cámara isométrica y **E** para interactuar con lo más cercano que sirva para el paso actual. Las acciones de mantener (inspeccionar, agacharse, sentarse, respirar, revolver, señalar) piden mantener E pulsado durante el tiempo del paso, con una barra circular. Si soltás antes, no cuenta.

**Guía visual:** en las habilidades de construcción aparece una obra fantasma (piezas translúcidas) 2.5 m adelante, o sobre el fuego o el refugio si la habilidad se ancla ahí. Cada E que coloca una pieza la vuelve real. El HUD resalta el próximo objeto útil.

**Se resiste un poco:** el personaje tiene una "deriva" proporcional a (1 − Lealtad): mientras lo movés, tira un poco hacia otro lado. A la Errante la atraen las cosas curiosas. Al Devoto casi no se le nota. Esto comunica que el cuerpo es de él y vos solo estás de visita.

**Posesión invasiva:** las dos primeras posesiones del día son gratis en lo emocional. Desde la tercera, cada una cuesta Vínculo −5 y Miedo +10: se siente usado.

**Cómo termina:** cuando se acaba el tiempo o un segundo después de completar la receta. El personaje "despierta" y se mira las manos (2.5 s). Lo que construiste queda construido; si la obra quedó a medias, él puede terminarla después practicando. Si no hiciste nada, la obra fantasma desaparece.

**El mundo no se detiene:** durante la posesión el tiempo sigue corriendo y la lluvia sigue cayendo, pero el Narrador no dispara eventos.

### 6.4 Calidad de la demostración

Cada demostración se evalúa de 0 a 1:

**Calidad = 0.6 × Completitud + 0.25 × Eficiencia + 0.15 × Claridad**

- **Completitud:** cuántas acciones de la receta hiciste. Lo que ya tenía en el inventario cuenta como recolectado. Los pasos hechos fuera de orden valen la mitad.
- **Eficiencia:** tiempo ideal de la habilidad / tiempo que tardaste.
- **Claridad:** baja con los gestos de más (E al aire, recolectar cosas que no hacían falta, golpes de más) y con el desorden (−0.15).

**Manías detectadas** (las va a copiar): `rodeo` (tu recorrido fue 1.8 veces más largo que lo necesario), `orden_invertido`, `golpes_de_mas` (2 o más golpes sobrantes en la obra) y `pausa_pensativa` (más de 4 s quieto).

### 6.5 Cuánto aprende

**Por demostración:** Ganancia = DemoGain de la habilidad × Calidad × Tasa / Dificultad × 0.8^(demos de hoy). Otros ajustes:
- ×1.3 si ya la había sabido y la olvidó (residuo).
- ×0.5 si la completitud fue menor que 0.5 (demo confusa, que además suma +5 de Miedo).
- Nunca pasa de 59.
- Una demo de calidad ≥ 0.7 da +3 de Vínculo.

**Por práctica (la hace él):** éxito = PracticeSuccessGain (8 por defecto) × (0.5 + 0.5 × calidad del resultado). Fallo = PracticeFailGain (3), ×1.5 si es Resiliente. Las dos se multiplican por Tasa / Dificultad.

**Tasa según personalidad y estado:**

| Condición | Multiplicador |
|---|---|
| Curioso (Curiosidad ≥ 65) | ×1.2 en todo |
| Leal | ×1.15 en demos |
| Lealtad ≤ 30 | ×0.85 en demos, ×1.25 en práctica |
| Empático, en habilidades Emocionales | ×1.15 |
| Valiente, en Supervivencia | ×1.1 |
| Miedoso, en Esconderse | ×1.25 |
| Valiente, en Esconderse | ×0.85 |
| Miedoso con Miedo > 50 (resto de habilidades) | ×0.85 |
| Vínculo, en demos | ×(0.6 + Vínculo/250): de 0.6 a 1.0 |
| Miedo > 60 | ×0.7 |
| Enfermo | ×0.8 |
| Energía < 20 | ×0.8 |

**Probabilidad de que le salga:** P = 0.35 + 0.6 × Conocimiento/100, con penalizaciones: Miedo por encima de 40 (−0.004 por punto), cojear (−0.1), estar enfermo (−0.1) o empapado (−0.05). Si está Dominado, P es como mínimo 0.92. La falla se reparte entre los pasos, así que un fracaso se ve en un momento concreto: se le cae el palo o la hoja se resbala.

**Calidad del resultado:** los pasos bien hechos dan entre 0.65 y 1 de calidad; los que salieron mal y se reintentaron, entre 0.3 y 0.55. Con menos de 12° × (1 − calidad), los palos quedan torcidos. **Un refugio con calidad < 0.5 gotea:** adentro se moja al 30%.

### 6.6 Olvido

- **Por falta de práctica:** cada 48 horas de juego sin practicar ni recibir una demo, pierde 10 puntos. Esto le pasa a todas las habilidades, también a las Dominadas, que por la histéresis aguantan hasta 85.
- **Por pánico:** cada vez que el Miedo cruza 80 (con un margen de 12 horas entre episodios), las habilidades en Imitando y Practicando pierden 20 puntos. Lo Dominado es memoria muscular y no se toca. **Los Resilientes son inmunes.**
- **Residuo:** una vez que llegó a Imitando, nunca baja de 15. Volver a enseñarle rinde ×1.3.
- Si una habilidad cae de Imitando a No Aprendido, se dispara el evento 23, Lo Olvidó.

### 6.7 Ejecución autónoma

Cada segundo, el sistema revisa qué habilidades en Practicando o Dominado tienen utilidad suficiente. Cada habilidad define su propia función de utilidad (de 0 a 100) según el estado y el mundo: Refugio de Palos vale 90 si llueve y no hay refugio a menos de 30 m, y 60 si el pronóstico dice que llueve en menos de 2 horas. Gana la de mayor utilidad que supere el umbral de su fase. Hay un solo ejecutor por personaje: hace una cosa a la vez y se cancela si lo poseés o si pasa algo grave.

| Fase | Dónde lo hace | Recorrido | Orden |
|---|---|---|---|
| Imitando | Exactamente donde vos lo hiciste. Busca materiales a menos de 15 m de donde vos los juntaste | Copia tus waypoints, con rodeos y todo | El tuyo, con los gestos de más (sin efecto) |
| Practicando | Donde lo hiciste o cerca del campamento, lo que esté más cerca. Busca a 30 m | Directo por NavMesh (si tu firma es el rodeo, conserva la primera mitad) | El de la receta, más tu firma |
| Dominado | Elige el lugar él mismo. Busca a 50 m | Directo, 10% más rápido | Junta todo primero y después construye, más tu firma |

En Imitando camina al 80% de su velocidad y tarda un 30% más en cada acción: se le nota que está copiando. Dominado es un 20% más rápido en cada acción.

### 6.8 Lo que tiene que comunicar el feedback

Lo central es que el jugador *vea* el aprendizaje, no solo un número subiendo:

- Encima del personaje, al empezar una habilidad, aparece un ícono con un anillo de progreso según la fase: contorno punteado (Imitando), lleno a medias (Practicando) o dorado (Dominado).
- El diario anota cada hito en primera persona del jugador: "Día 4: le mostré cómo hacer fuego. Se quemó la mano".
- Cuando cambia de fase, un sonido corto y una partícula. Cuando olvida, un sonido "hueco" y una pequeña niebla sobre el ícono.
- El momento de Lo Hizo Solo es el más importante y merece su propia cámara.

---

## 7. Lluvia y clima

**Ciclo:** el evento 21 inicia el pronóstico (1.5 horas de juego) → lluvia de 3 a 6 horas con intensidad aleatoria entre 0.45 y 1 → secado. La intensidad sube y baja en rampa durante unos 40 minutos de juego; por encima de 0.7 cuenta como Tormenta, con relámpagos.

**Efectos sobre el personaje** (están en `CharacterState.Tick`):

| Situación | Efecto |
|---|---|
| Empieza a llover y está a la intemperie | Miedo +30 (+10 y Ánimo +5 si domina Agua de Lluvia) |
| Bajo la lluvia | Humedad +30/h × intensidad, Miedo +4/h × intensidad |
| En un refugio que gotea | Humedad al 30% |
| Humedad > 50 | Energía −3/h (frío), tirita |
| Humedad > 70 durante 4 horas | Empieza a acumular riesgo de enfermarse, reducido por la Resiliencia |
| Enfermo (36 horas) | Recupera la Energía a la mitad, la Saciedad baja un 25% más rápido, Ánimo −10, aprende ×0.8 |
| Secarse | −40/h junto al fuego, −10/h en el refugio, −5/h al aire libre |

**La fogata:** sin techo, la lluvia la apaga en unos 25 s reales a intensidad máxima. La llama se achica antes de morir, sale vapor, y al final se escucha un siseo y se ve un hilo de humo. Con la habilidad Techo para la Fogata, el fuego aguanta. **Una noche de lluvia sin techo ni refugio es la situación que el jugador va a querer evitar enseñando.**

**La respuesta aprendida:** con Refugio de Palos Dominado, el personaje lee el pronóstico y construye o va al refugio antes de que llueva. Ese momento, en el que el personaje se anticipa a algo que antes lo destrozaba, es la mejor prueba de que el sistema funciona.

---

## 8. Fe y progreso del Hogar

### 8.1 Fe

Es el recurso del jugador, de 0 a 100, y empieza en 50. Se recarga a (1 + Vínculo/50) por hora de juego, o sea entre 1 y 3 por hora. Las ofrendas suman +5.

| Influencia | Costo |
|---|---|
| Susurro (cualquiera, incluido "Practicá") | 0 |
| Decisión Moral | 0 |
| Recuerdo/Calmar | 10 |
| Alterar Mundo | 15 |
| Objeto Divino (antorcha) | 20 |
| **Posesión Pedagógica** | **30** |
| Responder a Pedir Ayuda | la mitad del costo normal |

### 8.2 Hogar 0–100%

**Hogar = 35% Supervivencia + 25% Campamento + 20% Emocional + 20% Autonomía**

- **Supervivencia:** cuántas de estas 4 domina: Hacer Fuego, Refugio de Palos, Cama de Paja, Cocinar en Olla.
- **Campamento:** nivel de 0 a 3.
- **Emocional:** 100% con Calmarse Solo Dominado, 50% con Pedir Ayuda en Practicando.
- **Autonomía:** horas sin ayuda directa / 72. La ayuda directa es cualquier objeto, recuerdo o posesión. Los susurros no cuentan.

*Si en v0.7 ya calculaban el Hogar de otra forma, reemplacen esta fórmula; el resto del sistema solo usa `HomeProgress.Compute()`.*

### 8.3 Niveles del campamento

| Nivel | Se ve como | Requisitos de habilidad (en Practicando o más) |
|---|---|---|
| 0 · Choza | Un refugio de palos torcido, una fogata a cielo abierto y cosas tiradas en el suelo | — |
| 1 · Campamento | Refugio bien armado, fogata con piedras alrededor, la pila de palos ordenada y el tipi levantado | Refugio de Palos, Hacer Fuego |
| 2 · Asentamiento | La carpa médica con la cruz roja remendada, ollas colgadas, cama de paja, una soga con ropa secándose y la jaula del pájaro colgada | + Cama de Paja, Cocinar en Olla, Talar |
| 3 · RV fortificado | El RV con una empalizada de troncos, lona extendida como techo, la fogata techada, el colector de lluvia, una huerta mínima y una guirnalda de lucecitas a batería | + Techo para la Fogata, Agua de Lluvia, Talar **Dominado**, y los troncos que pida el mundo |

---

## 9. Dirección de arte: acogedor pero triste

### 9.1 Referencias y qué tomar de cada una

**Don't Starve:** contornos de tinta irregulares, siluetas legibles, una paleta desaturada con un solo color cálido (el fuego) y la sensación de que la noche es una amenaza. **Little Nightmares:** la escala (el personaje es chico y el mundo es grande), charcos de luz cálida en medio de una oscuridad opresiva y el silencio como recurso. **Project Zomboid:** la lectura isométrica clara, la vida cotidiana en los detalles (latas, mantas, cosas reparadas) y la sensación de supervivencia realista. *De Warcraft, nada.* Hay que inspirarse en el clima y los principios de estas referencias, sin copiar personajes, logos ni diseños concretos.

### 9.2 Cámara y forma

- Cámara **ortográfica** isométrica: yaw 45°, pitch entre 35° y 40°. Zoom con pocas paradas: cercano para enseñar y lejano para el mundo.
- El personaje mide alrededor de un 60% del alto de la puerta del RV, con cabeza grande y manos visibles. Tiene que parecer vulnerable.
- Los objetos del mundo son un poco más grandes de lo real (una olla le llega a la rodilla) para reforzar la escala.
- Contorno con *inverted hull* (casco invertido) de 1.5 a 2 px de grosor aparente, en un marrón casi negro, nunca negro puro.

### 9.3 Paleta

| Uso | Color |
|---|---|
| Musgo / Bosque | #4E5A3C |
| Ceniza / Baldío | #6B5E52 |
| Escarcha / Cumbres | #A9B8C2 |
| Óxido (RV, ollas) | #8A4B2E |
| Cruz de la carpa médica (el único rojo saturado del juego) | #A8322A |
| Luz de fogata | #F2B36B |
| Noche | #1E2430 |

Regla: la saturación alta queda solo para el fuego, la cruz roja y las lucecitas del nivel 3. Todo lo demás vive entre el 20% y el 45% de saturación.

### 9.4 Luz

- Luz ambiental baja. De noche, el mundo prácticamente desaparece fuera del radio de la fogata (OmniLight3D con atenuación fuerte y sombra).
- La fogata titila con dos senoides (ya está en `Campfire`). En el nivel 3, las lucecitas a batería titilan un poco *distinto*, más eléctricas y frías.
- Niebla volumétrica de Godot 4 a baja densidad, que sube con la lluvia.
- Un viñeteado leve y un poco de grano de película: es el toque triste.

### 9.5 El Refugio de Palos (la habilidad estrella)

**Construcción paso a paso** (`BuildSite` con 6 piezas):
1. **Recoger** cada palo largo: se agacha, lo levanta con las dos manos y se lo apoya en el hombro.
2. **Colocar palo ×4:** lo levanta y lo clava en el suelo. La pieza cae desde +0.45 m con un rebote corto (0.35 s) y, si la calidad es baja, queda torcida.
3. **Martillar ×4** con una piedra: tres golpes por animación, cada uno hunde el palo 3 cm, lo hace tambalear un poco y levanta polvo.
4. **Recoger hojas ×6:** las junta en un abrazo.
5. **Colocar techo ×2:** extiende cada capa de hojas sobre los palos. La pieza entra deslizándose y se asienta.
6. **Al terminar:** la estructura se aplasta un poco y vuelve (squash). Si llueve, el agua empieza a gotear por los bordes y no adentro, salvo que haya quedado mal hecho.

**Animaciones necesarias** (`CharacterMover`): las 8 base son recoger, colocar, martillar, encender, agacharse, sentarse, señalar e inspeccionar. Las que se suman en v0.8 (colocar_palo, colocar_techo, envolver, revolver, alisar, esconderse, respirar, señalar_cielo, mirar_recuerdo, mirar_manos, confundido, recoger_pesado) **caen automáticamente a la base más parecida** hasta que existan, así se puede jugar desde el primer día.

### 9.6 Lluvia

- **Partículas:** un GPUParticles3D *pegado al rig de la cámara* (no al mundo), con alineación "Z billboard + Y a la velocidad" para que las gotas se vean como trazos. Hasta 6000 gotas en tres escalones de intensidad. Las salpicaduras van en un segundo emisor a ras del suelo alrededor de la cámara.
- **Charcos:** `WetGround.gdshader` hace crecer los charcos desde los pozos del terreno (definidos por una textura de máscara) a medida que sube `rain_wetness`, con ondas animadas y reflejos del cielo gris. Se forman en alrededor de 1 hora de juego y se secan en unas 4.
- **Personaje mojado:** `WetCharacter.gdshader` oscurece el albedo hasta un 45%, baja la rugosidad para que brille, agrega vetas de agua que bajan y hace tiritar los vértices del torso. Todo con *instance uniforms*, sin duplicar materiales.
- **Fogata bajo la lluvia:** la llama se achica, sale vapor y al final queda un hilo de humo. Con techo, gotea alrededor pero el fuego se mantiene.
- **Sonido:** lluvia en loop con volumen según la intensidad, gotas sobre las hojas del techo cuando está en el refugio (el sonido de la seguridad) y truenos con un destello de la luz direccional.

---

## 10. Propuesta técnica: que los agentes no pesen

**Me encanta la idea del agente que aprende.** Es lo que le da identidad propia a AUTONOMA. La clave técnica es que un "agente" **no es un nodo ni una red neuronal**: es un objeto de datos más un conjunto de reglas.

**1. Datos, no nodos.** Los 15 `LearningAgent` son objetos C# simples, de alrededor de 1 a 2 KB cada uno con su memoria episódica. Por personaje hay **3 nodos fijos** (LearningSystem, SkillExecutor, PedagogicalPossession), sin importar cuántas habilidades se agreguen. Agregar la habilidad 16 es agregar una entrada en `SkillTree`, no una escena.

**2. Un solo cuerpo, un solo ejecutor.** Las habilidades compiten por un único `SkillExecutor`, que resuelve una a la vez con `async/await` (moverse, animar, aplicar el efecto). No hay 15 máquinas de estados corriendo en paralelo.

**3. Tick lento.** El olvido y la elección de la próxima acción corren a 1 Hz, no por frame. 15 agentes × 1 Hz no se nota en el profiler. La utilidad de cada habilidad es una función corta sobre datos que ya están en `WorldState`.

**4. Memoria simbólica y comprimida.** No se graba la demostración frame por frame: se guardan pasos simbólicos ("colocó palo_largo en (1.2, 0, 0.4) a los 12.3 s") y como máximo 64 waypoints (solo en giros o cada 1.5 m, y se reduce a la mitad si se pasa). Las posiciones se guardan relativas a un ancla, así la misma demo se puede reproducir en otro lugar.

**5. Consultas por grupos.** Los objetos del mundo se encuentran por grupos de Godot (`item_palo`, `fuego`, `escondite`), no con raycasts constantes. Solo se busca cuando el ejecutor necesita un objetivo.

**6. Partículas y luces compartidas.** El polvo de martillar y las chispas son un emisor por obra, que se reinicia en vez de instanciarse. La lluvia es un emisor que sigue a la cámara, no uno por zona.

**7. Guardado.** Cada agente se serializa como diccionario: conocimiento, contadores, firma y las 4 demos (cada `Vector3` como array de 3 floats). Una partida completa ronda los 30 a 60 KB en JSON.

**Por qué no usar machine learning de verdad (todavía):** entrenar una red en tiempo de ejecución en Godot es pesado, impredecible y difícil de balancear. Además, el jugador no percibe la diferencia: lo que hace sentir que "aprende" es que *copie tus manías, se equivoque de formas creíbles y un día lo haga solo*. Eso se logra con imitación simbólica + utilidad + estadísticas. Si más adelante quieren ML, el lugar correcto es offline (behavior cloning sobre demos grabadas, exportado como política chica). Esta arquitectura ya guarda los datos que eso necesitaría.

---

## 11. Integración en Godot

**Árbol de nodos sugerido:**
```
Main
├─ AutonomaSim                (grupo "autonoma_sim")
├─ RainController             (+ GPUParticles3D de lluvia y salpicaduras en el rig de cámara)
├─ Personaje (CharacterBody3D + CharacterMover)
│   ├─ NavigationAgent3D
│   ├─ Modelo (mallas con WetCharacter.gdshader → listarlas en WetMeshes)
│   └─ AnimationTree
├─ LearningSystem             (Sim, Executor)
├─ SkillExecutor              (Sim, Learning, Mover, Camp, BuildSkillIds + BuildScenes)
├─ PedagogicalPossession      (Sim, Learning, Executor, Mover, Camera)
└─ Mundo
    ├─ NavigationRegion3D (horneada)
    ├─ Suelo con WetGround.gdshader
    ├─ Interactables: palos (GivesItem=palo), arbustos (StationGroup=arbusto_bayas, GivesItem=baya_buena, MaxPickups=5, FreeWhenExhausted=false), escondites (StationGroup=escondite), lugar alto (lugar_alto), recuerdo personal (recuerdo_personal)…
    └─ Campamento: fogata inicial (Interactable StationGroup=fuego + Campfire hijo con StartLit)
```

**Escenas de obra** (una por habilidad de construcción, con su `BuildSite`): refugio_palos (6 piezas, CompletedGroup=refugio), hacer_fuego (3 palos + 1 hojas = 4 piezas, CompletedGroup=fuego, con un hijo `Campfire` llamado exactamente "Campfire"), cama_paja (2), trampa_simple (3), techo_fogata (4), agua_lluvia (2).

**Input Map:** move_left, move_right, move_forward, move_back, interact.

**Project Settings → Shader Globals:** rain_wetness (float), rain_intensity (float).

**El controlador del personaje** (lo que ya tienen en v0.7) tiene que hacer cuatro cosas: llenar los flags de `CharacterState` (InShelter, NearFire, ShelterLeaks…) y los sensores de `WorldState` (ShelterDistance, FireDistance, FireHasRoof, ItemsNearby); no pisar al `SkillExecutor` mientras `Busy` sea true; llamar a `LearningSystem.RequestPractice(id)` cuando se acepta un susurro de "Practicá"; y pasar `Needs.SpeedMultiplier` a `Mover.SpeedMultiplier`.
