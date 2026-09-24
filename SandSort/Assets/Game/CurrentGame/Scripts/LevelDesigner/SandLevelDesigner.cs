using System.Collections.Generic;
using UnityEngine;

// Sand Level Designer (2026-09-22): Phase 1 scene editing + Phase 2 colour / Save / Load.
//
// A Play-mode authoring tool for the level scene. The Board only exists at runtime (Level.buildBoard
// derives it from the sand), so the designer runs in Play, next to the built Level, and is toggled
// with _toggleKey. While active it:
//  - hides the gameplay Containers (they are re-enabled when the designer is switched off),
//  - shows its own WORKING COPY of the level's ContainerData list — one preview per entry, built
//    from the same canonical Shape prefab + Shape.applyRotation the level itself uses — and
//  - edits that list: create (1), drag to move, R rotate, Q next pool shape, C / Shift+C colour,
//    Delete remove; S saves it back into the SandLevelSO asset, L reloads from the asset.
//
// GRID BLOCKS (2026-09-24): the level's fixed obstacles (SandLevelSO.gridBlocks) are edited in the
// same working list as their own Entry kind (Entry.isBlock): 2 creates the default one
// (Level.gridBlockPrefab, the 1x1) at the hovered cell, drag moves it, Delete / Backspace removes it.
// A Grid Block prefab carries a Shape component, so a block entry is the same ContainerData a piece
// is — data.shape is the block prefab's Shape — and R (rotate) and Q (next shape, through
// _gridBlockPool instead of _shapePool) run the exact code the pieces use. C does not apply: a Grid
// Block has no colour. They count in the overlap validation like any piece, and Save writes them
// into SandLevelSO._gridBlocks (prefab + position + rotation).
//
// What it edits is the ContainerData representation (shape / position / rotation / color), on a
// copy: the SandLevelSO asset is READ at activation and written ONLY by S (Editor only, through
// Undo + SerializedObject, see save). The running Level is never rebuilt from it — reopening the
// level is how a save shows up in play. Sand physics, extraction, Container gameplay and the Shape
// prefabs are never touched — previews are plain Shape instances with a Container-style colour
// material and no Container, ExtractionGrid, fill or readout on them.
//
// Colours come from Level._sandPaletteColors, restricted to those with a ColorSO on the Level prefab,
// so a designed piece can never name a colour the level cannot draw. Save REFUSES a working list
// with any off-board / overlapping piece or any colour outside that set (an entry loaded from an
// older asset can carry one): an invalid level is never written, not even with a warning.
//
// PLACEMENT IS UNRESTRICTED WHILE EDITING (2026-09-22): create, drag, R and Q always apply, even
// when the result overlaps another piece or sticks out of the Board — a piece in that state is only
// tinted red (Entry.valid) and blocks Save until it is moved. The Board's gameplay rules
// (isInBounds / isAreaFree) are untouched; they simply are not enforced on the working copy.
//
// SAND TEXTURE COLOUR DEBUGGER (2026-09-22): the assignable colours are further restricted to the
// ones that actually occur in the sand texture (_sandTexture, or the loaded level's own PNG), so a
// piece can only be given a colour it can be filled with. The texture is read the way the level
// reads it — unique opaque pixel colours matched to SandPaletteSO's author colours, slot ->
// ItemColor through Level._sandPaletteColors — with one difference: a per-channel tolerance
// (_colorTolerance) so an off-palette pixel is still attributed to its nearest slot and reported as
// "approx"; the loader itself matches EXACTLY and will refuse such a texture. The result is shown in
// this component's Inspector (SandLevelDesignerEditor): the texture's colours with a swatch, their
// ColorSO and how many pieces of each are placed. Before Play the Inspector analyses the assigned
// texture against the Level PREFAB's palette; while designing it shows the live analysis.
//
// "Shape Pool" = the canonical prefabs under Prefabs/Shapes/ (Shape_1x1 ... Shape_Plus5). There is
// no pool asset in the project, so the designer carries the list itself (_shapePool), filled from
// that folder in the Editor (Reset / context menu) and ordered by ShapeType.
//
// Not readonly anywhere on purpose (mid-Play domain reload skips readonly fields — see Container).
public class SandLevelDesigner : MonoBehaviour {

    const string SHAPE_PREFAB_FOLDER = "Assets/Game/CurrentGame/Prefabs/Shapes";
    const string GRID_BLOCK_PREFAB_FOLDER = "Assets/Game/CurrentGame/Prefabs/GridBlocks";
    // The Level prefab: its SandPaletteSO, slot -> ItemColor mapping and ColorSO list are what the
    // Inspector analyses a texture against before a Level exists in the scene.
    public const string LEVEL_PREFAB_PATH = "Assets/Game/CurrentGame/Prefabs/Level.prefab";
    // The colour the piece is drawn with, on the ColorSO (TCP2) materials — same property Shape reads.
    static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
    static readonly int COLOR_ID = Shader.PropertyToID("_Color");
    static readonly List<Vector2Int> SINGLE_CELL = new() { Vector2Int.zero };
    // Board-local Z of the hover cursor: in front of the floor (Board's FLOOR_DEPTH_OFFSET = 0.2)
    // but behind the pieces (whose front face is at -0.5), so it shows on empty cells only.
    const float CURSOR_DEPTH = 0.15f;
    const float CURSOR_SIZE = 0.92f;

    [Header("Activation")]
    [Tooltip("Toggles the designer while playing. Only works in the Editor or a development build.")]
    [SerializeField] KeyCode _toggleKey = KeyCode.F2;
    [Tooltip("Switch the designer on as soon as a Level is built, instead of waiting for the toggle key.")]
    [SerializeField] bool _startActive;

