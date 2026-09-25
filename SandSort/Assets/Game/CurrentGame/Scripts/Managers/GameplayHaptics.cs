using Moow;
using UnityEngine;

// Gameplay haptics (2026-09-25): the only place the sand puzzle decides WHEN and HOW HARD to buzz.
// It plays through MoowCore's HapticManager (BaseScene, DontDestroyOnLoad), which already honours the
// Settings popup's vibration toggle (BaseDataSO.disableHaptic) and routes to the iOS plugin / Android
// Vibrator. Nothing here reads or writes the sand: the extraction pulses are driven purely by the cell
// counts ExtractionGrid already reports each frame.
//
// - shapeSelected: press on a shape (Container.beginDrag) — one soft tap.
// - shapeDropped: player lets go of a shape (Container.endDrag) — one slightly firmer tap. Dragging
//   itself never buzzes.
// - tickExtraction: once per frame from ExtractionGrid.LateUpdate with the cells removed by every
//   shape that frame. Those become a flow rate (cells/s) smoothed over FLOW_SMOOTHING, mapped between
//   FLOW_LIGHT and FLOW_HEAVY (the same band the extraction sound's volume uses) onto a pulse interval
//   and a pulse type: a thin trickle gives sparse soft ticks, a full drain denser, firmer ones. No
//   pulse on a frame that removed nothing, so haptics stop the moment the sand stops.
//
// Fails quietly: in the Editor HapticManager only logs to its debugger; with no HapticManager (a
// scene played without BaseScene) or a platform call throwing, haptics switch themselves off for the
// rest of the session instead of spamming warnings or breaking input.
public static class GameplayHaptics {

    const HapticFeedbackType SELECT_TYPE = HapticFeedbackType.FeedbackSoft;
    const HapticFeedbackType DROP_TYPE = HapticFeedbackType.FeedbackMedium;
    const HapticFeedbackType FLOW_LIGHT_TYPE = HapticFeedbackType.FeedbackSoft;
    const HapticFeedbackType FLOW_HEAVY_TYPE = HapticFeedbackType.FeedbackLight;

    const float FLOW_SMOOTHING = 0.15f;
    const float FLOW_LIGHT = 150f;
    const float FLOW_HEAVY = 900f;
    // Pulse spacing at the light and heavy ends of the flow band, in seconds.
    const float PULSE_INTERVAL_LIGHT = 0.28f;
    const float PULSE_INTERVAL_HEAVY = 0.09f;
    // Above this flow intensity (0..1) the pulses switch to FLOW_HEAVY_TYPE.
    const float HEAVY_TYPE_THRESHOLD = 0.6f;
    // Frames without any drain longer than this reset the flow, so the next drain starts light.
    const float DRY_RESET = 0.3f;

    static float s_flowRate;
    static float s_lastDrainTime;
    static float s_lastPulseTime;
    static bool s_unavailable;

    // Statics survive "Enter Play Mode without domain reload", so they are reset per play session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void reset() {
        s_flowRate = 0f;
        s_lastDrainTime = float.NegativeInfinity;
        s_lastPulseTime = float.NegativeInfinity;
        s_unavailable = false;
    }

    public static void shapeSelected() => play(SELECT_TYPE);

    public static void shapeDropped() => play(DROP_TYPE);

    public static void tickExtraction(int frameCells, float deltaTime) {
        if (deltaTime <= 0f) return;
        float now = Time.unscaledTime;

        if (frameCells <= 0) {
            if (now - s_lastDrainTime > DRY_RESET) s_flowRate = 0f;
            return;
        }

        float frameRate = frameCells / deltaTime;
        if (now - s_lastDrainTime > DRY_RESET) s_flowRate = frameRate;
        else s_flowRate += (frameRate - s_flowRate) * (1f - Mathf.Exp(-deltaTime / FLOW_SMOOTHING));
        s_lastDrainTime = now;

        float intensity = Mathf.InverseLerp(FLOW_LIGHT, FLOW_HEAVY, s_flowRate);
        float interval = Mathf.Lerp(PULSE_INTERVAL_LIGHT, PULSE_INTERVAL_HEAVY, intensity);
        if (now - s_lastPulseTime < interval) return;

        s_lastPulseTime = now;
        play(intensity >= HEAVY_TYPE_THRESHOLD ? FLOW_HEAVY_TYPE : FLOW_LIGHT_TYPE);
    }

    static void play(HapticFeedbackType type) {
        if (s_unavailable) return;

        try {
            HapticManager manager = HapticManager.instance;
            if (manager == null) {
                s_unavailable = true;
                return;
            }
            manager.feedback(type);
        } catch (System.Exception exception) {
            s_unavailable = true;
            Debug.LogWarning($"[GameplayHaptics] Haptics disabled for this session: {exception.Message}");
        }
    }
}
