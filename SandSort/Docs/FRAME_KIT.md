# Modular Board Frame Kit

Generator: `frame_kit_generator.py` · Kaynak: `SandSort_FrameKit.blend` · Görsel: `preview_FrameKit.png`
FBX: `FBX/Board_FrameEdge.fbx`, `FBX/Board_FrameCorner.fbx`, `FBX/Board_FrameTJunction.fbx`

`Board_Frame` ve kilitli 16 asset'e dokunulmadı. Ölçüler Board_Frame'in mockup'a karşı
doğrulanmış değerlerinden gelir; export ayarları `export_fbx.py` ile birebir aynıdır
(Scale 1.0, −Z Forward / Y Up, Apply Unit) → Unity'de manuel scale correction gerekmez.

## Parçalar

| Asset | Boyut (x × y × z, Blender) | Pivot | Tri |
|---|---|---|--:|
| `Board_FrameEdge` | 0.85 × 0.19 × 0.25 | Segment başı, iç kenar çizgisi, Z = taban | 28 |
| `Board_FrameCorner` | 0.615 × 0.615 × 0.25 | Pencere köşe noktası (iç kenar çizgilerinin kesişimi), Z = taban | 108 |
| `Board_FrameTJunction` | 1.04 × 0.615 × 0.25 | Stem'in sol yüzü ile iç kenar çizgisinin kesişimi, Z = taban | 124 |

Ortak standart: border genişliği 0.19, yükseklik 0.25, dış köşe radius 0.14,
pencere köşe radius 0.10, üst bevel 0.02 (2 segment), `M_Board`, tek UV0.
**Tüm birleşim uçlarının kesiti aynıdır** → parçalar dikişsiz birleşir.

## Modül kuralı

- `Edge` = **1 cell (0.85)**. Köşe ve T kolları = **yarım cell (0.425)**.
- N cell'lik bir pencere kenarı = `Corner (0.425) + (N−1) × Edge + Corner/T (0.425)` = `N × 0.85`.
- Cell katı olmayan kenarlar (örn. canvas 3.57) için `Edge` **yalnız uzunluk ekseninde** ölçeklenir.
  Edge'in iki ucu dışında vertex'i yoktur; kesit ve bevel ölçekten etkilenmez.
- Divider, `T-Junction` stem'lerinden devam eden `Edge`'lerle kurulur.

## Yerleştirme (pencere etrafında CCW, malzeme her zaman gidiş yönünün sağında)

Z ekseni etrafında rotasyon (Blender); Unity'de aynı dönüşüm kalınlık ekseni etrafındadır.

| Konum | Parça | Rotasyon |
|---|---|--:|
| Alt kenar (+X yönünde) | Edge | 0° |
| Sağ kenar (+Y yönünde) | Edge | 90° |
| Üst kenar (−X yönünde) | Edge | 180° |
| Sol kenar (−Y yönünde) | Edge | −90° |
| Sol-alt / sağ-alt / sağ-üst / sol-üst köşe | Corner | 0° / 90° / 180° / −90° |
| Sağ kenarda divider çıkışı | T-Junction | 90° |
| Sol kenarda divider çıkışı | T-Junction | −90° |
| Divider (+X yönünde) | Edge | 0° |

### Mockup board örneği (doğrulandı)
Grid penceresi `[0, 4.25]²`, divider `y ∈ [4.25, 4.44]`, canvas penceresi `[0, 4.25] × [4.44, 8.01]`.

| Parça | Pivot `(x, y)` | Rot | Ölçek X |
|---|---|--:|--:|
| Corner | (0, 0) | 0 | 1 |
| Edge ×4 | (0.425 + k·0.85, 0) | 0 | 1 |
| Corner | (4.25, 0) | 90 | 1 |
| Edge ×4 | (4.25, 0.425 + k·0.85) | 90 | 1 |
| T-Junction | (4.25, 4.25) | 90 | 1 |
| Edge | (4.25, 4.865) | 90 | 3.2 |
| Corner | (4.25, 8.01) | 180 | 1 |
| Edge ×4 | (3.825 − k·0.85, 8.01) | 180 | 1 |
| Corner | (0, 8.01) | −90 | 1 |
| Edge | (0, 7.585) | −90 | 3.2 |
| T-Junction | (0, 4.44) | −90 | 1 |
| Edge ×4 | (0, 3.825 − k·0.85) | −90 | 1 |
| Edge ×4 (divider) | (0.425 + k·0.85, 4.44) | 0 | 1 |

Toplam 28 instance (22 Edge, 4 Corner, 2 T). Raycast doğrulaması: 5 hat boyunca 14 062 örnekte
boşluk yok, pencere içleri boş, grid penceresi 4.25, divider 0.19, canvas 3.57, dış ölçü 4.63 × 8.39.

## Board_Frame'den farklar (bilinçli)

| Konu | Board_Frame | Kit | Neden |
|---|---|---|---|
| Divider kalınlığı | 0.12 | **0.19** | 3 parçalık kitte divider = Edge; ayrı divider parçası yok |
| Alt kenar beveli | 0.015 | **yok** | Tüm birleşim kesitlerinin birebir aynı olması için |
| Pencere tabanı | 0.06 taban var | **yok** (ray/rail parçaları) | Modüler parçalar taban içeremez; arka yüzey ayrı çözülmeli |
