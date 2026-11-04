using UnityEngine;

namespace CardboardXR.Stereo
{
    // The pattern a viewer is tuned against: a square grid, a crosshair and two circles, drawn on a
    // texture and hung in front of the eyes. Through a lens and distortion that match the viewer, the
    // grid comes out straight and evenly spaced and the cross sits in the middle of each lens; a
    // separation, a distance or a distortion that's off bows the lines, stretches the grid or moves
    // the cross off centre, and an eye offset that's off puts the cross off the lens's middle.
    // The drawing is a plain function of each pixel's place in the texture, so it's tested without a
    // device.
    public static class ViewerTestPattern
    {
        public const int Size = 512;

        // The grid's lines each way, and how often a thicker, brighter one comes.
        const int k_Cells = 16;
        const int k_MajorEvery = 4;
        // Half the length of the crosshair's arms, as a share of the texture's side.
        const float k_CrossArm = 0.11f;

        public static readonly Color32 Background = new(16, 16, 20, 255);
        public static readonly Color32 Line = new(64, 64, 72, 255);
        public static readonly Color32 MajorLine = new(140, 140, 152, 255);
        public static readonly Color32 Cross = new(255, 255, 255, 255);
        public static readonly Color32 Circle = new(96, 200, 255, 255);

        public static Color32[] Build(int width, int height)
        {
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Background;

            // The grid: sixteen cells each way, every fourth line brighter, so a bow or a stretch
            // shows.
            var thickness = Mathf.Max(1, Mathf.Min(width, height) / 256);
            for (var i = 0; i < k_Cells; i++)
            {
                var major = i % k_MajorEvery == 0;
                var colour = major ? MajorLine : Line;
                var line = major ? thickness * 2 : thickness;
                FillColumn(pixels, width, height, i * width / k_Cells, line, colour);
                FillRow(pixels, width, height, i * height / k_Cells, line, colour);
            }

            var cx = width / 2;
            var cy = height / 2;
            // Two circles round the middle, for the distortion: a lens that's right keeps them
            // circular.
            DrawRing(pixels, width, height, cx, cy, Mathf.Min(width, height) / 6f, thickness * 2, Circle);
            DrawRing(pixels, width, height, cx, cy, Mathf.Min(width, height) / 3f, thickness * 2, Circle);

            // The crosshair, whose middle should sit at the middle of the lens.
            var arm = Mathf.RoundToInt(Mathf.Min(width, height) * k_CrossArm);
            FillRow(pixels, width, height, cy, thickness * 3, Cross, cx - arm, cx + arm);
            FillColumn(pixels, width, height, cx, thickness * 3, Cross, cy - arm, cy + arm);
            return pixels;
        }

        public static Texture2D CreateTexture(int width = Size, int height = Size)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Viewer Test Pattern",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(Build(width, height));
            texture.Apply(false);
            return texture;
        }

        static void FillRow(Color32[] pixels, int width, int height, int y, int thickness, Color32 colour,
            int from = 0, int to = -1)
        {
            if (to < 0)
                to = width - 1;
            var half = thickness / 2;
            for (var dy = -half; dy <= half; dy++)
            {
                var row = y + dy;
                if (row < 0 || row >= height)
                    continue;
                for (var x = Mathf.Max(0, from); x <= Mathf.Min(width - 1, to); x++)
                    pixels[row * width + x] = colour;
            }
        }

        static void FillColumn(Color32[] pixels, int width, int height, int x, int thickness, Color32 colour,
            int from = 0, int to = -1)
        {
            if (to < 0)
                to = height - 1;
            var half = thickness / 2;
            for (var dx = -half; dx <= half; dx++)
            {
                var column = x + dx;
                if (column < 0 || column >= width)
                    continue;
                for (var y = Mathf.Max(0, from); y <= Mathf.Min(height - 1, to); y++)
                    pixels[y * width + column] = colour;
            }
        }

        static void DrawRing(Color32[] pixels, int width, int height, int cx, int cy, float radius, int thickness,
            Color32 colour)
        {
            if (radius <= 0f)
                return;
            var steps = Mathf.Max(64, Mathf.RoundToInt(radius * 8f));
            var dot = Mathf.Max(1, thickness);
            for (var i = 0; i < steps; i++)
            {
                var angle = i * (Mathf.PI * 2f / steps);
                var x = Mathf.RoundToInt(cx + Mathf.Cos(angle) * radius);
                var y = Mathf.RoundToInt(cy + Mathf.Sin(angle) * radius);
                FillDisc(pixels, width, height, x, y, dot, colour);
            }
        }

        static void FillDisc(Color32[] pixels, int width, int height, int cx, int cy, int radius, Color32 colour)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius)
                        continue;
                    var x = cx + dx;
                    var y = cy + dy;
                    if (x < 0 || x >= width || y < 0 || y >= height)
                        continue;
                    pixels[y * width + x] = colour;
                }
            }
        }
    }
}