    [Header("Authoring")]
    [Tooltip("Colour given to a Shape created with the 1 key. Changing colour is not part of Phase 1.")]
    [SerializeField] ColorSO.ItemColor _defaultColor = ColorSO.ItemColor.RED;
    [Tooltip("The Shape Pool: the canonical Shape prefabs, in the order Q cycles through them. 1 creates the first one. Filled from Prefabs/Shapes by Reset or the 'Fill Shape Pool From Project' context menu.")]
    [SerializeField] List<Shape> _shapePool = new();
    [Tooltip("The Grid Block pool: one prefab per shape under Prefabs/GridBlocks, in the order Q cycles a selected Grid Block through them. Filled by Reset or the 'Fill Shape Pool From Project' context menu. Empty = Q has only the Level prefab's default Grid Block.")]
    [SerializeField] List<GridBlock> _gridBlockPool = new();

    [Header("Sand Texture Colour Debugger")]
    [Tooltip("The sand texture to analyse. Empty = the loaded level's own sand texture (SandLevelSO.sandTexture). Only colours found in it (that also have a ColorSO) can be assigned with C / Shift+C or 1.")]
    [SerializeField] Texture2D _sandTexture;
    [Tooltip("Max per-channel RGB difference (0-255) for a texture colour to count as one of the SandPaletteSO's slot colours. The level loader matches EXACTLY, so anything that only matches through this tolerance is listed as 'approx' — the level will refuse to load that texture until the pixel is fixed.")]
    [Range(0, 64)]
    [SerializeField] int _colorTolerance = 8;

    // One authored piece: its working ContainerData plus the preview drawn for it.
    class Entry {
        public ContainerData data;
        public Shape instance;
        // A Grid Block rather than a piece. data.shape is then the Grid Block prefab's Shape and
        // data.color is unused; position, rotation and the footprint work exactly as for a piece.
        public bool isBlock;
        public string label => isBlock ? $"GridBlock {data.shape.type}" : data.shape.type.ToString();
        // Per-preview copy of the ColorSO material, so hover/selected/invalid tints never write into
        // the shared asset. Owned here, destroyed with the preview.
        public Material material;
        public Color baseColor;
        // The renderers the colour goes on, and what they were drawing before (the prefab's own
        // M_Shape) — the fallback when a colour has no ColorSO material.
        public Renderer[] renderers = System.Array.Empty<Renderer>();
        public Material fallbackMaterial;
        public bool valid = true;
        // False when data.color has no ColorSO on the Level prefab. Only an entry loaded from the
        // asset can be in this state — C never assigns such a colour — and Save refuses it.
        public bool colorValid = true;
    }

    List<Entry> _entries = new();
    List<GameObject> _hiddenContainers = new();
    // The colours C cycles through: Level._sandPaletteColors, minus any without a ColorSO, minus any
    // the analysed sand texture does not contain (see rebuildColorChoices).
    List<ColorSO.ItemColor> _colorChoices = new();

    // One gameplay colour the analysed texture contains. Read by SandLevelDesignerEditor.
    public class TextureColor {
        public ColorSO.ItemColor color;
        // The ColorSO on the Level prefab for this colour, null when there is none.
        public ColorSO colorSO;
        public bool hasColorSO;
        // The palette slot's author colour — what the PNG is meant to contain for this colour.
        public Color32 swatch;
        public int pixels;
        // Every texture colour attributed to it matched its palette slot exactly (what the loader
        // needs). False = at least one was only within _colorTolerance.
        public bool exact = true;
        public List<string> hexes = new();
    }

    List<TextureColor> _textureColors = new();
    // Texture colours no palette slot is within tolerance of: "#RRGGBB xN".
    List<string> _unmatchedTextureColors = new();
    string _textureReport = "";
    bool _textureAnalysed;
    Texture2D _analysedTexture;

    public IReadOnlyList<TextureColor> textureColors => _textureColors;
    public IReadOnlyList<string> unmatchedTextureColors => _unmatchedTextureColors;
    public string textureReport => _textureReport;
    public bool textureAnalysed => _textureAnalysed;
    public Texture2D analysedTexture => _analysedTexture;
    public IReadOnlyList<ColorSO.ItemColor> colorChoices => _colorChoices;
    // Set by OnValidate (texture / tolerance edited in the Inspector during Play) — re-analysed on
    // the next Update rather than inside OnValidate.
    bool _reanalyseTexture;
    // Working list differs from the asset since the last Save / Load.
    bool _dirty;
    // Legacy (shape-less) entries the asset had and this tool does not carry — Save drops them.
    int _skippedLegacy;

    // Q's list for Grid Block entries: _gridBlockPool as Shapes (or just the Level's default block).
    List<Shape> _gridBlockShapes = new();

    Level _level;
    Board _board;
    SandLevelSO _levelSO;
    Transform _previewRoot;
    Transform _cursor;
    Material _cursorMaterial;

    bool _active;
    bool _activationAnnounced;
    Entry _selected;
    Entry _hovered;
    bool _dragging;
    // Cell the pointer grabbed, relative to the entry's anchor, so a piece grabbed off its anchor
    // does not jump under the pointer.
    Vector2Int _grabOffset;
    Vector2Int _dragStartPosition;
    Vector2Int _hoverCell;
    bool _hoverCellValid;
    string _lastMessage = "";

    public bool isActive => _active;
    // The working copy Phase 2 (Save) will write back into the SandLevelSO. Read-only here.
    public IReadOnlyList<ContainerData> entries {
        get {
            List<ContainerData> list = new(_entries.Count);
            foreach (Entry entry in _entries) if (!entry.isBlock) list.Add(entry.data);
            return list;
        }
    }

    bool available => Application.isEditor || Debug.isDebugBuild;

