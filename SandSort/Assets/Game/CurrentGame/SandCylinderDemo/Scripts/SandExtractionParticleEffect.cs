using System.Collections.Generic;
using UnityEngine;

// Owns the sparse, per-color "sand grain" visual for SandCylinderDemo's
// extraction effect. Each grain is a genuine Unity ParticleSystem particle,
// launched once (via EmitParams) with a precomputed free-fall velocity —
// zero initial vertical speed, horizontal speed solved to land near the
// extracting cube's PREDICTED position — and Unity's own particle engine
// then integrates gravity over the flight every frame; there is no
// per-grain C# loop here at all.
//
// This is still "no real physics" in the RigidBody/PhysX sense — no
// collision, no per-object simulation step, just a closed-form free-fall
// formula solved once at spawn time — but offloading the per-frame
// integration to Unity's native, mobile-cheap ParticleSystem engine (instead
// of a hand-rolled Vector3.Lerp loop over a List<Grain>, as this used to be)
// is what lets grain counts scale up enough, combined with per-grain timing/
// landing scatter (see SpawnGrain), to read as a pouring stream of discrete
// falling grains — not one smooth liquid jet, and not individually-tracked,
// one-at-a-time dots either.
//
// One ParticleSystem per palette color (rather than one shared system) so
// simultaneously in-flight grains of different colors — e.g. a red grain
// still falling while a different cube starts pulling blue — never bleed
// into each other's color. This is necessary because the project's URP
// Unlit shader (SandCylinderCubeUnlit.mat — see SandCylinderRenderer's
// Android note on why that can't just be swapped for a particle-specific
// shader via Shader.Find) doesn't sample per-particle vertex color, so each
// system's color is instead fixed once via MaterialPropertyBlock at Init and
// never touched again — the same _BaseColor/_Color keys SandExtractionCube
// .Setup uses.
public class SandExtractionParticleEffect : MonoBehaviour {

    // Generous per-color mobile-safety ceiling. Unity's own ParticleSystem
    // enforces this natively (an Emit() past maxParticles is simply
    // ignored), so unlike the old hand-rolled list this needs no bookkeeping
    // on our side.
    const int MaxGrainsPerColor = 256;

    // DEPTH OF THE POUR, followTarget path only (2026-09-13) — i.e. the Sand Idea game's shapes; the
    // demo conveyor keeps its own behaviour untouched.
    //
    // Measured: the sand quad the grains leave sits at z = 0, and a Container's cells (cellVisual,
    // the target handed in here) also sit at z = 0 — which is the BACK plane of the shape's FBX,
    // whose visible front face is 0.5 nearer the camera at z = -0.5. So a grain aimed straight at
    // cellVisual flies to 0.5 BEHIND the face the player is looking at and is occluded by the piece's
    // own rim on the way in: it reads as the sand passing THROUGH the shape.
    //
    // Both ends of the flight are therefore pushed toward the camera (world -Z: the project's
    // convention is gameplay plane XY, thickness Z, camera at -Z). The grain leaves the sand just
    // clear of the sand quad, and lands just clear of the front face, so the whole pour happens in
    // front of everything it passes over.
    // ONE plane for the whole flight, not a slide from one depth to another. Offsetting only the
    // landing was not enough: the grain then crosses Z gradually and is still behind the front face
    // for most of the fall, so it disappears into the piece for the last stretch — measured 41 of 47
    // live grains behind the face. Spawning on the same plane it lands on makes velocity.z zero and
    // keeps every grain in front of the shape from the moment it leaves the sand.
    const float FollowPourForwardOffset = 0.56f;

    SandCylinderTunables tunables;
    ParticleSystem[] systemsByColor; // index 0 unused (EMPTY)
    Vector3 receivingOffset;

