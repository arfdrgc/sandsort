#ifndef GRID_BLOCK_WORLD_UV_INCLUDED
#define GRID_BLOCK_WORLD_UV_INCLUDED

// Grid Block world-space texture mapping (2026-09-24), used by GridBlockBrickWall.shader.
//
// The texture coordinate is NOT the FBX's UV. The Grid Block FBXs lay their front face out in
// different orientations (turned 90 degrees on some shapes, 45 on Plus5) and the block's own
// rotation turns them again, so mesh UVs can never keep bricks horizontal. Instead the fragment's
// world position, relative to the Grid Block ROOT (_GridBlockOrigin), is projected on the world plane
// the face points at: world XY for the front, ZY for the left / right sides, XZ for top / bottom.
// The Board lies in world XY and never rotates, so world X is always "horizontal" on screen.
//
// Measuring from the root (not from the world origin) gives every block its own pattern that
// travels with it, and the root is the one transform that never rotates (Shape turns VisualRoot
// only), so the pattern also does not jump when the block rotates.
//
// _BaseMap's tiling is therefore "texture repeats per world unit" and its offset the pattern phase.
//
// _GridBlockOrigin is per renderer — GridBlock sets it through a MaterialPropertyBlock — so it lives
// outside UnityPerMaterial. That makes the shader SRP-Batcher-incompatible, which only means these
// few renderers take the regular (per-draw) path.
float4 _GridBlockOrigin;

float2 GridBlockPlanarUV(float3 positionWS, float3 normalWS)
{
    float3 local = positionWS - _GridBlockOrigin.xyz;
    float3 axis = abs(normalWS);
    if (axis.z >= max(axis.x, axis.y)) return local.xy;
    if (axis.x >= axis.y) return local.zy;
    return local.xz;
}

#endif