    void Update() {
        if (!available) return;

        if (Input.GetKeyDown(_toggleKey)) {
            if (_active) deactivate();
            else activate();
        }

        if (!_active) {
            if (_startActive && !_activationAnnounced && FindFirstObjectByType<Level>() != null) {
                _activationAnnounced = true;
                activate();
            }
            return;
        }

        // The Level was rebuilt (restart / next level): the previews went with the old Board.
        if (_level == null || _board == null) {
            deactivate();
            return;
        }

        if (_reanalyseTexture) {
            _reanalyseTexture = false;
            analyseTexture(designTexture, _level);
            rebuildColorChoices();
        }

        updateHover();
        handleMouse();
        handleKeys();
        refreshTints();
    }

    // -- Activation ---------------------------------------------------------------------------

    void activate() {
        _level = FindFirstObjectByType<Level>();
        if (_level == null) {
            log("No Level in the scene — start the level first.");
            return;
        }
        _board = _level.GetComponentInChildren<Board>();
        _levelSO = _level.levelSO as SandLevelSO;
        if (_board == null || _levelSO == null) {
            log("Level has no Board / SandLevelSO yet — cannot design.");
            _level = null;
            return;
        }
        if (_shapePool.Count == 0) {
            log("Shape Pool is empty — fill it on the SandLevelDesigner component (Reset / context menu).");
            _level = null;
            return;
        }

        _active = true;
        _selected = null;
        _hovered = null;
        _dragging = false;
        _dirty = false;
        _skippedLegacy = 0;

        analyseTexture(designTexture, _level);
        rebuildColorChoices();
        if (_colorChoices.Count == 0) {
            log("The Level prefab has no ColorSO for any of its sand palette colours — nothing valid to assign, cannot design.");
            _active = false;
            _level = null;
            return;
        }

        // Gameplay pieces (and Grid Blocks) out of the way; remembered so only what we hid is put back.
        _hiddenContainers.Clear();
        foreach (Container container in _level.GetComponentsInChildren<Container>(false)) {
            container.gameObject.SetActive(false);
            _hiddenContainers.Add(container.gameObject);
        }
        foreach (GridBlock block in _level.GetComponentsInChildren<GridBlock>(false)) {
            block.gameObject.SetActive(false);
            _hiddenContainers.Add(block.gameObject);
        }

        GameObject root = new GameObject("SandLevelDesigner_Previews");
        root.transform.SetParent(_board.transform, false);
        _previewRoot = root.transform;
        buildCursor();

        // Working copies — the asset's own ContainerData objects are never handed to the previews.
        _entries.Clear();
        foreach (ContainerData source in _levelSO.containers) {
            if (source.shape == null) {
                _skippedLegacy++;
                log($"Skipping a legacy container at {source.position} ({source.color}): it has no Shape prefab and the designer only edits Shape-based entries. A Save will NOT keep it.");
                continue;
            }
            Entry entry = new Entry {
                data = new ContainerData {
                    position = source.position,
                    shape = source.shape,
                    rotation = source.rotation,
                    color = source.color,
                    cells = new List<Vector2Int>(source.cells),
                },
            };
            _entries.Add(entry);
            buildPreview(entry);
        }
        rebuildGridBlockShapes();
        int blockCount = 0;
        foreach (GridBlockData source in _levelSO.gridBlocks) {
            GridBlock prefab = source.block != null ? source.block : _level.gridBlockPrefab;
            if (prefab == null) {
                log($"Skipping the Grid Block at {source.position}: it names no prefab and the Level prefab has no default Grid Block. A Save will NOT keep it.");
                continue;
            }
            Entry entry = newBlockEntry(source.position, prefab.shape, source.rotation);
            _entries.Add(entry);
            buildPreview(entry);
            blockCount++;
        }
        revalidateAll();
        int badColors = 0;
        foreach (Entry entry in _entries) if (!entry.colorValid) badColors++;
        log($"Designer ON — {_entries.Count - blockCount} piece(s) + {blockCount} Grid Block(s) from {_levelSO.name}, {_colorChoices.Count} colour(s) available"
            + (badColors > 0 ? $", {badColors} piece(s) with a colour that has no ColorSO (orange — recolour before saving)" : "")
            + $". 1 create · 2 Grid Block · drag move · R rotate · Q next shape · C colour · Delete/Backspace remove · S save · L reload · {_toggleKey} exit.");
    }

    void deactivate() {
        if (_dirty) log("Designer OFF with UNSAVED changes — they are discarded (S saves, L reloads).");
        _dirty = false;
        foreach (Entry entry in _entries) destroyPreview(entry);
        _entries.Clear();

        if (_previewRoot != null) Destroy(_previewRoot.gameObject);
        _previewRoot = null;
        _cursor = null;
        if (_cursorMaterial != null) Destroy(_cursorMaterial);
        _cursorMaterial = null;

        foreach (GameObject hidden in _hiddenContainers) {
            if (hidden != null) hidden.SetActive(true);
        }
        _hiddenContainers.Clear();

        _active = false;
        _selected = null;
        _hovered = null;
        _dragging = false;
        _level = null;
        _board = null;
        _levelSO = null;
        log("Designer OFF — gameplay pieces restored.");
    }

    // L: throw the working copy away and read the asset again. Same code path as F2 off/on, minus
    // the "unsaved changes" warning, since discarding is the point.
    void reload() {
        _dirty = false;
        deactivate();
        activate();
        log("Reloaded from the asset — working changes discarded.");
    }

    // What 1 paints with: the Inspector default when the level can draw it, else the first colour
    // the level can.
    ColorSO.ItemColor defaultColor => _colorChoices.Contains(_defaultColor) ? _defaultColor : _colorChoices[0];

    // -- Sand texture colour debugger ---------------------------------------------------------