    // Followed grains (SpawnGrain's followTarget, used by the Sand Idea Level's shapes, never by
    // the demo conveyor): each such grain is emitted with its own randomSeed and keeps steering
    // onto its target's CURRENT position while in flight — see LateUpdate. Runtime-only state.
    Dictionary<uint, Transform> followedTargetBySeed = new Dictionary<uint, Transform>();
    Dictionary<Transform, Vector3> followedTargetLastPosition = new Dictionary<Transform, Vector3>();
    Dictionary<Transform, Vector3> followedTargetDelta = new Dictionary<Transform, Vector3>();
    HashSet<uint> liveFollowedSeeds = new HashSet<uint>();
    List<uint> seedScratch = new List<uint>();
    List<Transform> targetScratch = new List<Transform>();
    ParticleSystem.Particle[] particleBuffer;
    uint nextFollowSeed = 1;

    public void Init(SandCylinderTunables tunables, Material sharedMaterial, float cubeSize) {
        this.tunables = tunables;

        // Just inside the cube's top face — where sand arriving from the
        // cylinder above visually "enters" — rather than the cube's center.
        receivingOffset = new Vector3(0f, cubeSize * 0.35f, 0f);

        Color[] palette = tunables.sandColors;
        int colorCount = palette.Length;
        systemsByColor = new ParticleSystem[colorCount + 1];

        for (int c = 1; c <= colorCount; c++) {
            GameObject go = new GameObject("SandGrains_Color" + c);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psRenderer = go.GetComponent<ParticleSystemRenderer>();
            BuildGrainSystem(ps, psRenderer, sharedMaterial, palette[c - 1]);
            systemsByColor[c] = ps;
        }
    }

    void BuildGrainSystem(ParticleSystem ps, ParticleSystemRenderer psRenderer, Material sharedMaterial, Color color) {
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = MaxGrainsPerColor;
        main.startSpeed = 0f; // velocity is set per-particle via EmitParams instead
        main.gravityModifier = 0f; // set immediately before each Emit — see SpawnGrain

        var emission = ps.emission;
        emission.enabled = false; // every particle is injected manually via Emit(EmitParams)

        var shape = ps.shape;
        shape.enabled = false;

        // No trails: a stretched trail behind each grain reads as one
        // continuous, smooth liquid rope/jet (like water being sprayed)
        // rather than dry sand — grains stay untrailed, discrete billboards,
        // and it's density (emission rate) plus per-grain velocity scatter
        // (see SpawnGrain) that reads as a pouring stream instead.
        var trails = ps.trails;
        trails.enabled = false;

        // Small built-in turbulence so the fall reads as organic, dry sand
        // rather than a perfectly straight laser — Unity integrates this
        // natively per particle, no per-grain scripting needed. Kept subtle
        // and non-scrolling so it doesn't read as a flowing liquid surface.
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.05f;
        noise.frequency = 2.5f;
        noise.scrollSpeed = 0f;

        if (sharedMaterial == null) {
            Debug.LogError("SandExtractionParticleEffect: sharedMaterial is null — assign SandCylinderDemoBootstrap's Cube Material field in the Inspector (Assets/Game/CurrentGame/SandCylinderDemo/Materials/SandCylinderCubeUnlit.mat). Grains will render with Unity's default (magenta) material until this is fixed.");
        }
        psRenderer.sharedMaterial = sharedMaterial;
        psRenderer.trailMaterial = sharedMaterial;
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        psRenderer.alignment = ParticleSystemRenderSpace.View;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        psRenderer.SetPropertyBlock(block); // fixed for this system's whole life
    }

