// AUTONOMA – Catálogo de eventos + Narrador v0.8 (Godot 4.1+ .NET / C#)
// v0.8: 25 eventos (5 nuevos de aprendizaje/lluvia), Fe, lluvia y eventos externos (los dispara LearningSystem).
// Los ids de evento/memoria/reacción coinciden con la Biblia (AUTONOMA_Biblia_Personalidad_Eventos.md).
// Los ids de reacción son claves para el árbol de comportamiento / AnimationTree.

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static Autonoma.ReactionProfile;

namespace Autonoma;

public sealed class GameEventDef
{
    public string Id = "", Name = "";
    public int Score;                          // 0 trágico … 10 ideal
    public bool Terminal, UsesMoralDecision;
    public bool OncePerRun = true;
    public float CooldownHours = 24f;          // solo si OncePerRun = false
    public float BaseWeight = 1f;
    public float RescueWindowSeconds;          // finales trágicos: ventana real antes de cerrar (Resiliente ×1.5)
    public Func<CharacterState, WorldState, bool> Trigger = (_, _) => false;
    public Func<CharacterState, WorldState, float> WeightBonus; // opcional (null = 1)
    public Dictionary<ReactionProfile, string> Reactions = new();
}

public static class EventCatalog
{
    static Dictionary<ReactionProfile, string> R(params (ReactionProfile p, string id)[] xs) =>
        xs.ToDictionary(x => x.p, x => x.id);

    static float BerryThreshold(Traits t) =>
        t.Curiosidad >= Traits.High ? 35f : t.Valentia <= Traits.Low ? 8f : 15f;

    static bool StormyOrRain(WorldState w) =>
        w.Weather is Weather.Lluvia or Weather.Tormenta or Weather.Ventisca;