    // The texture being designed against: the Inspector override, else the loaded level's own PNG.
    Texture2D designTexture => _sandTexture != null ? _sandTexture : (_levelSO != null ? _levelSO.sandTexture : null);

#if UNITY_EDITOR
    // For the Inspector when NOT designing: analyses the assigned texture against the Level in the
    // scene if there is one, else against the Level PREFAB asset (its palette, mapping and ColorSO
    // list are serialized data, so the accessors work on the asset). While designing, the live
    // analysis is already there and this is a no-op.
    public void analyseForInspector() {
        if (_active) return;
        Level level = FindFirstObjectByType<Level>();
        if (level == null) level = UnityEditor.AssetDatabase.LoadAssetAtPath<Level>(LEVEL_PREFAB_PATH);
        Texture2D texture = _sandTexture;
        if (texture == null && level != null && level.levelSO is SandLevelSO so) texture = so.sandTexture;
        analyseTexture(texture, level);
        if (texture == null && level != null) {
            _textureReport = Application.isPlaying
                ? "No sand texture: the Sand Texture field is empty and the loaded level has no PNG."
                : "Assign a Sand Texture to preview its colours before Play. While designing, the loaded level's own PNG is used automatically.";
        }
    }
#endif

    // Reads the texture the way the level does (SandPatternTextureConverter.tryReadSlots): opaque
    // pixels only (alpha >= ALPHA_SAND_THRESHOLD), RGB looked up in the SandPaletteSO's author
    // colours, slot i -> Level.sandPaletteColors[i]. Unlike the loader it is tolerant — the nearest
    // slot within _colorTolerance per channel counts, and is reported as not exact — so an
    // anti-aliased edge still shows up under the colour it was meant to be instead of vanishing.
    void analyseTexture(Texture2D texture, Level level) {
        _textureColors.Clear();
        _unmatchedTextureColors.Clear();
        _textureAnalysed = false;
        _analysedTexture = texture;

        if (level == null) { _textureReport = $"No Level to analyse against (none in the scene, and {LEVEL_PREFAB_PATH} did not load)."; return; }
        SandPaletteSO palette = level.sandPalette;
        if (texture == null) { _textureReport = "No sand texture: the Sand Texture field is empty and the level has no PNG (hand-painted pattern levels are not analysed)."; return; }
        if (palette == null) { _textureReport = "The Level prefab has no SandPaletteSO — cannot tell which pixel is which slot."; return; }

        Color32[] pixels;
        try {
            pixels = texture.GetPixels32();
        } catch (UnityException e) {
            _textureReport = $"Cannot read '{texture.name}' — enable Read/Write on its import settings. ({e.Message})";
            return;
        }

        // Unique opaque colours with their pixel counts.
        Dictionary<int, int> countByRgb = new();
        foreach (Color32 pixel in pixels) {
            if (pixel.a < SandPaletteSO.ALPHA_SAND_THRESHOLD) continue;
            int key = SandPaletteSO.packRgb(pixel);
            countByRgb[key] = countByRgb.TryGetValue(key, out int count) ? count + 1 : 1;
        }

        IReadOnlyList<SandPaletteSO.Slot> slots = palette.slots;
        IReadOnlyList<ColorSO.ItemColor> mapping = level.sandPaletteColors;
        Dictionary<ColorSO.ItemColor, TextureColor> byColor = new();

        foreach (KeyValuePair<int, int> pair in countByRgb) {
            Color32 rgb = new Color32((byte)(pair.Key >> 16), (byte)(pair.Key >> 8), (byte)pair.Key, 255);
            string hex = SandPaletteSO.hexOf(rgb);

            int best = -1;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < slots.Count; i++) {
                Color32 author = slots[i].authorColor;
                int distance = Mathf.Max(Mathf.Abs(rgb.r - author.r), Mathf.Max(Mathf.Abs(rgb.g - author.g), Mathf.Abs(rgb.b - author.b)));
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }

            if (best < 0 || bestDistance > _colorTolerance) {
                _unmatchedTextureColors.Add($"{hex} x{pair.Value}");
                continue;
            }
            if (best >= mapping.Count) {
                _unmatchedTextureColors.Add($"{hex} x{pair.Value} (slot {best + 1} has no ItemColor on the Level prefab)");
                continue;
            }

            ColorSO.ItemColor item = mapping[best];
            if (!byColor.TryGetValue(item, out TextureColor info)) {
                ColorSO colorSO = level.containerColorOf(item);
                info = new TextureColor { color = item, colorSO = colorSO, hasColorSO = colorSO != null, swatch = slots[best].authorColor };
                byColor[item] = info;
                _textureColors.Add(info);
            }
            info.pixels += pair.Value;
            if (bestDistance > 0) info.exact = false;
            info.hexes.Add(bestDistance > 0 ? $"{hex}~" : hex);
        }

