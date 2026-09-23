using System.IO;
using UnityEditor;
using UnityEngine;

// Window front-end for SandPatternImporter: pick a picture, get a palette-exact sand-pattern PNG.
// Runs entirely in Edit Mode. Open it from Tools > SandSort > Sand Pattern Importer.
public class SandPatternImporterWindow : EditorWindow {

    string _sourcePath;
    SandPaletteSO _palette;
    string _outputFolder = SandPatternTextureImportSettings.PATTERN_FOLDER.TrimEnd('/');
    string _outputName;

    // index = slot - 1. Resized (all enabled) whenever the palette's slot count changes.
    bool[] _allowed;
    SandPatternImporter.ResizeMode _resizeMode = SandPatternImporter.ResizeMode.PaletteMajority;
    int _widthCells = SandPatternImporter.DEFAULT_WIDTH_CELLS;
    bool _livePreview = true;

    SandPatternImporter.Result _result;
    string _error;
    Vector2 _scroll;

    // Preview state — never serialized; rebuilt on demand.
    [System.NonSerialized] SandPatternImporter.Source _source;
    [System.NonSerialized] SandPatternImporter.Result _preview;
    [System.NonSerialized] Texture2D _previewTexture;
    [System.NonSerialized] string _previewKey;
    [System.NonSerialized] string _previewError;
    [System.NonSerialized] bool _previewRequested;

    const float SWATCH_CELL_WIDTH = 150f;

    [MenuItem("Tools/SandSort/Sand Pattern Importer")]
    static void Open() {
        var window = GetWindow<SandPatternImporterWindow>();
        window.titleContent = new GUIContent("Sand Pattern Importer");
        window.minSize = new Vector2(440f, 360f);
    }

    void OnEnable() {
        if (_palette == null) _palette = SandPatternTextureImportSettings.findPalette();
        if (string.IsNullOrEmpty(_sourcePath) && Selection.activeObject is Texture2D selected) setSource(fullPathOf(selected));
    }

    void OnDisable() => clearPreview();

    void OnGUI() {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("Source Image", EditorStyles.boldLabel);
        Texture2D projectTexture = !string.IsNullOrEmpty(_sourcePath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(toAssetPath(_sourcePath)) : null;
        Texture2D picked = (Texture2D)EditorGUILayout.ObjectField("Project Texture", projectTexture, typeof(Texture2D), false);
        if (picked != projectTexture && picked != null) setSource(fullPathOf(picked));
        using (new EditorGUILayout.HorizontalScope()) {
            string typed = EditorGUILayout.TextField("File", _sourcePath);
            if (typed != _sourcePath) setSource(typed);
            if (GUILayout.Button("Browse…", GUILayout.Width(72f))) {
                string path = EditorUtility.OpenFilePanelWithFilters("Source image", sourceBrowseFolder(), new[] { "Images", "png,jpg,jpeg" });
                if (!string.IsNullOrEmpty(path)) setSource(path);
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
        _palette = (SandPaletteSO)EditorGUILayout.ObjectField("Sand Palette", _palette, typeof(SandPaletteSO), false);
        _resizeMode = (SandPatternImporter.ResizeMode)EditorGUILayout.EnumPopup(
            new GUIContent("Resize", "Palette Majority: snap source pixels to the enabled colours, then each output pixel takes the colour covering most of its area — never invents in-between colours.\nArea Average: average real colours, then snap — smoother on photos, but region edges can become an in-between colour (red + blue -> purple)."),
            _resizeMode);

        syncAllowed();
        if (_palette != null) drawAllowedColors();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope()) {
            _outputFolder = EditorGUILayout.TextField("Folder", _outputFolder);
            if (GUILayout.Button("Browse…", GUILayout.Width(72f))) {
                string path = EditorUtility.OpenFolderPanel("Output folder", Path.GetFullPath(_outputFolder), "");
                if (!string.IsNullOrEmpty(path)) {
                    string asset = toAssetPath(path);
                    if (asset != null) _outputFolder = asset;
                    else EditorUtility.DisplayDialog("Sand Pattern Importer", "The output folder must be inside this project's Assets folder.", "OK");
                }
            }
        }
        _outputName = EditorGUILayout.TextField("File Name", _outputName);
        _widthCells = EditorGUILayout.IntSlider(
            new GUIContent("Target Width (Cells)", $"Output width in sand cells; one cell is always {SandPatternImporter.PIXELS_PER_CELL} px. Height follows the source's aspect ratio."),
            _widthCells, SandPatternImporter.MIN_WIDTH_CELLS, SandPatternImporter.MAX_WIDTH_CELLS);
        EditorGUILayout.LabelField(" ", widthInfo(), EditorStyles.miniLabel);

        string outputPath = outputAssetPath();
        if (outputPath != null && !SandPatternTextureImportSettings.isSandPatternPath(outputPath))
            EditorGUILayout.HelpBox("Output is outside SandPatterns/ — the Sand Pattern importer will not apply its settings or validate it.", MessageType.Warning);

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope()) {
            using (new EditorGUI.DisabledScope(!canMap())) {
                if (GUILayout.Button("Preview", GUILayout.Height(30f), GUILayout.Width(90f))) { _previewRequested = true; Repaint(); }
            }
            using (new EditorGUI.DisabledScope(!canImport(outputPath))) {
                if (GUILayout.Button("Import & Optimize", GUILayout.Height(30f))) runImport(outputPath);
            }
        }
        _livePreview = EditorGUILayout.ToggleLeft("Live preview (re-map whenever a setting changes)", _livePreview);

        if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Error);
        if (_result != null) drawResult();

        // Only on Layout: changing what the preview draws between Layout and Repaint breaks GUILayout.
        if (Event.current.type == EventType.Layout && canMap() && (_livePreview || _previewRequested)) {
            refreshPreview(force: _previewRequested);
            _previewRequested = false;
        } else if (Event.current.type == EventType.Layout && !canMap() && _previewKey != null) {
            clearPreview();   // e.g. every colour disabled — never leave a stale preview on screen
        }
        drawPreview();

        EditorGUILayout.EndScrollView();
    }

