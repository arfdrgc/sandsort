using UnityEngine;

// Display-side staircase for a Shape's fill readout (2026-09-21, Docs/ref_box_fill_animation.md §4.2).
//
// In the reference, progress is a staircase: short 1–4% bursts (40–250 ms) separated by 0.2–1.0 s
// plateaus, with the label and the fill picture moving together. Here extraction is a steady-rate
// stream — while a piece covers reachable sand, Container's fill grows a little every frame — so shown
// as-is it reads as a smooth count-up. Extraction must stay exactly as it is, so the staircase is made
// HERE, purely on the display side: this holds a `shown` value that trails Container's real fill and
// releases what has built up in bursts.
//
// Nothing here feeds gameplay. Capacity, fillLevel, seal and extraction never see `shown`; Shape
// hands it to both the label and ShapeSandFill, which is what keeps those two in step.
//
// Rules:
//   - First value, a drop (reset) and full (1.0) are shown immediately — so a part-filled load does
//     not animate, and 100% appears on the very frame the piece seals, as before.
//   - First sand (shown still 0) starts one FIRST_CONTACT_BURST: the display follows the real fill live
//     for 0.27 s — the reference's 0 -> 24% burst — so the label and the first-contact reveal start
//     on contact, and the first plateau comes after it.
//   - Otherwise sand builds up during a plateau and is released as one burst when the plateau is over
//     and at least MIN_BURST has built up. If MAX_BURST has built up, the plateau is cut short — but
//     never below MIN_PLATEAU. With 1–4% bursts and >= 0.2 s plateaus the reference can only show
//     about 10%/s; a faster real stream (measured here: ~15%/s sustained, ~30%/s at first contact)
//     therefore shows as BIGGER bursts with the plateaus kept, rather than as a continuous rise or a
//     display that falls further and further behind. Every burst releases everything pending, so the
//     lag stays bounded (about rate * (MIN_PLATEAU + MAX_BURST_DURATION)).
//   - When the stream stops, whatever is left (even under MIN_BURST) is released after the plateau, so
//     the display always ends on the real figure.
public class FillStaircase {

    const float MIN_BURST = 0.01f;
    const float MAX_BURST = 0.04f;
    const float MIN_PLATEAU = 0.2f;
    const float MAX_PLATEAU = 1.0f;

    // A burst runs over a short time rather than in one frame — the reference label ticks through the
    // intermediate values (46, 47, 48, 49, 50 in 0.24 s) — at about 18% per second (46->50 in 0.24 s,
    // 56->59 in 0.14 s, 33->36 in 0.20 s), clamped to the observed 40–250 ms.
    const float BURST_RATE = 0.18f;
    const float MIN_BURST_DURATION = 0.04f;
    const float MAX_BURST_DURATION = 0.25f;

    // How long the real fill must stand still before a leftover under MIN_BURST is released.
    const float STALL_RELEASE = 0.3f;

    // First contact: how long the display follows the real fill live before the first plateau.
    const float FIRST_CONTACT_BURST = 0.27f;

    float _target;
    float _shown = -1f;

    bool _bursting;
    float _burstFrom, _burstTo, _burstDuration, _burstTime;
    float _followLeft;          // > 0 while the first-contact burst is tracking the real fill
    float _plateauLeft;
    float _plateauTime;         // time spent in the current plateau
    float _sinceTargetChanged;

    public float shown => Mathf.Max(0f, _shown);

    // The real fill, 0..1. Returns true when `shown` changed on the spot and must be displayed now.
    public bool setTarget(float value) {
        value = Mathf.Clamp01(value);
        if (value != _target) _sinceTargetChanged = 0f;
        _target = value;

        if (_shown < 0f || value >= 1f || value < _shown) {
            _shown = value;
            _bursting = false;
            _followLeft = 0f;
            _plateauLeft = 0f;
            _plateauTime = MIN_PLATEAU;
            return true;
        }
        return false;
    }

    // Advances by one frame. Returns true when `shown` changed and must be displayed.
    public bool tick(float deltaTime) {
        if (_shown < 0f) return false;
        _sinceTargetChanged += deltaTime;

        // First contact: follow the real fill live until the burst's time is up.
        if (_followLeft > 0f) {
            _followLeft -= deltaTime;
            bool moved = _shown != _target;
            _shown = _target;
            if (_followLeft <= 0f) startPlateau();
            return moved;
        }

        if (!_bursting) {
            _plateauLeft -= deltaTime;
            _plateauTime += deltaTime;
            float pending = _target - _shown;
            if (pending <= 0f) return false;

            if (_shown <= 0f) {
                _followLeft = FIRST_CONTACT_BURST;
                _shown = _target;
                return true;
            }

            bool release =
                (pending >= MAX_BURST && _plateauTime >= MIN_PLATEAU) ||
                (_plateauLeft <= 0f && (pending >= MIN_BURST || _sinceTargetChanged >= STALL_RELEASE));
            if (!release) return false;

            _bursting = true;
            _burstFrom = _shown;
            _burstTo = _target;
            _burstDuration = Mathf.Clamp((_burstTo - _burstFrom) / BURST_RATE, MIN_BURST_DURATION, MAX_BURST_DURATION);
            _burstTime = 0f;
        }

        _burstTime += deltaTime;
        float t = Mathf.Clamp01(_burstTime / _burstDuration);
        _shown = Mathf.Lerp(_burstFrom, _burstTo, t);
        if (t >= 1f) {
            _bursting = false;
            startPlateau();
        }
        return true;
    }

    void startPlateau() {
        _plateauLeft = Random.Range(MIN_PLATEAU, MAX_PLATEAU);
        _plateauTime = 0f;
    }
}
