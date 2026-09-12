using UnityEngine;

public class SandGrid : MonoBehaviour {

    public const byte EMPTY = 0;

    [SerializeField] SandTunables tunables = new SandTunables();
    public SandTunables Tunables => tunables;

    byte[] cells;
    byte[] settledStreak;
    float[] fallProgress;
    int width, height;
    float stepAccumulator;
    int frameParity;

    public int Width => width;
    public int Height => height;

    void Awake() {
        Resize(tunables.gridWidth, tunables.gridHeight);
    }

    public void Resize(int w, int h) {
        width = Mathf.Max(1, w);
        height = Mathf.Max(1, h);
        cells = new byte[width * height];
        settledStreak = new byte[width * height];
        fallProgress = new float[width * height];
    }

    public byte GetCell(int x, int y) => cells[y * width + x];

    void SetCell(int x, int y, byte v) => cells[y * width + x] = v;

    public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    public void SpawnCell(int x, int y, byte colorIndex) {
        if (!InBounds(x, y)) return;
        if (GetCell(x, y) == EMPTY) SetCell(x, y, colorIndex);
    }

    void Update() {
        if (width != tunables.gridWidth || height != tunables.gridHeight) {
            Resize(tunables.gridWidth, tunables.gridHeight);
        }

        stepAccumulator += Time.deltaTime;
        float stepInterval = 1f / Mathf.Max(1, tunables.simulationStepsPerSecond);
        int safety = 8;
        while (stepAccumulator >= stepInterval && safety-- > 0) {
            Step();
            stepAccumulator -= stepInterval;
        }
    }

    void Step() {
        frameParity ^= 1;
        for (int y = 1; y < height; y++) {
            bool leftToRight = ((y + frameParity) & 1) == 0;
            if (leftToRight) {
                for (int x = 0; x < width; x++) StepCell(x, y);
            } else {
                for (int x = width - 1; x >= 0; x--) StepCell(x, y);
            }
        }
    }

    void StepCell(int x, int y) {
        byte c = GetCell(x, y);
        if (c == EMPTY) return;
        int idx = y * width + x;

        if (GetCell(x, y - 1) == EMPTY) {
            if (tunables.fallSidewaysMixChance > 0f && Random.value < tunables.fallSidewaysMixChance) {
                bool spillLeft = x > 0 && GetCell(x - 1, y - 1) == EMPTY;
                bool spillRight = x < width - 1 && GetCell(x + 1, y - 1) == EMPTY;
                if (spillLeft || spillRight) {
                    bool goLeft = spillLeft && (!spillRight || Random.value < 0.5f);
                    int nx = goLeft ? x - 1 : x + 1;
                    int spillIdx = (y - 1) * width + nx;
                    SetCell(nx, y - 1, c);
                    SetCell(x, y, EMPTY);
                    settledStreak[spillIdx] = 0;
                    fallProgress[idx] = 0f;
                    fallProgress[spillIdx] = 0f;
                    return;
                }
            }

            float progress = fallProgress[idx] + tunables.maxFallCellsPerTick;
            int wantCells = Mathf.FloorToInt(progress);

            int landY = y - 1;
            int moved = 1;
            for (int i = 2; i <= wantCells; i++) {
                int cy = y - i;
                if (cy < 0 || GetCell(x, cy) != EMPTY) break;
                landY = cy;
                moved = i;
            }

            int landIdx = landY * width + x;
            SetCell(x, landY, c);
            SetCell(x, y, EMPTY);
            settledStreak[landIdx] = 0;
            fallProgress[idx] = 0f;
            fallProgress[landIdx] = (moved >= wantCells) ? (progress - wantCells) : 0f;
            return;
        }

        fallProgress[idx] = 0f;

        bool leftFree = x > 0 && GetCell(x - 1, y - 1) == EMPTY;
        bool rightFree = x < width - 1 && GetCell(x + 1, y - 1) == EMPTY;

        if (leftFree || rightFree) {
            bool goLeft = leftFree && (!rightFree || Random.value < 0.5f);
            int nx = goLeft ? x - 1 : x + 1;

            bool steepGap = IsSteepGap(nx, y - 1);
            bool allowSlide = steepGap
                ? Random.value < tunables.lateralSpreadChance
                : Random.value >= tunables.pileStability && Random.value < tunables.lateralSpreadChance;

            if (allowSlide) {
                int destIdx = (y - 1) * width + nx;
                SetCell(nx, y - 1, c);
                SetCell(x, y, EMPTY);
                settledStreak[destIdx] = 0;
                fallProgress[destIdx] = 0f;
                return;
            }
        }

        if (settledStreak[idx] < 255) settledStreak[idx]++;

        if (settledStreak[idx] > 2 && settledStreak[idx] <= tunables.settleJitterWindowTicks && Random.value < tunables.settleJitterChance) {
            bool jitterLeft = x > 0 && GetCell(x - 1, y) == EMPTY;
            bool jitterRight = x < width - 1 && GetCell(x + 1, y) == EMPTY;
            if (jitterLeft || jitterRight) {
                bool goLeft = jitterLeft && (!jitterRight || Random.value < 0.5f);
                int nx = goLeft ? x - 1 : x + 1;
                int destIdx = y * width + nx;
                SetCell(nx, y, c);
                SetCell(x, y, EMPTY);
                fallProgress[destIdx] = 0f;
            }
        }
    }

    bool IsSteepGap(int x, int destY) {
        int emptyCount = 0;
        for (int i = 1; i <= tunables.minGapForFreeCascade; i++) {
            int cy = destY - i;
            if (cy >= 0 && GetCell(x, cy) == EMPTY) emptyCount++;
            else break;
        }
        return emptyCount >= tunables.minGapForFreeCascade;
    }
}