    // Spawns exactly one grain from sourcePos, launched with a closed-form
    // free-fall velocity — solved once, right here — that lands it near
    // targetCube's PREDICTED position at landing time, not its position
    // right now. targetCube keeps moving at a known, constant
    // tunables.cubeMovementSpeed for the whole flight (see
    // SandExtractionController.AdvanceConveyor), so that prediction holds
    // exactly under normal operation — no per-frame retargeting needed,
    // which is what lets this be a single Emit() call instead of a live
    // per-frame C# tracker.
    //
    // Callers should still invoke this at a sparse, tunable rate
    // (SandCylinderTunables.particlesPerExtraction, grains/second) rather
    // than once per extraction tick, so grains stay dense enough to read as
    // a stream without being so dense they hide the underlying tunable.
    //
    // followTarget (default false = the demo conveyor behavior above, unchanged): for a target that
    // moves unpredictably — a dragged shape — the grain is aimed at the target's current position
    // instead of a predicted one and then keeps following it every frame (LateUpdate).
    public void SpawnGrain(Vector3 sourcePos, Transform targetCube, byte colorIndex, bool followTarget = false) {
        if (systemsByColor == null || colorIndex <= 0 || colorIndex >= systemsByColor.Length) return;
        ParticleSystem ps = systemsByColor[colorIndex];
        if (ps == null) return;

        float spread = tunables.particleSpread;
        Vector3 source = sourcePos + new Vector3(
            Random.Range(-spread, spread),
            Random.Range(-spread, spread),
            Random.Range(-spread, spread) * 0.3f);
        if (followTarget) source.z -= FollowPourForwardOffset;

        Vector3 cubePos = targetCube.position;
        // A followed target already tracked this frame is aimed at where LateUpdate last saw it, so
        // that LateUpdate's next correction (target movement since then) lands it exactly.
        if (followTarget && followedTargetLastPosition.TryGetValue(targetCube, out Vector3 trackedPos)) {
            cubePos = trackedPos;
        }
        Vector3 cubeTargetY = cubePos + receivingOffset;
        if (followTarget) cubeTargetY.z -= FollowPourForwardOffset;

        // Vertical and horizontal motion are solved independently, on
        // purpose: a single "arrive exactly at the target after flightTime"
        // ballistic solve (the previous approach) can demand a positive
        // (upward) initial vertical velocity whenever the horizontal
        // distance is large relative to the vertical drop — the solver's
        // only way to stay airborne long enough to cover that distance in
        // the time available. That reads exactly as a sprayed/spurting jet,
        // not falling sand.
        //
        // Instead: the grain starts at zero vertical velocity and only ever
        // accelerates downward under gravity — y(t) = source.y - 0.5*g*t^2
        // is strictly non-increasing for every t >= 0, so it is
        // mathematically impossible for a grain's Y to ever rise above the
        // Y it was extracted from. fallTime is simply how long real
        // free-fall (from rest) takes to cover the actual vertical drop to
        // the cube; horizontal motion then just runs at whatever constant
        // velocity covers the horizontal distance in that same time —
        // independent of the vertical solve, so jittering fallTime per
        // grain (for scatter) never reintroduces an upward vertical launch.
        float gravityModifier = tunables.particleGravity;
        float gravityMagnitude = Mathf.Max(0.01f, -Physics.gravity.y * gravityModifier);
        float verticalDrop = Mathf.Max(0.01f, source.y - cubeTargetY.y);
        float fallTime = Mathf.Clamp(Mathf.Sqrt(2f * verticalDrop / gravityMagnitude), 0.05f, Mathf.Max(0.05f, tunables.particleLifetime));

        // Jittered per grain: without this, every grain spawned in the same
        // tick shares almost the same source/target/duration and traces
        // almost the same path, which overlaps into one smooth, coherent
        // stream instead of a scatter of individually falling grains.
        float flightTime = fallTime * Random.Range(0.85f, 1.15f);

        float landingScatter = tunables.particleSpread * 3f;
        float predictedCubeX = cubePos.x + (followTarget ? 0f : tunables.cubeMovementSpeed * flightTime);
        // Scatter stays on X for a followed target: scattering the LANDING in Z as well would put a
        // share of the grains back behind the front face the offset above just cleared, and they
        // would vanish into the piece again. The pour is a plane in front of the shape, not a cloud.
        float landingScatterZ = followTarget ? 0f : landingScatter;
        Vector3 target = new Vector3(predictedCubeX, cubeTargetY.y, cubeTargetY.z)
            + new Vector3(Random.Range(-landingScatter, landingScatter), 0f, Random.Range(-landingScatterZ, landingScatterZ));

        Vector3 velocity = new Vector3(
            (target.x - source.x) / flightTime,
            0f, // free-fall from rest — gravity alone carries it down, never up
            (target.z - source.z) / flightTime);

        var main = ps.main;
        main.gravityModifier = gravityModifier;

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams {
            position = source,
            velocity = velocity,
            startSize = Mathf.Max(0.001f, tunables.particleSize),
            startLifetime = flightTime,
            startColor = Color.white,
            rotation = 0f,
        };

        if (followTarget) {
            uint seed = nextFollowSeed++;
            if (seed == 0) seed = nextFollowSeed++;
            emitParams.randomSeed = seed;
            followedTargetBySeed[seed] = targetCube;
            if (!followedTargetLastPosition.ContainsKey(targetCube)) {
                followedTargetLastPosition[targetCube] = targetCube.position;
            }
        }

        ps.Emit(emitParams, 1);
    }

