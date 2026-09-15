using UnityEditor;
using UnityEngine;

// TEMPORARY visual-test tool for ShapeSandFill (2026-09-13). Editor-only: it lives in an Editor
// folder, so it is never compiled into a build and costs the game nothing.
//
// It drives Shape.setFillPercent, which is the same entry Container already uses for the readout —
// so it moves the VISUAL only. Container._filledUnits, capacity, sealing, extraction and the sand
// grid are never touched, and everything it does is gone when Play Mode stops.
//
// Open it from the menu: Window > SandSort > Shape Sand Fill Test.
public class ShapeSandFillTestWindow : EditorWindow {

    static readonly float[] STEPS = { 0f, 0.25f, 0.5f, 0.75f, 1f };
    static readonly string[] STEP_LABELS = { "0%", "25%", "50%", "75%", "100%" };

    Vector2 _scroll;
    bool _linked;

    [MenuItem("Window/SandSort/Shape Sand Fill Test")]
    static void Open() {
        var window = GetWindow<ShapeSandFillTestWindow>();
        window.titleContent = new GUIContent("Sand Fill Test");
        window.minSize = new Vector2(420f, 260f);
    }

    // The drawn fill animates, so keep repainting or the readout lags behind the game view.
    void OnInspectorUpdate() => Repaint();

    void OnGUI() {
        if (!Application.isPlaying) {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to drive the fills.\n\n" +
                "Test level: SandSort_FillTest_5Shapes_Level (7x9, five shapes, five colours, none on " +
                "the extraction row so nothing auto-fills over your values).",
                MessageType.Info);
            return;
        }

        Level level = FindFirstObjectByType<Level>();
        if (level == null) {
            EditorGUILayout.HelpBox("No Level in the scene yet — it is still loading.", MessageType.Warning);
            return;
        }

        Container[] containers = level.GetComponentsInChildren<Container>(true);
        if (containers.Length == 0) {
            EditorGUILayout.HelpBox("Level has no Containers.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Visual only — capacity, extraction and the sand grid are untouched.", EditorStyles.miniLabel);
        EditorGUILayout.Space(4f);

        // ---- all at once ----
        EditorGUILayout.LabelField("All shapes", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope()) {
            for (int s = 0; s < STEPS.Length; s++) {
                if (GUILayout.Button(STEP_LABELS[s], GUILayout.Height(24f))) SetAll(containers, STEPS[s]);
            }
        }
        _linked = EditorGUILayout.ToggleLeft("Slider below moves every shape together", _linked);
        if (_linked) {
            float v = EditorGUILayout.Slider("All", CurrentOf(containers[0]), 0f, 1f);
            SetAll(containers, v);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Per shape", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (Container container in containers) {
            Shape shape = container.GetComponent<Shape>();
            if (shape == null) continue;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                using (new EditorGUILayout.HorizontalScope()) {
                    Rect swatch = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f), GUILayout.Height(16f));
                    EditorGUI.DrawRect(swatch, ColorOf(container));
                    EditorGUILayout.LabelField(Pretty(container.name), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(Mathf.RoundToInt(CurrentOf(container) * 100f) + "%",
                        EditorStyles.miniBoldLabel, GUILayout.Width(46f));
                }

                using (new EditorGUILayout.HorizontalScope()) {
                    for (int s = 0; s < STEPS.Length; s++) {
                        if (GUILayout.Button(STEP_LABELS[s])) shape.setFillPercent(STEPS[s]);
                    }
                }

                float v = EditorGUILayout.Slider(CurrentOf(container), 0f, 1f);
                if (!Mathf.Approximately(v, CurrentOf(container))) shape.setFillPercent(v);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    static void SetAll(Container[] containers, float value) {
        foreach (Container container in containers) {
            Shape shape = container.GetComponent<Shape>();
            if (shape != null) shape.setFillPercent(value);
        }
    }

    // What the fill visual is currently drawing. Read straight off ShapeSandFill so the readout
    // follows the animation rather than the value that was last asked for.
    static float CurrentOf(Container container) {
        ShapeSandFill fill = container.GetComponent<ShapeSandFill>();
        return fill != null ? fill.drawnFill : 0f;
    }

    static Color ColorOf(Container container) => SandColorUtility.toUnityColor(container.targetColor);

    static string Pretty(string name) => name.Replace("Container_", "").Replace("_Deg0", "");
}