        _textureColors.Sort((a, b) => b.pixels.CompareTo(a.pixels));
        _textureAnalysed = true;
        _textureReport = $"'{texture.name}' {texture.width}x{texture.height} ({(_sandTexture != null ? "designer override" : "level's own")}), tolerance {_colorTolerance}";
    }

    // The assignable set, in palette slot order: has a ColorSO AND occurs in the texture. With no
    // usable texture the texture restriction is dropped (loudly) so the tool stays usable.
    void rebuildColorChoices() {
        _colorChoices.Clear();
        foreach (ColorSO.ItemColor color in _level.sandPaletteColors) {
            if (_colorChoices.Contains(color) || !_level.hasContainerColor(color)) continue;
            if (_textureAnalysed && textureColorOf(color) == null) continue;
            _colorChoices.Add(color);
        }

        if (!_textureAnalysed) {
            log($"Texture not analysed ({_textureReport}) — C / 1 fall back to every palette colour with a ColorSO.");
        } else if (_colorChoices.Count == 0) {
            log("None of the texture's colours has a ColorSO — C / 1 fall back to every palette colour with a ColorSO.");
            foreach (ColorSO.ItemColor color in _level.sandPaletteColors) {
                if (!_colorChoices.Contains(color) && _level.hasContainerColor(color)) _colorChoices.Add(color);
            }
        }
    }

    public TextureColor textureColorOf(ColorSO.ItemColor color) {
        foreach (TextureColor info in _textureColors) if (info.color == color) return info;
        return null;
    }

    // Pieces in the working list carrying this colour. 0 when not designing.
    public int placedCountOf(ColorSO.ItemColor color) {
        int count = 0;
        foreach (Entry entry in _entries) if (!entry.isBlock && entry.data.color == color) count++;
        return count;
    }

    void OnValidate() {
        if (_active) _reanalyseTexture = true;
    }

    void OnDisable() {
        if (_active) deactivate();
    }

    // -- Previews -----------------------------------------------------------------------------

    // Same recipe as Level.buildContainers for the visual half only: the canonical prefab,
    // Shape.applyRotation for the orientation, the board's cell size, the Container colour material
    // on the FBX renderers. No Container, no ExtractionGrid, no fill.
    // A Grid Block goes through the same steps (its prefab's Shape is data.shape) and differs only
    // in its material, see applyBlockMaterial.
    void buildPreview(Entry entry) {
        ContainerData data = entry.data;
        Shape instance = Instantiate(data.shape, _previewRoot, false);
        instance.name = entry.isBlock ? $"Design_GridBlock_{data.shape.type}" : $"Design_{data.color}_{data.shape.type}";
        instance.applyRotation(data.rotation);
        instance.setCellWorldSize(_board.cellSize);
        entry.instance = instance;

        // The readout belongs to gameplay fill; a design preview has none.
        if (instance.fillPercentageUI != null) instance.fillPercentageUI.gameObject.SetActive(false);
        else if (instance.fillPercentageText != null) instance.fillPercentageText.gameObject.SetActive(false);

        entry.renderers = instance.fbxPlaceholder != null
            ? instance.fbxPlaceholder.GetComponentsInChildren<Renderer>(true)
            : instance.GetComponentsInChildren<Renderer>(true);
        entry.fallbackMaterial = entry.renderers.Length > 0 ? entry.renderers[0].sharedMaterial : null;
        if (entry.isBlock) applyBlockMaterial(entry);
        else applyColorMaterial(entry);

        placePreview(entry);
    }

    // A Grid Block keeps its prefab's own material (no colour system), on a per-preview copy so the
    // hover / selected / invalid tints stay off the shared asset. The preview is never
    // GridBlock.initialize'd — that would block Board cells, and the designer never touches the
    // Board's occupancy.
    void applyBlockMaterial(Entry entry) {
        entry.colorValid = true;
        if (entry.fallbackMaterial == null) return;
        entry.material = new Material(entry.fallbackMaterial) { name = "DesignerGridBlock (runtime)" };
        entry.baseColor = readColor(entry.material);
        foreach (Renderer renderer in entry.renderers) renderer.sharedMaterial = entry.material;
    }

    Entry newBlockEntry(Vector2Int position, Shape blockShape, ShapeRotation rotation) {
        return new Entry {
            isBlock = true,
            data = new ContainerData { position = position, shape = blockShape, rotation = rotation },
        };
    }

    // Q's cycle for Grid Blocks: the pool's Shapes, or the Level's default block alone when the pool
    // is empty (Q is then a no-op). The default is always in it so a block created with 2 is found.
    void rebuildGridBlockShapes() {
        _gridBlockShapes.Clear();
        foreach (GridBlock block in _gridBlockPool) {
            if (block != null && block.shape != null && !_gridBlockShapes.Contains(block.shape)) _gridBlockShapes.Add(block.shape);
        }
        if (_level.gridBlockPrefab != null && !_gridBlockShapes.Contains(_level.gridBlockPrefab.shape)) {
            _gridBlockShapes.Insert(0, _level.gridBlockPrefab.shape);
        }
    }

    // (Re)builds the preview's colour material from data.color: the Container's ColorSO material,
    // or the prefab's own material when the colour has none (which also marks the entry as not
    // saveable). Called on build and again on every C.
    void applyColorMaterial(Entry entry) {
        entry.colorValid = _level.hasContainerColor(entry.data.color);
        if (entry.renderers.Length == 0) return;

        Material source = _level.containerMaterialOf(entry.data.color);
        if (source == null) source = entry.fallbackMaterial;
        if (entry.material != null) Destroy(entry.material);
        entry.material = new Material(source) { name = "DesignerPreview (runtime)" };
        entry.baseColor = readColor(entry.material);
        foreach (Renderer renderer in entry.renderers) renderer.sharedMaterial = entry.material;
    }

    void destroyPreview(Entry entry) {
        if (entry.instance != null) Destroy(entry.instance.gameObject);
        if (entry.material != null) Destroy(entry.material);
        entry.instance = null;
        entry.material = null;
        entry.renderers = System.Array.Empty<Renderer>();
        entry.fallbackMaterial = null;
    }

    // Grid snapping of the visual: the anchor cell's world centre, exactly where Container puts its
    // root (Board.anchorToWorldCenter; the shape argument is unused there).
    void placePreview(Entry entry) {
        if (entry.instance == null) return;
        entry.instance.transform.position = _board.anchorToWorldCenter(entry.data.position, null);
    }

    void buildCursor() {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "HoverCursor";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(_previewRoot, false);
        quad.transform.localScale = new Vector3(_board.cellSize * CURSOR_SIZE, _board.cellSize * CURSOR_SIZE, 1f);
        _cursor = quad.transform;

        Renderer renderer = quad.GetComponent<Renderer>();
        Material source = _level.containerMaterialOf(defaultColor);
        _cursorMaterial = new Material(source != null ? source : renderer.sharedMaterial) { name = "DesignerCursor (runtime)" };
        writeColor(_cursorMaterial, Color.Lerp(readColor(_cursorMaterial), Color.white, 0.5f));
        renderer.sharedMaterial = _cursorMaterial;
        quad.SetActive(false);
    }

    // -- Pointer ------------------------------------------------------------------------------

    // Pointer -> board cell: the pointer ray hits the Board's plane (the same plane Container drags
    // on), Board.worldToAnchorPoint turns that into fractional cell units, and rounding snaps it to
    // the nearest cell. May be outside the board — callers check.
    bool tryPointerCell(out Vector2Int cell) {
        cell = default;
        Camera camera = Camera.main;
        if (camera == null) return false;

        Plane plane = new Plane(_board.transform.forward, _board.transform.position);
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (!plane.Raycast(ray, out float distance)) return false;

        Vector2 anchor = _board.worldToAnchorPoint(ray.GetPoint(distance), null);
        cell = new Vector2Int(Mathf.RoundToInt(anchor.x), Mathf.RoundToInt(anchor.y));
        return true;
    }

    void updateHover() {
        _hoverCellValid = tryPointerCell(out _hoverCell) && _board.isInBounds(_hoverCell, SINGLE_CELL);
        _hovered = _hoverCellValid ? entryAt(_hoverCell) : null;

        if (_cursor != null) {
            bool show = _hoverCellValid && !_dragging;
            _cursor.gameObject.SetActive(show);
            if (show) {
                _cursor.localPosition = _board.cellToLocalPosition(_hoverCell) + new Vector3(0f, 0f, CURSOR_DEPTH);
            }
        }
    }

    void handleMouse() {
        if (Input.GetMouseButtonDown(0)) {
            if (_hovered != null) {
                _selected = _hovered;
                _dragging = true;
                _grabOffset = _hoverCell - _selected.data.position;
                _dragStartPosition = _selected.data.position;
            } else if (_hoverCellValid) {
                _selected = null;
            }
        }

        if (!_dragging) return;

        if (_selected == null || _selected.instance == null) {
            _dragging = false;
            return;
        }

        if (tryPointerCell(out Vector2Int cell)) {
            Vector2Int wanted = cell - _grabOffset;
            if (wanted != _selected.data.position) {
                _selected.data.position = wanted;
                placePreview(_selected);
                revalidateAll();
            }
        }

        if (!Input.GetMouseButton(0)) {
            _dragging = false;
            // The drop is kept wherever it landed; an off-board / overlapping piece stays red and
            // blocks Save, it is never moved back.
            if (_selected.data.position != _dragStartPosition) {
                _dirty = true;
                if (!_selected.valid) log($"Dropped at {_selected.data.position} (off-board or overlapping) — fix before saving.");
            }
        }
    }

    // Keys act on the piece under the pointer; with nothing under it, on the selected piece.
    // S and L act on the whole working list and need no piece.
    void handleKeys() {
        if (Input.GetKeyDown(KeyCode.S)) {
            save();
            return;
        }
        if (Input.GetKeyDown(KeyCode.L)) {
            reload();
            return;
        }

        // 1 creates at the hovered cell whether or not a piece is already there; the anchor cell
        // itself must be on the Board (that is what the pointer tracks), the footprint may not be.
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) {
            if (_hoverCellValid) createAt(_hoverCell);
            else log("1: point at a Board cell to create there.");
            return;
        }
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) {
            if (_level.gridBlockPrefab == null) log("2: the Level prefab has no Grid Block prefab assigned.");
            else if (_hoverCellValid) createBlockAt(_hoverCell);
            else log("2: point at a Board cell to place a Grid Block there.");
            return;
        }

        Entry target = _hovered ?? _selected;
        if (target == null || _dragging) return;

        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) {
            remove(target);
            return;
        }
        if (Input.GetKeyDown(KeyCode.R)) rotate(target);
        else if (Input.GetKeyDown(KeyCode.Q)) nextShape(target);
        else if (Input.GetKeyDown(KeyCode.C)) {
            // A Grid Block has no colour.
            if (target.isBlock) log("Grid Blocks have no colour — R rotates, Q changes the shape.");
            else cycleColor(target, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
        }
    }

    // -- Edits --------------------------------------------------------------------------------

    void createAt(Vector2Int cell) {
        Entry entry = new Entry {
            data = new ContainerData {
                position = cell,
                shape = _shapePool[0],
                rotation = ShapeRotation.Deg0,
                color = defaultColor,
            },
        };
        _entries.Add(entry);
        buildPreview(entry);
        _selected = entry;
        revalidateAll();
        _dirty = true;
        log($"Created {entry.data.shape.type} ({entry.data.color}) at {cell}."
            + (entry.valid ? "" : " It is off-board or overlapping — fix before saving."));
    }

    // 2: a Grid Block at the hovered cell. Like 1, an occupied cell is allowed — the block is then
    // red and blocks Save until one of the two is moved.
    void createBlockAt(Vector2Int cell) {
        Entry entry = newBlockEntry(cell, _level.gridBlockPrefab.shape, ShapeRotation.Deg0);
        _entries.Add(entry);
        buildPreview(entry);
        _selected = entry;
        revalidateAll();
        _dirty = true;
        log($"Created a Grid Block at {cell}." + (entry.valid ? "" : " The cell is taken — fix before saving."));
    }

    // C / Shift+C: next / previous colour in _colorChoices — the level's sand palette colours that
    // have a ColorSO. A colour outside that set (only possible on an entry loaded from an older
    // asset) steps into the set at its first / last colour. Position, shape and rotation untouched;
    // colour never changes the footprint, so no re-validation is needed.
    void cycleColor(Entry entry, int direction) {
        ColorSO.ItemColor previous = entry.data.color;
        int index = _colorChoices.IndexOf(previous);
        int count = _colorChoices.Count;
        int nextIndex = index < 0
            ? (direction > 0 ? 0 : count - 1)
            : (index + direction + count) % count;
        ColorSO.ItemColor next = _colorChoices[nextIndex];
        if (next == previous) return;

        entry.data.color = next;
        applyColorMaterial(entry);
        _dirty = true;
        log($"Colour {previous} -> {next} ({entry.data.shape.type} at {entry.data.position}, {entry.data.rotation} kept).");
    }

    // 0 -> 90 -> 180 -> 270 -> 0, through Shape.applyRotation on the existing instance (absolute,
    // so repeated turns never compound). Position, shape and colour untouched.
    void rotate(Entry entry) {
        ShapeRotation previous = entry.data.rotation;
        ShapeRotation next = (ShapeRotation)(((int)previous + 1) & 3);
        entry.data.rotation = next;
        entry.instance.applyRotation(next);
        revalidateAll();
        _dirty = true;
        log($"Rotation {previous} -> {next}." + (entry.valid ? "" : " Now off-board or overlapping — fix before saving."));
    }

    // Swaps the canonical prefab for the next one in the pool and rebuilds the preview. Position,
    // rotation and colour are left exactly as they were (the same ContainerData object, three fields
    // untouched); only `shape` changes.
    // A Grid Block cycles through _gridBlockShapes instead of the piece pool; everything else is the
    // same (its colour field is simply unused).
    void nextShape(Entry entry) {
        List<Shape> pool = entry.isBlock ? _gridBlockShapes : _shapePool;
        Shape previous = entry.data.shape;
        int index = pool.IndexOf(previous);
        Shape next = pool[(index + 1) % pool.Count];
        if (next == previous) return;

        entry.data.shape = next;
        destroyPreview(entry);
        buildPreview(entry);
        revalidateAll();
        _dirty = true;
        log($"{(entry.isBlock ? "Grid Block" : "Shape")} {previous.type} -> {next.type} (position {entry.data.position}, {entry.data.rotation}{(entry.isBlock ? "" : $", {entry.data.color}")} kept)."
            + (entry.valid ? "" : " Now off-board or overlapping — fix before saving."));
    }

    void remove(Entry entry) {
        destroyPreview(entry);
        _entries.Remove(entry);
        if (_selected == entry) _selected = null;
        if (_hovered == entry) _hovered = null;
        revalidateAll();
        _dirty = true;
        log($"Removed {entry.label} at {entry.data.position}.");
    }

    // -- Save ---------------------------------------------------------------------------------

    // S: the working list replaces SandLevelSO._containers, in working order. Editor only: it is an
    // asset write. Refused outright — nothing written — when any piece is off-board / overlapping
    // or carries a colour without a ColorSO, so the asset can never hold a level this tool knows to
    // be invalid. Written through Undo + SerializedObject so it is undoable and goes through
    // Unity's own serialization (the asset's ContainerData caches are rebuilt from shape + rotation
    // on the next read, see ContainerData.occupiedCells). The running Level is NOT rebuilt.
    void save() {
#if UNITY_EDITOR
        if (_levelSO == null || !UnityEditor.AssetDatabase.Contains(_levelSO)) {
            log("Save refused: the level's SandLevelSO is not a project asset.");
            return;
        }

        List<string> problems = new();
        for (int i = 0; i < _entries.Count; i++) {
            ContainerData data = _entries[i].data;
            string piece = $"#{i} {_entries[i].label} at {data.position}";
            if (!_entries[i].valid) problems.Add($"{piece} is off-board or overlapping");
            if (!_entries[i].colorValid) problems.Add($"{piece} has colour {data.color}, which has no ColorSO on the Level prefab");
        }
        if (problems.Count > 0) {
            log($"Save refused ({problems.Count} problem(s)): {string.Join("; ", problems)}.");
            return;
        }

        UnityEditor.Undo.RecordObject(_levelSO, "Save Sand Level");
        UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(_levelSO);
        List<ContainerData> pieces = new();
        List<ContainerData> blocks = new();
        foreach (Entry entry in _entries) {
            if (entry.isBlock) blocks.Add(entry.data);
            else pieces.Add(entry.data);
        }

        UnityEditor.SerializedProperty gridBlocks = serialized.FindProperty("_gridBlocks");
        gridBlocks.arraySize = blocks.Count;
        for (int i = 0; i < blocks.Count; i++) {
            UnityEditor.SerializedProperty element = gridBlocks.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("position").vector2IntValue = blocks[i].position;
            element.FindPropertyRelative("block").objectReferenceValue = blocks[i].shape.GetComponent<GridBlock>();
            element.FindPropertyRelative("rotation").intValue = (int)blocks[i].rotation;
        }

        UnityEditor.SerializedProperty containers = serialized.FindProperty("_containers");
        containers.arraySize = pieces.Count;
        for (int i = 0; i < pieces.Count; i++) {
            ContainerData data = pieces[i];
            UnityEditor.SerializedProperty element = containers.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("position").vector2IntValue = data.position;
            element.FindPropertyRelative("shape").objectReferenceValue = data.shape;
            element.FindPropertyRelative("rotation").intValue = (int)data.rotation;
            element.FindPropertyRelative("color").intValue = (int)data.color;
            // Not authoritative once a Shape is set (see ContainerData.cells) — carried through
            // unchanged so a save is never the thing that alters it.
            UnityEditor.SerializedProperty cells = element.FindPropertyRelative("cells");
            cells.arraySize = data.cells.Count;
            for (int j = 0; j < data.cells.Count; j++) cells.GetArrayElementAtIndex(j).vector2IntValue = data.cells[j];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.EditorUtility.SetDirty(_levelSO);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(_levelSO);

        _dirty = false;
        log($"Saved {pieces.Count} piece(s) + {blocks.Count} Grid Block(s) into {_levelSO.name}"
            + (_skippedLegacy > 0 ? $" — {_skippedLegacy} legacy shape-less entry(ies) the tool skipped are gone from the asset" : "")
            + ". Reopen the level to play it.");
#else
        log("Save is Editor-only.");
#endif
    }

    // -- Validation ---------------------------------------------------------------------------

    Entry entryAt(Vector2Int cell) {
        foreach (Entry entry in _entries) {
            List<Vector2Int> cells = entry.data.occupiedCells;
            for (int i = 0; i < cells.Count; i++) {
                if (entry.data.position + cells[i] == cell) return entry;
            }
        }
        return null;
    }

    // Inside the Board and sharing no cell with any OTHER entry — the same two rules Board applies
    // to gameplay Containers (isInBounds / isAreaFree), computed on the working list because the
    // Board's occupancy holds the hidden gameplay pieces, not these. Only MARKS an entry
    // (Entry.valid -> red tint, Save refusal); no edit is ever blocked or reverted by it.
    bool isPlacementValid(Entry entry, Vector2Int position, List<Vector2Int> cells) {
        if (!_board.isInBounds(position, cells)) return false;
        foreach (Entry other in _entries) {
            if (other == entry) continue;
            List<Vector2Int> otherCells = other.data.occupiedCells;
            for (int i = 0; i < cells.Count; i++) {
                Vector2Int cell = position + cells[i];
                for (int j = 0; j < otherCells.Count; j++) {
                    if (other.data.position + otherCells[j] == cell) return false;
                }
            }
        }
        return true;
    }

    void revalidateAll() {
        foreach (Entry entry in _entries) {
            entry.valid = isPlacementValid(entry, entry.data.position, entry.data.occupiedCells);
        }
    }

    // -- Feedback -----------------------------------------------------------------------------

    void refreshTints() {
        foreach (Entry entry in _entries) {
            if (entry.material == null) continue;
            Color color = entry.baseColor;
            if (!entry.valid) color = Color.Lerp(color, Color.red, 0.6f);
            else if (!entry.colorValid) color = Color.Lerp(color, new Color(1f, 0.5f, 0f), 0.6f);
            else if (entry == _selected) color = Color.Lerp(color, Color.white, 0.35f);
            else if (entry == _hovered) color = Color.Lerp(color, Color.white, 0.15f);
            writeColor(entry.material, color);
        }
    }

    static Color readColor(Material material) {
        if (material.HasProperty(BASE_COLOR_ID)) return material.GetColor(BASE_COLOR_ID);
        if (material.HasProperty(COLOR_ID)) return material.GetColor(COLOR_ID);
        return Color.white;
    }

    static void writeColor(Material material, Color color) {
        if (material.HasProperty(BASE_COLOR_ID)) material.SetColor(BASE_COLOR_ID, color);
        else if (material.HasProperty(COLOR_ID)) material.SetColor(COLOR_ID, color);
    }

    void log(string message) {
        _lastMessage = message;
        Debug.Log($"[SandLevelDesigner] {message}");
    }

    void OnGUI() {
        if (!available) return;
        if (!_active) {
            GUI.Label(new Rect(10, 10, 600, 24), $"SandLevelDesigner: press {_toggleKey} to design the level");
            return;
        }

        string hover = _hoverCellValid ? _hoverCell.ToString() : "—";
        string piece = _selected == null ? "none"
            : _selected.isBlock ? $"GridBlock {_selected.data.shape.type} @ {_selected.data.position} {_selected.data.rotation}{(_selected.valid ? "" : "  INVALID")}"
            : $"{_selected.data.shape.type} @ {_selected.data.position} {_selected.data.rotation} {_selected.data.color}{(_selected.valid ? "" : "  INVALID")}{(_selected.colorValid ? "" : "  NO ColorSO")}";
        GUI.Label(new Rect(10, 10, 900, 24), $"DESIGN MODE ({_toggleKey} exits) — {_levelSO.name}{(_dirty ? " *UNSAVED*" : "")} — {_entries.Count} item(s) — cell {hover} — selected: {piece}");
        GUI.Label(new Rect(10, 32, 900, 24), "1: create · 2: Grid Block · drag: move · R: rotate · Q: next shape · C / Shift+C: colour · Delete / Backspace: remove · S: save · L: reload · colour info: Inspector");
        GUI.Label(new Rect(10, 54, 900, 24), _lastMessage);
    }

#if UNITY_EDITOR
    // The Shape Pool is the prefab folder itself; this just lists it, ordered by ShapeType so 1
    // always creates the simplest piece (Shape_1x1) and Q walks the library in its enum order.
    void Reset() {
        fillShapePoolFromProject();
    }

    [ContextMenu("Fill Shape Pool From Project")]
    void fillShapePoolFromProject() {
        _shapePool.Clear();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { SHAPE_PREFAB_FOLDER });
        foreach (string guid in guids) {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            Shape shape = UnityEditor.AssetDatabase.LoadAssetAtPath<Shape>(path);
            if (shape != null) _shapePool.Add(shape);
        }
        _shapePool.Sort((a, b) => {
            int byType = ((int)a.type).CompareTo((int)b.type);
            return byType != 0 ? byType : string.CompareOrdinal(a.name, b.name);
        });

        _gridBlockPool.Clear();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { GRID_BLOCK_PREFAB_FOLDER })) {
            GridBlock block = UnityEditor.AssetDatabase.LoadAssetAtPath<GridBlock>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (block != null && block.shape != null) _gridBlockPool.Add(block);
        }
        _gridBlockPool.Sort((a, b) => {
            int byType = ((int)a.shape.type).CompareTo((int)b.shape.type);
            return byType != 0 ? byType : string.CompareOrdinal(a.name, b.name);
        });
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