    // Keeps every followed grain on the line from its launch point to its target's CURRENT
    // position. Each frame, a target that moved by delta shifts each of its grains by delta *
    // flight progress and adds delta / flight time to its velocity — so the grain still lands exactly
    // on the target at the end of its (unchanged) flight time, however the target moves meanwhile.
    // Vertical free fall is untouched apart from that same shift. Does nothing unless followed
    // grains exist, so the demo conveyor never pays for it.
    void LateUpdate() {
        if (followedTargetBySeed.Count == 0 || systemsByColor == null) return;

        targetScratch.Clear();
        targetScratch.AddRange(followedTargetLastPosition.Keys);
        followedTargetDelta.Clear();
        foreach (Transform target in targetScratch) {
            if (target == null) {
                followedTargetLastPosition.Remove(target);
                continue;
            }
            Vector3 position = target.position;
            followedTargetDelta[target] = position - followedTargetLastPosition[target];
            followedTargetLastPosition[target] = position;
        }

        liveFollowedSeeds.Clear();
        for (int c = 1; c < systemsByColor.Length; c++) {
            ParticleSystem ps = systemsByColor[c];
            if (ps == null) continue;

            if (particleBuffer == null || particleBuffer.Length < ps.main.maxParticles) {
                particleBuffer = new ParticleSystem.Particle[ps.main.maxParticles];
            }

            int count = ps.GetParticles(particleBuffer);
            bool changed = false;
            for (int i = 0; i < count; i++) {
                uint seed = particleBuffer[i].randomSeed;
                if (!followedTargetBySeed.TryGetValue(seed, out Transform target)) continue;
                liveFollowedSeeds.Add(seed);

                if (target == null || !followedTargetDelta.TryGetValue(target, out Vector3 delta) || delta == Vector3.zero) continue;

                float flightTime = Mathf.Max(0.0001f, particleBuffer[i].startLifetime);
                float progress = 1f - particleBuffer[i].remainingLifetime / flightTime;
                particleBuffer[i].position += delta * progress;
                particleBuffer[i].velocity += delta / flightTime;
                changed = true;
            }
            if (changed) ps.SetParticles(particleBuffer, count);
        }

        // Forget grains that have landed, then targets with no grain left in flight.
        seedScratch.Clear();
        foreach (uint seed in followedTargetBySeed.Keys) {
            if (!liveFollowedSeeds.Contains(seed)) seedScratch.Add(seed);
        }
        foreach (uint seed in seedScratch) followedTargetBySeed.Remove(seed);

        targetScratch.Clear();
        targetScratch.AddRange(followedTargetLastPosition.Keys);
        foreach (Transform target in targetScratch) {
            if (!followedTargetBySeed.ContainsValue(target)) followedTargetLastPosition.Remove(target);
        }
    }
}