    // ---- allowed colours ----------------------------------------------------------------------

    void syncAllowed() {
        if (_palette == null) return;
        if (_allowed == null || _allowed.Length != _palette.slotCount) _allowed = SandPatternImporter.allEnabled(_palette.slotCount);
    }

    void drawAllowedColors() {
        EditorGUILayout.Space(6f);
        int enabled = 0;
        for (int i = 0; i < _allowed.Length; i++) if (_allowed[i]) enabled++;
        using (new EditorGUILayout.HorizontalScope()) {
            EditorGUILayout.LabelField($"Allowed Colors  ({enabled} of {_allowed.Length})", EditorStyles.boldLabel);
            if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(44f))) setAllAllowed(true);
            if (GUILayout.Button("None", EditorStyles.miniButtonMid, GUILayout.Width(44f))) setAllAllowed(false);
            using (new EditorGUI.DisabledScope(_preview == null)) {
                if (GUILayout.Button(new GUIContent("Used", "Keep only the colours the current preview actually uses."), EditorStyles.miniButtonRight, GUILayout.Width(44f))) keepPreviewColors();
            }
        }

        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 24f) / SWATCH_CELL_WIDTH));
        for (int start = 0; start < _allowed.Length; start += columns) {
            using (new EditorGUILayout.HorizontalScope()) {
                for (int i = start; i < Mathf.Min(start + columns, _allowed.Length); i++) drawSwatch(i);
                GUILayout.FlexibleSpace();
            }
        }
        if (enabled == 0) EditorGUILayout.HelpBox("No colours enabled — enable at least one.", MessageType.Error);
    }

    void drawSwatch(int index) {
        SandPaletteSO.Slot slot = _palette.slots[index];
        Rect row = GUILayoutUtility.GetRect(SWATCH_CELL_WIDTH, EditorGUIUtility.singleLineHeight + 2f, GUILayout.Width(SWATCH_CELL_WIDTH));

        Rect toggle = new(row.x, row.y + 1f, 16f, EditorGUIUtility.singleLineHeight);
        Rect swatch = new(toggle.xMax + 2f, row.y + 2f, 22f, EditorGUIUtility.singleLineHeight - 2f);
        Rect label = new(swatch.xMax + 4f, row.y + 1f, row.xMax - swatch.xMax - 4f, EditorGUIUtility.singleLineHeight);

        Color32 c = slot.authorColor;
        c.a = 255;
        EditorGUI.DrawRect(new Rect(swatch.x - 1f, swatch.y - 1f, swatch.width + 2f, swatch.height + 2f), new Color(0f, 0f, 0f, 0.6f));
        EditorGUI.DrawRect(swatch, _allowed[index] ? (Color)c : Color.Lerp(c, Color.gray, 0.75f));

        string name = string.IsNullOrEmpty(slot.name) ? SandPaletteSO.hexOf(slot.authorColor) : slot.name;
        GUIContent content = new($"{index + 1}. {name}", $"Slot {index + 1}  {SandPaletteSO.hexOf(slot.authorColor)}");
        _allowed[index] = EditorGUI.Toggle(toggle, _allowed[index]);
        // The swatch and the name toggle too, so the whole cell is one click target.
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
            (swatch.Contains(Event.current.mousePosition) || label.Contains(Event.current.mousePosition))) {
            _allowed[index] = !_allowed[index];
            Event.current.Use();
            Repaint();
        }
        using (new EditorGUI.DisabledScope(!_allowed[index])) EditorGUI.LabelField(label, content);
    }

    void setAllAllowed(bool value) {
        for (int i = 0; i < _allowed.Length; i++) _allowed[i] = value;
    }

    void keepPreviewColors() {
        for (int i = 0; i < _allowed.Length; i++) _allowed[i] = _preview.slotCounts[i + 1] > 0;
    }

    // ---- preview ------------------------------------------------------------------------------

    bool canMap() => _palette != null && !string.IsNullOrEmpty(_sourcePath) && File.Exists(_sourcePath) && _allowed != null && System.Array.IndexOf(_allowed, true) >= 0;

    // Everything the mapped result depends on, including the palette's colours (an Inspector edit
    // must invalidate the preview) and the source file's write time.
    string previewKey() {
        System.Text.StringBuilder key = new();
        key.Append(_sourcePath).Append('|').Append(File.GetLastWriteTimeUtc(_sourcePath).Ticks).Append('|')
           .Append(_palette.GetInstanceID()).Append('|').Append(_resizeMode).Append('|').Append(_widthCells).Append('|');
        for (int i = 0; i < _palette.slotCount; i++)
            key.Append(_allowed[i] ? '1' : '0').Append(SandPaletteSO.packRgb(_palette.slots[i].authorColor)).Append(',');
        return key.ToString();
    }

    void refreshPreview(bool force) {
        string key = previewKey();
        if (!force && key == _previewKey) return;
        _previewKey = key;
        _previewError = null;
        clearPreviewTexture();
        _preview = null;

        if (_source == null || _source.path != _sourcePath || _source.lastWrite != File.GetLastWriteTimeUtc(_sourcePath)) {
            if (!SandPatternImporter.tryLoadSource(_sourcePath, out _source, out _previewError)) return;
        }
        if (!SandPatternImporter.tryMap(_source, _palette, _allowed, _resizeMode, _widthCells, out _preview, out byte[] slots, out _previewError)) {
            _preview = null;
            return;
        }
        _previewTexture = SandPatternImporter.buildPreviewTexture(slots, _preview.width, _preview.height, _palette);
    }

    void drawPreview() {
        if (!string.IsNullOrEmpty(_previewError)) {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox($"Preview: {_previewError}", MessageType.Warning);
            return;
        }
        if (_preview == null || _previewTexture == null) return;

        SandPatternImporter.Result p = _preview;
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{p.width} x {p.height} px (actual output resolution, {cellsText(p)}), {p.colorsUsed} colour(s), {p.resizeMode}", EditorStyles.miniLabel);

        // Integer zoom only, so every output pixel is a crisp square of identical size.
        float available = Mathf.Max(p.width, position.width - 24f);
        int zoom = Mathf.Clamp(Mathf.FloorToInt(available / p.width), 1, 4);
        Rect rect = GUILayoutUtility.GetRect(p.width * zoom, p.height * zoom, GUILayout.Width(p.width * zoom), GUILayout.Height(p.height * zoom));
        drawChecker(rect);
        GUI.DrawTexture(rect, _previewTexture, ScaleMode.StretchToFill, true);

        drawCounts(p);
    }

    static void drawChecker(Rect rect) {
        EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f));
        const float size = 8f;
        Color light = new(0.45f, 0.45f, 0.45f);
        for (float y = 0; y < rect.height; y += size)
            for (float x = ((int)(y / size) % 2) * size; x < rect.width; x += size * 2f)
                EditorGUI.DrawRect(new Rect(rect.x + x, rect.y + y, Mathf.Min(size, rect.width - x), Mathf.Min(size, rect.height - y)), light);
    }

    void clearPreviewTexture() {
        if (_previewTexture != null) DestroyImmediate(_previewTexture);
        _previewTexture = null;
    }

    void clearPreview() {
        clearPreviewTexture();
        _preview = null;
        _previewKey = null;
        _previewError = null;
    }

    // ---- import -------------------------------------------------------------------------------

    void runImport(string outputPath) {
        _error = null;
        _result = null;

        if (File.Exists(outputPath) &&
            !EditorUtility.DisplayDialog("Sand Pattern Importer", $"{outputPath} already exists. Overwrite it?", "Overwrite", "Cancel")) return;

        if (SandPatternImporter.tryImport(_sourcePath, _palette, _allowed, _resizeMode, _widthCells, outputPath, out _result, out _error)) {
            if (_result.width > SandPatternImporter.IMPORTER_MAX_SIZE || _result.height > SandPatternImporter.IMPORTER_MAX_SIZE)
                _error = $"{_result.width}x{_result.height} is larger than the importer's {SandPatternImporter.IMPORTER_MAX_SIZE} cap — Unity downsamples it on import, which breaks exact matching. Scale the source down first.";
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath));
            Debug.Log($"[SandPatternImporter] {Path.GetFileName(_sourcePath)} -> {outputPath}: {_result.colorsUsed} colour(s), {_resizeMode}, {kb(_result.sourceBytes)} -> {kb(_result.outputBytes)}. {_result.verifyMessage}");
        }
    }

    void drawResult() {
        SandPatternImporter.Result r = _result;
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Output", r.outputAssetPath);
        EditorGUILayout.LabelField("Original Resolution", $"{r.sourceWidth} x {r.sourceHeight}");
        EditorGUILayout.LabelField("Output Resolution", $"{r.width} x {r.height}  ({cellsText(r)}, {r.totalPixels:N0} pixels)");
        EditorGUILayout.LabelField("Original File Size", kb(r.sourceBytes));
        EditorGUILayout.LabelField("Output File Size", kb(r.outputBytes));
        EditorGUILayout.LabelField("Size Reduction", $"{100f - 100f * r.outputBytes / Mathf.Max(1, r.sourceBytes):0.##}%");
        EditorGUILayout.LabelField("Empty Pixels", $"{r.emptyPixels:N0}");
        EditorGUILayout.LabelField("Resize", r.resizeMode.ToString());
        EditorGUILayout.LabelField("Colours Used", $"{r.colorsUsed} of {_palette.slotCount}  ({r.bitDepth}-bit indexed PNG)");
        string basis = r.resizeMode == SandPatternImporter.ResizeMode.PaletteMajority ? "source" : "resized";
        EditorGUILayout.LabelField("Already On Palette", $"{r.sourceMatchPercent:0.##}%  ({r.exactPixels:N0} of {r.matchSampled:N0} {basis} sand pixels; the rest were snapped)");
        EditorGUILayout.LabelField("Output Palette Match", r.verified ? "100%" : "FAILED");
        EditorGUILayout.HelpBox(r.verifyMessage, r.verified ? MessageType.Info : MessageType.Error);
        drawCounts(r);
    }

    void drawCounts(SandPatternImporter.Result r) {
        EditorGUILayout.LabelField("Per Colour", EditorStyles.miniBoldLabel);
        for (int s = 1; s < r.slotCounts.Length; s++) {
            if (r.slotCounts[s] == 0) continue;
            string label = _palette.slots[s - 1].name;
            EditorGUILayout.LabelField($"  {s}. {label}", $"{r.slotCounts[s]:N0}  ({100f * r.slotCounts[s] / r.totalPixels:0.##}%)");
        }
    }

    static string cellsText(SandPatternImporter.Result r) =>
        $"{r.widthCells} x {r.height / (float)SandPatternImporter.PIXELS_PER_CELL:0.##} cells";

    // "7 cells x 34 px = 238 px wide", plus the height the current source will get, when known.
    string widthInfo() {
        int width = SandPatternImporter.outputWidthFor(_widthCells);
        string text = $"{_widthCells} cells x {SandPatternImporter.PIXELS_PER_CELL} px = {width} px wide";
        if (_source != null && _source.path == _sourcePath)
            text += $", {SandPatternImporter.outputHeightFor(width, _source.width, _source.height)} px high (from {_source.width} x {_source.height})";
        else
            text += " (height from aspect ratio)";
        return text;
    }

    bool canImport(string outputPath) => canMap() && outputPath != null;

    string outputAssetPath() {
        if (string.IsNullOrEmpty(_outputFolder) || string.IsNullOrEmpty(_outputName)) return null;
        string name = _outputName.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ? _outputName : _outputName + ".png";
        return $"{_outputFolder.TrimEnd('/')}/{name}";
    }

    void setSource(string path) {
        _sourcePath = path;
        if (!string.IsNullOrEmpty(path)) _outputName = SandPatternImporter.defaultOutputName(path);
        _result = null;
        _error = null;
        _source = null;
        clearPreview();
    }

    string sourceBrowseFolder() =>
        !string.IsNullOrEmpty(_sourcePath) && File.Exists(_sourcePath) ? Path.GetDirectoryName(_sourcePath) : Application.dataPath;

    static string fullPathOf(Object asset) => Path.GetFullPath(AssetDatabase.GetAssetPath(asset));

    // Absolute path -> "Assets/..." when it lies inside this project, else null.
    static string toAssetPath(string path) {
        if (string.IsNullOrEmpty(path)) return null;
        string full = Path.GetFullPath(path).Replace('\\', '/');
        string assets = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
        if (full == assets) return "Assets";
        return full.StartsWith(assets + "/", System.StringComparison.Ordinal) ? "Assets" + full.Substring(assets.Length) : null;
    }

    static string kb(long bytes) => bytes < 1024 ? $"{bytes} B" : $"{bytes / 1024f:0.#} KB";
}