    public static readonly List<GameEventDef> All = new()
    {
        // ═══════════ 0 · FINALES TRÁGICOS ═══════════
        new() {
            Id = "ultimo_abrazo", Name = "Último Abrazo a la Luz", Score = 0, Terminal = true, RescueWindowSeconds = 60f,
            Trigger = (c, w) => c.HoursStarving >= 8f && w.IsNight && c.HasTorch
                                && c.Has("antorcha_regalada") && c.Needs.Vinculo >= 40f,
            // Empatico solo si tiene "cria_adoptada"; si no, el controlador usa Neutral.
            Reactions = R((Leal, "levanta_antorcha_al_cielo"), (Valiente, "antorcha_estandarte"),
                          (Miedoso, "ovillo_en_la_llama"), (Resiliente, "intenta_levantarse"),
                          (Empatico, "despide_a_la_cria")),
        },
        new() {
            Id = "ojos_salvajes", Name = "Ojos Salvajes", Score = 0, Terminal = true, RescueWindowSeconds = 45f,
            Trigger = (c, w) => c.Needs.Vinculo < 10f
                                && c.ConsecutiveFearNights >= (c.Traits.Is(Leal) ? 5 : 3)
                                && c.HoursSinceRecuerdo >= 72f,
            Reactions = R((Valiente, "depredador"), (Miedoso, "presa"), (Curioso, "nido_brillante"),
                          (Leal, "resiste"), (Resiliente, "se_va_lucido")),
        },
        new() {
            Id = "rendicion", Name = "La Rendición", Score = 0, Terminal = true, RescueWindowSeconds = 90f,
            Trigger = (c, w) => c.HoursMoraleCritical >=
                                (c.Traits.Resiliencia < 30f ? 24f : c.Traits.Is(Resiliente) ? 96f : 48f),
            Reactions = R((Resiliente, "rendicion_ciclica"), (Leal, "para_que_si_vos_no"),
                          (Empatico, "solo_por_otros"), (Miedoso, "se_encierra")),
        },

        // ═══════════ 1–4 ═══════════
        new() {
            Id = "bayas_ceniza", Name = "Bayas de Ceniza", Score = 1,
            Trigger = (c, w) => c.Needs.Saciedad < BerryThreshold(c.Traits) && w.UnknownBerriesNearby
                                && !w.KnownBerriesNearby && !c.Has("bayas_ceniza_venenosas")
                                && !c.Skills.IsAtLeast("identificar_bayas", SkillState.Practicando),
            Reactions = R((Curioso, "prueba_y_analiza"), (Miedoso, "una_sola_y_escupe"),
                          (Valiente, "punado"), (Resiliente, "recuperacion_rapida"), (Neutral, "come_bayas")),
        },
        new() {
            Id = "llama_apaga", Name = "La Llama se Apaga", Score = 2, OncePerRun = false, CooldownHours = 24f,
            Trigger = (c, w) => w.IsNight && StormyOrRain(w) && !c.InShelter
                                && (c.HasTorch || (c.NearFire && !w.FireHasRoof)),
            Reactions = R((Valiente, "reencender_a_mano"), (Miedoso, "corre_a_ciegas"),
                          (Leal, "espera_senal"), (Curioso, "mira_relampagos")),
        },
        new() {
            Id = "cazado", Name = "Cazado", Score = 2, UsesMoralDecision = true, OncePerRun = false, CooldownHours = 48f,
            Trigger = (c, w) => (w.Biome is Biome.BosqueAncestral or Biome.BaldioDeCeniza)
                                && (w.IsNight || w.Weather == Weather.Niebla) && w.HostileDistance < 25f,
            WeightBonus = (c, w) => c.Needs.Energia < 50f ? 2f : 1f,
            Reactions = R((Valiente, "se_planta"), (Miedoso, "huye_ya"),
                          (Empatico, "ve_la_herida"), (Curioso, "mira_demasiado")),
        },
        new() {
            Id = "grieta_hielo", Name = "Grieta en el Hielo", Score = 3, OncePerRun = false, CooldownHours = 48f,
            Trigger = (c, w) => w.Biome == Biome.CumbresHeladas && w.OnFrozenLake
                                && (c.Needs.Curiosidad > 60f || c.IsFleeing),
            WeightBonus = (c, w) => (w.IsNight || w.Weather == Weather.Ventisca) ? 2f : 1f,
            Reactions = R((Valiente, "sale_solo"), (Miedoso, "paralizado"),
                          (Curioso, "brillo_bajo_hielo"), (Resiliente, "se_rie")),
        },
        new() {
            Id = "silencio_dios", Name = "El Silencio del Dios", Score = 3, OncePerRun = false, CooldownHours = 48f,
            // Si nadie responde en 40 s: Vinculo -15 y Remember("abandonado") (Resiliente: -5 y "autosuficiente").
            Trigger = (c, w) => c.HoursSinceInfluence >= 24f && c.Needs.Vinculo > 30f && c.Needs.Miedo < 50f,
            Reactions = R((Leal, "llama_insistente"), (Resiliente, "se_encoge_de_hombros"),
                          (Curioso, "escala_para_buscarte"), (Empatico, "deja_ofrenda")),
        },
        new() {
            Id = "extrano_herido", Name = "El Extraño Herido", Score = 4, UsesMoralDecision = true,
            // Curar → "salvo_extrano" · Atacar → "robo_extrano" · Huir → "ignoro_extrano"
            Trigger = (c, w) => w.Day >= 4 && c.Needs.Saciedad > 30f && w.StrangerSpawnNearby,
            Reactions = R((Empatico, "corre_a_ayudar"), (Miedoso, "observa_escondido"),
                          (Valiente, "se_acerca_directo"), (Leal, "espera_tu_decision")),
        },
        new() {
            Id = "pesadilla", Name = "Pesadilla del Hogar", Score = 4, OncePerRun = false, CooldownHours = 24f,
            Trigger = (c, w) => c.IsSleeping && c.FearAtSleepStart > 50f && c.HasAnyTrauma,
            Reactions = R((Miedoso, "vela_el_fuego"), (Resiliente, "sigue_durmiendo"),
                          (Leal, "te_busca"), (Curioso, "sueno_como_brujula")),
        },

        // ═══════════ 5–7 ═══════════
        new() {
            Id = "tormenta_ceniza", Name = "Tormenta de Ceniza", Score = 5, OncePerRun = false, CooldownHours = 48f,
            BaseWeight = 0.5f,
            // Este evento INICIA la tormenta: aviso de 60 s (cielo rojo) y luego Weather = TormentaCeniza.
            Trigger = (c, w) => w.Biome == Biome.BaldioDeCeniza && w.Day >= 3
                                && w.Weather == Weather.Despejado && !w.IsNight,
            Reactions = R((Valiente, "avanza_cubierto"), (Miedoso, "primer_hueco"),
                          (Curioso, "formas_en_la_ceniza"), (Resiliente, "se_tumba_y_espera")),
        },
        new() {
            Id = "ruinas", Name = "Ruinas Ancestrales", Score = 5, OncePerRun = false, CooldownHours = 12f,
            // Al encontrar el fragmento: Remember($"fragmento_mapa_{w.Biome}")
            Trigger = (c, w) => c.Needs.Curiosidad > 50f && c.Needs.Miedo < 60f && w.RuinNearby
                                && !w.IsNight && !c.Has($"fragmento_mapa_{w.Biome}"),
            Reactions = R((Curioso, "explora_todo"), (Miedoso, "mira_desde_la_puerta"),
                          (Valiente, "entra_de_apuro"), (Leal, "vos_conocias_esto")),
        },
        new() {
            Id = "primer_fuego", Name = "Primer Fuego Propio", Score = 6,
            // v0.8: EXTERNO. LearningSystem lo dispara la primera vez que Hacer Fuego sale bien
            // sin posesión (y guarda "fuego_propio"). Una antorcha divina durante el intento lo cancela.
            Trigger = (_, _) => false,
            Reactions = R((Resiliente, "primer_intento"), (Valiente, "se_quema_orgulloso"),
                          (Leal, "te_lo_muestra"), (Miedoso, "tres_intentos")),
        },
        new() {
            Id = "la_cria", Name = "La Cría", Score = 7,
            // Si adopta: Remember("cria_adoptada"). Resiliente + Empatia<30 + Saciedad<30 → la caza.
            Trigger = (c, w) => (w.Biome is Biome.BosqueAncestral or Biome.CumbresHeladas)
                                && !w.IsNight && c.Needs.Miedo < 50f && w.OrphanCubNearby,
            Reactions = R((Empatico, "adopta_y_nombra"), (Curioso, "sigue_a_la_madriguera"),
                          (Miedoso, "apego_ansioso"), (Resiliente, "evalua_utilidad"), (Neutral, "adopta")),
        },
        new() {
            Id = "noche_estrellas", Name = "Noche de Estrellas", Score = 7, OncePerRun = false, CooldownHours = 48f,
            // Curioso: Remember("lee_estrellas")
            Trigger = (c, w) => w.IsNight && w.Weather == Weather.Despejado && c.Needs.Miedo < 20f
                                && c.Needs.Saciedad > 50f && c.Needs.Energia > 40f && c.NearFire,
            Reactions = R((Leal, "dibuja_tu_constelacion"), (Curioso, "nombra_estrellas"),
                          (Empatico, "abraza_y_cuenta"), (Resiliente, "silencio_y_sonrisa")),
        },

        // ═══════════ 8–9 ═══════════
        new() {
            Id = "el_nombre", Name = "El Nombre", Score = 8,
            Trigger = (c, w) => c.HoursBondAbove80 >= 48f && c.RecuerdosAceptados >= 3
                                && c.DaysSince("abandonado", w.Day) > 3,
            Reactions = R((Leal, "nombre_devocional"), (Curioso, "nombre_pregunta"), (Valiente, "nombre_camarada"),
                          (Resiliente, "nombre_seco"), (Empatico, "nombre_suave")),
        },
        new() {
            Id = "ecos_tribu", Name = "Ecos de la Tribu", Score = 9,
            // Sobrevivir una amenaza junto a la tribu → Remember("defendio_tribu")
            Trigger = (c, w) => c.Has("salvo_extrano") && w.Day >= 10
                                && c.DaysSince("salvo_extrano", w.Day) >= 4 && c.Needs.Animo > 50f,
            Reactions = R((Empatico, "cura_al_grupo"), (Valiente, "monta_guardia"),
                          (Curioso, "intercambia_historias"), (Miedoso, "a_distancia"), (Leal, "les_habla_de_vos")),
        },

        // ═══════════ 10 · FINALES IDEALES ═══════════
        new() {
            Id = "hogar_permanente", Name = "Hogar Permanente", Score = 10, Terminal = true,
            // v0.8: Hogar 100% = 4 habilidades de supervivencia Dominadas + campamento nivel 3
            //       + Calmarse Solo Dominado + 72 h sin ayuda directa (objeto, recuerdo o posesión).
            Trigger = (c, w) => c.Has("fuego_propio") && HomeProgress.Compute(c) >= 99.9f
                                && c.AvgSaciedad3d > 60f && c.AvgEnergia3d > 60f && w.Day >= 15,
            Reactions = R((Resiliente, "envejece_en_el_valle"), (Leal, "tu_simbolo_en_la_puerta"),
                          (Empatico, "refugio_de_animales"), (Curioso, "base_de_expediciones"),
                          (Valiente, "protector_del_valle")),
        },
        new() {
            Id = "carta_despedida", Name = "La Carta de Despedida", Score = 10, Terminal = true,
            // Si Vinculo < 50 → variante "salida silenciosa" (sin carta, cuenta como 8).
            // Contenido: AutonomaSim.TopMoments(3).
            Trigger = (c, w) => (c.MapFragments >= 3 || (c.MapFragments >= 2 && c.Has("lee_estrellas")))
                                && w.AtMountainPass,
            Reactions = R((Leal, "carta_larga"), (Resiliente, "tres_lineas"), (Curioso, "carta_de_preguntas"),
                          (Empatico, "cuida_lo_que_dejo"), (Valiente, "carta_en_la_lanza")),
        },
        new() {
            Id = "guia_tribu", Name = "Guía de la Tribu", Score = 10, Terminal = true, UsesMoralDecision = true,
            // Decisión: Atacar = tribu guerrera · Curar = sanadora · Huir = nómada
            Trigger = (c, w) => c.Has("ecos_tribu") && c.Has("salvo_extrano") && c.Has("defendio_tribu")
                                && !c.Has("robo_extrano")
                                && (c.Traits.Empatia >= 50f || c.Traits.Valentia >= 65f)
                                && c.Needs.Animo > 60f,
            Reactions = R((Empatico, "guia_curandero"), (Valiente, "guia_protector"), (Leal, "guia_sacerdote"),
                          (Curioso, "guia_explorador"), (Resiliente, "guia_reacio")),
        },

        // ═══════════ v0.8 · APRENDIZAJE Y LLUVIA ═══════════
        new() {
            Id = "la_lluvia", Name = "La Lluvia", Score = 3, OncePerRun = false, CooldownHours = 30f, BaseWeight = 0.8f,
            // INICIA la lluvia: RainController.Forecast(1.5 h) → llueve 3-6 h. En Cumbres es aguanieve.
            // El pronóstico (viento, hojas volando, cielo gris) da tiempo a los que dominan Refugio.
            Trigger = (c, w) => !w.IsRaining && float.IsPositiveInfinity(w.RainForecastHours)
                                && w.Biome != Biome.BaldioDeCeniza && w.Day >= 2,
            Reactions = R((Valiente, "aguanta_bajo_el_agua"), (Miedoso, "se_esconde_temblando"),
                          (Curioso, "mira_los_charcos"), (Resiliente, "sigue_trabajando")),
        },
        new() {
            Id = "lo_hizo_solo", Name = "Lo Hizo Solo", Score = 8, OncePerRun = false, CooldownHours = 0f,
            // EXTERNO: primera ejecución autónoma exitosa de cada habilidad (salvo Hacer Fuego → primer_fuego).
            Trigger = (_, _) => false,
            Reactions = R((Leal, "te_lo_muestra_orgulloso"), (Resiliente, "asiente_para_si"),
                          (Curioso, "lo_mira_desde_todos_lados"), (Valiente, "grito_de_triunfo"),
                          (Empatico, "se_lo_muestra_a_la_cria")),
        },
        new() {
            Id = "lo_olvido", Name = "Lo Olvidó", Score = 2, OncePerRun = false, CooldownHours = 0f,
            // EXTERNO: una habilidad cae de Imitando+ a No aprendido (pánico o falta de práctica).
            Trigger = (_, _) => false,
            Reactions = R((Miedoso, "se_mira_las_manos_asustado"), (Leal, "te_busca_para_que_le_muestres"),
                          (Resiliente, "lo_intenta_igual"), (Curioso, "lo_reinventa_mal")),
        },
        new() {
            Id = "te_imita", Name = "Te Imita en Todo", Score = 6,
            // EXTERNO: la primera vez que, imitando, reproduce un gesto de más tuyo (rodeo, golpes de más, pausa).
            Trigger = (_, _) => false,
            Reactions = R((Leal, "copia_exacta_reverente"), (Curioso, "exagera_el_gesto"),
                          (Miedoso, "copia_temblando"), (Resiliente, "lo_hace_a_su_manera")),
        },
        new() {
            Id = "ensena_a_otro", Name = "Le Enseña a Otro", Score = 9,
            // Ahora ÉL enseña: a la cría o a alguien de la tribu, lo que vos le enseñaste.
            Trigger = (c, w) => (c.Has("cria_adoptada") || c.Has("ecos_tribu"))
                                && c.Skills.CountAtLeast(SkillState.Dominado) >= 3
                                && (c.Traits.Empatia >= 50f || c.Traits.Lealtad >= 65f)
                                && c.Needs.Miedo < 40f && !w.IsNight,
            Reactions = R((Empatico, "ensena_con_paciencia"), (Leal, "ensena_como_vos"),
                          (Valiente, "ensena_a_defenderse"), (Curioso, "ensena_y_aprende"),
                          (Resiliente, "ensena_sin_palabras")),
        },
    };
}
