# AUTONOMA · VIDA — Documento de traspaso
> "Una conciencia aprende a vivir."
> Este documento es la **fuente de verdad** del proyecto. Leelo completo antes de tocar código.

---

## 0. REGLA SUPREMA: NO REGRESIÓN

La base oficial del juego es **`index.html`** (antes `autonoma_vida_referencia.html`).

1. **No reescribir desde cero.** Extender el archivo existente.
2. **No simplificar** gráficos, personaje, iluminación ni sistemas que ya funcionan.
3. Todo sistema nuevo va en un **módulo aparte** (ver sección 4) y, si falla, se puede apagar sin romper el resto.
4. Antes de entregar, comparar contra las capturas de `/baseline/`. **Si algo se ve o funciona peor, se revierte.**
5. Prioridad, en este orden: **no romper → corregir → mejorar → agregar.**

### Prohibido
- Árboles hechos con cono + cilindro, personaje hecho con esfera + bloques.
- Terreno rojo o saturado. Escenas tan oscuras que no se vea al personaje.
- Texto técnico en la UI pública: versiones, fórmulas, estados en mayúscula, "fase 0.1"…
- Personaje debajo del terreno o dentro del agua. Objetos que atraviesan el cuerpo.

### Checklist de cada entrega
- [ ] Gráficos iguales o mejores que el baseline
- [ ] El personaje nunca atraviesa el suelo, el agua ni objetos
- [ ] Todos los susurros del menú siguen funcionando
- [ ] La UI pública no muestra datos técnicos (todo eso va en F3)
- [ ] Sin errores en la consola
- [ ] FPS igual o mejor

**Formato de entrega:** qué se agregó · qué se corrigió · qué NO se tocó · checklist · capturas · problemas conocidos.

---

## 1. Visión

El jugador **no controla** a AUTONOMA: es una **voz que lo acompaña**, le susurra y le enseña.
AUTONOMA tiene necesidades, decide solo, se equivoca y aprende.
**Se gana cuando AUTONOMA ya no te necesita.**

La pregunta que tiene que generar el juego: **"¿Qué va a hacer ahora?"**

Referencias de sensación (no copiar assets): Firewatch (luz), The Long Dark (soledad y clima),
Kena (naturaleza viva), Black & White (criatura que aprende), RimWorld (historias emergentes).

---

## 2. Historia (Episodio 1)

Base literaria (dominio público): **Hayy ibn Yaqzan** (aprender el mundo solo, observando)
+ **Frankenstein** (un ser creado y abandonado). El zorro toma la idea de *El Principito* (vínculo con paciencia).
No usar *El robot salvaje* como referencia: es demasiado parecido.

| Acto | Qué pasa |
|---|---|
| I · Despertar | AUTONOMA sobrevive: fuego, refugio, agua, comida. Vos sos la voz que lo acompaña. |
| II · Rastros | En la ruina aparecen objetos del creador. Aparece **el Extraño**, que roba por hambre. |
| III · La pregunta | En la cueva, marcas de intentos anteriores. Entiende que lo dejaron ahí a propósito. |
| IV · Elegir | **Hogar** (se queda) · **La Carta** (se va a buscar al creador) · **Legado** (le enseña a otro). |

---

## 3. Reglas de diseño que no se cambian

- Demostración (posesión) limitada al **59 %** de conocimiento. Posesión cuesta **30 Fe**.
- Olvido **multiplicativo 10 % cada 48 h**. El "quirk" del maestro persiste.
- Decisiones del cerebro a **1 Hz**. Un solo ejecutor de habilidad a la vez.
- **"Lo hizo solo"** aparece la primera vez que hace cada habilidad sin que se la pidan.
- **Sin combate.** Las amenazas asustan, roban o enferman, pero no pelean.
- **Nada es bueno o malo siempre:** depende del momento, de las señales y de lo aprendido.
- Toda amenaza muestra **señales visibles antes** de actuar (el jugador también aprende a leerlas).
- Los estados se comunican con **conducta**, no con barras ni números.

---

## 4. Lo que YA funciona en `index.html`

Un solo archivo con Three.js r160. Módulos marcados con `// ===== N.`:

| Módulo | Contenido |
|---|---|
| 1 CONFIG | Valores ajustables |
| 2 TERRENO | `heightAt(x,z)` = **única fuente de altura del mundo** |
| 3 ARROYO | Cauce excavado, agua con reflejo |
| 4 CIELO | Atardecer procedural, sombras largas, bloom |
| 5 VEGETACIÓN | 3 tipos de árbol, arbustos, pasto con viento, flores, rocas |
| 6 CAMPAMENTO | Fogata (luz, chispas, humo), refugio, olla, tocones, tronco |
| 7 PERSONAJE | Humanoide articulado, mochila, bufanda, parpadeo, mirada |
| 8 ZORRO | Confianza que crece con el trato |
| 8b MUNDO | Bayas buenas/malas, arroyo, refugio, tronco, roca, flores, mirador |
| 8c PÁJAROS | Se posan en bayas buenas, huyen si te acercás |
| 8d NARRADOR | Viento, ramas que caen de árboles reales, bayas que rebrotan |
| 8e AGENTES DE OBJETOS | Cada cosa tiene estado + menú de susurros |
| 9 CEREBRO | Necesidades + IA de utilidad + memoria (`KNOW`) + consulta al jugador |
| 10 CÁMARA · 11 UI | Rotar/zoom, diario (J), debug (F3) |

**Necesidades:** hambre, sed, frío, energía, curiosidad, compañía.
**Aprende solo:** bayas (probando u observando pájaros), echar leña, sacudir árboles, preparar caldo.
**Te consulta** cuando no sabe si algo se come.

### Cómo agregar un objeto nuevo
1. Crear su malla y registrarlo con `addInter({...})`.
2. Agregar sus acciones en `MENU`.
3. Agregar el efecto en `applyAct()` y la duración en `ACT_DUR`.
4. Si debe hacerlo solo una vez aprendido, sumarlo en `options()`.

---

## 5. Hoja de ruta (en orden, una fase por vez)

| Fase | Objetivo |
|---|---|
| 1 | **Modelo GLB riggeado** para AUTONOMA, conservando los mismos sockets y estados. Árboles menos redondos. Cámara que esquiva árboles. |
| 2 | **Guardado** (localStorage, con versión) + exportar/importar partida. |
| 3 | **Tormentas legibles:** señales 1 día antes (cielo rojo, pájaros bajos, viento, animales escondidos). |
| 4 | **Construcción:** elegir lugar (árbol hueco puede caer, arroyo se inunda) y armar la choza pieza por pieza. Evaluador estructural **determinístico**, sin LLM. |
| 5 | **Amenazas sin combate:** urracas y mapaches que roban, oso con estados (tranquilo, curioso, hambriento, con crías, celo). |
| 6 | **Mundo que sigue sin vos:** 1 día real = 1 día de juego (máx. 14). Modo Pausado opcional. |
| 7 | **Expansión:** pradera, bosque profundo, ruina, cueva, lago. Carga por regiones. |
| 8 | **Historia:** el creador, el Extraño, los tres finales. Diario con voz propia (LLM opcional, solo para escribir). |

**Presupuesto web:** campamento 40–80 MB; total 150–300 MB cargado por regiones.

---

## 6. Cómo trabajamos

- `main` = siempre la última versión estable. Cada fase en su rama: `fase-N-nombre`.
- Etiquetar versiones estables: `v2.0-base`, `v2.1-glb`, etc.
- **Mauricio aprueba** cada merge. Ningún agente aprueba su propio trabajo.
- Una tarea chica por pedido. Nunca "mejorá todo el juego".
