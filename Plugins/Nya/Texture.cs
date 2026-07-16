namespace Nya
{
    using System.Reflection.Metadata;
    using ModelConverter.Geometry;
    using ModelConverter.Graphics;
    using Nya.Serializer;
    using SLIS = SixLabors.ImageSharp;
    // using ImageHash = CoenM.ImageHash; //Tried to compare textures with this, but my own method turned out to be better

    /// <summary>
    /// Catgirl texture
    /// </summary>
    public class Texture
    {
        // private static readonly ImageHash.IImageHash _hasher = new ImageHash.HashAlgorithms.PerceptualHash();
        // private static readonly ImageHash.IImageHash _hasher = new ImageHash.HashAlgorithms.AverageHash();
        // private static readonly ImageHash.IImageHash _hasher = new ImageHash.HashAlgorithms.DifferenceHash();

        /// <summary>
        /// Initializes a new instance of the <see cref="Texture"/> class
        /// </summary>
        public Texture()
        {
            this.Data = new ushort[0];
            this.Width = 0;
            this.Height = 0;
            this.Name = string.Empty;
            this.UV = new int[]
            {
                0,
                0,
                0,
                0
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Texture"/> class
        /// </summary>
        /// <param name="name">Texture name</param>
        /// <param name="width">bitmap width</param>
        /// <param name="height">Bitmap height</param>
        /// <param name="data">Bitmap data</param>
        public Texture(string name, ushort width, ushort height, ushort[] data) : this()
        {
            this.Name = name;
            this.Width = width;
            this.Height = height;
            this.Data = data;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Texture"/> class
        /// </summary>
        /// <param name="name">Texture name</param>
        /// <param name="bitmap">Bitmap data</param>
        public Texture(string name, SLIS.Image<SLIS.PixelFormats.Argb32> bitmap) : this()
        {
            this.Name = name;
            this.Width = (ushort)bitmap.Width;
            this.Height = (ushort)bitmap.Height;

            List<Color> colors = new List<Color>();

            for (int y = 0; y < this.Height; y++)
            {
                for (int x = 0; x < this.Width; x++)
                {
                    SLIS.PixelFormats.Argb32 color = bitmap[x, y];

                    if (color.A < 0x80)
                    {
                        colors.Add(Color.FromRgb(0, 0, 0, 0));
                    }
                    else
                    {
                        colors.Add(Color.FromRgb(color.R, color.G, color.B));
                    }
                }
            }

            this.Data = colors.Select(color => color.AsAbgr555()).ToArray();
        }

        /// <summary>
        /// Gets or sets image data
        /// </summary>
        [ArraySizeDynamic("DataLength")]
        [FieldOrder(2)]
        public ushort[] Data { get; set; }

        /// <summary>
        /// Gets data length
        /// </summary>
        public int DataLength => this.Width * this.Height;

        /// <summary>
        /// Gets image height
        /// </summary>
        [FieldOrder(1)]
        public ushort Height { get; set; }

        /// <summary>
        /// Material name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets or sets UV map this texture belongs to
        /// </summary>
        public int[] UV { get; set; }

        /// <summary>
        /// Gets image width
        /// </summary>
        [FieldOrder(0)]
        public ushort Width { get; set; }

        /// <summary>
        /// Image hash
        /// </summary>
        private string hash = string.Empty;

        /// <summary>
        /// Gets image hash
        /// </summary>
        public string Hash
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.hash))
                {
                    using (var sha1 = System.Security.Cryptography.SHA1.Create())
                    {
                        this.hash = string.Concat(sha1.ComputeHash(this.Data.SelectMany(pair => new byte[] { (byte)((pair >> 8) & 0xf), (byte)(pair & 0xf) }).ToArray()).Select(x => x.ToString("X2")));
                    }
                }

                return this.hash;
            }
        }

        /// <summary>
        /// Get UV unwrap texture
        /// </summary>
        /// <param name="baseTexture">base texture</param>
        /// <param name="uv">UV coords</param>
        /// <returns>Unwrapped texture</returns>
        public static Texture GetUnwrap(Texture baseTexture, List<Vector3D> uv)
        {
            // Get region bounds
            Vector3D min = new Vector3D(uv.Min(comp => comp.X), uv.Min(comp => comp.Y), 0.0);
            Vector3D max = new Vector3D(uv.Max(comp => comp.X), uv.Max(comp => comp.Y), 0.0);

            // Get unwrap texture size
            ushort width = (ushort)Math.Max(Math.Round((Math.Abs(max.X - min.X) * baseTexture.Width) / 8.0) * 8.0, 8.0);
            ushort height = (ushort)Math.Max((Math.Abs(max.Y - min.Y) * baseTexture.Height), 1.0);

            // New empty texture
            Texture unwrap = new Texture(baseTexture.Name + "+" + Guid.NewGuid().ToString(), width, height, new ushort[width * height]);

            // UV map polygon axies
            Vector3D uvTopDirection = uv[1] - uv[0];
            Vector3D uvBottomDirection = uv[2] - uv[3];
            List<ushort> data = new List<ushort>();

            // Render to rectangle
            for (int y = height - 1; y >= 0; y--)
            {
                double portionY = ((y + 1) / (double)height);

                for (int x = 0; x < width; x++)
                {
                    double portionX = ((x + 1) / (double)width);

                    // Calculate location on quad
                    Vector3D topLocation = uv[0] + (uvTopDirection * portionX);
                    Vector3D bottomLocation = uv[3] + (uvBottomDirection * portionX);
                    Vector3D uvLocation = bottomLocation + ((topLocation - bottomLocation) * portionY);

                    // Calculate location in the UV mapped texture
                    int uvX = (int)(uvLocation.X * (baseTexture.Width - 1));
                    int uvY = (baseTexture.Height - 1) - (int)(uvLocation.Y * (baseTexture.Height - 1));

                    // Handle repeating textures
                    if (uvX >= baseTexture.Width)
                    {
                        uvX %= baseTexture.Width;
                    }
                    else if (uvX < 0)
                    {
                        uvX = baseTexture.Width - (Math.Abs(uvX + 1) % baseTexture.Width) - 1;
                    }

                    if (uvY >= baseTexture.Height)
                    {
                        uvY %= baseTexture.Height;
                    }
                    else if (uvY < 0)
                    {
                        uvY = baseTexture.Height - (Math.Abs(uvY + 1) % baseTexture.Height) - 1;
                    }

                    // Write pixel to rectangle texture
                    data.Add(baseTexture.Data[(uvY * baseTexture.Width) + uvX]);
                }
            }

            unwrap.Data = data.ToArray();
            return unwrap;
        }

        /// <summary>
        /// Get base name
        /// </summary>
        /// <returns>Base name</returns>
        public string GetBaseName()
        {
            var id = this.Name.LastIndexOf('+');

            if (id > 0)
            {
                return this.Name[..id];
            }

            return this.Name;
        }

        /// <summary>
        /// Compares this texture with another one.
        /// The similarity score is based on color similarity (via CIEDE2000) and gradient (average detail/edge strength)
        /// between the 2 images and between sub parts of both images.
        /// </summary>
        /// <param name="other">The texture to compare to.</param>
        /// <returns>A similarity score between 0.0 (completely different) and 100.0 (exactly the same).</returns>
        public double CalculateSimilarityTo(Texture other)
        {
            if (other == null) return 0.0;
            if (ReferenceEquals(this, other)) return 100.0;

            return CalculateRecursiveSimilarity(this, other, 
                                                0, 0, this.Width, this.Height,
                                                0, 0, other.Width, other.Height);
        }

        /// <summary>
        /// Recursive similarity calculation using sliding windows.
        /// </summary>
        private static double CalculateRecursiveSimilarity(
            Texture img1, Texture img2,
            int x1, int y1, int w1, int h1,   // région courante sur img1
            int x2, int y2, int w2, int h2)   // région courante sur img2
        {
            if (w1 * h1 < 4 || w2 * h2 < 4)
            {
                var avg1 = GetAverageColor(img1, x1, y1, w1, h1);
                var avg2 = GetAverageColor(img2, x2, y2, w2, h2);
                return ColorSimilarity(avg1, avg2);
            }

            // Mean color similarity
            var avgFullImg1 = GetAverageColor(img1, x1, y1, w1, h1);
            var avgFullImg2 = GetAverageColor(img2, x2, y2, w2, h2);
            double colorSimilarity = ColorSimilarity(avgFullImg1, avgFullImg2);

            // Structure similarity via mean gradient
            double grad1 = GetMeanGradient(img1, x1, y1, w1, h1);
            double grad2 = GetMeanGradient(img2, x2, y2, w2, h2);
            double gradientSimilarity = GradientSimilarity(grad1, grad2);

            // The deeper we go, the less relevant is structure similarity
            double depthWeight = (w1 / img1.Width)/2;
            double fullImgSimilarity = colorSimilarity * (1-depthWeight) + gradientSimilarity * (depthWeight);

            int midW1 = (w1 + 1) / 2;
            int midH1 = (h1 + 1) / 2;
            int midW2 = (w2 + 1) / 2;
            int midH2 = (h2 + 1) / 2;

            // We do the same for each quadrant of the img
            double tl = CalculateRecursiveSimilarity(img1, img2, x1, y1, midW1, midH1, x2, y2, midW2, midH2);
            double tr = CalculateRecursiveSimilarity(img1, img2, x1 + midW1, y1, w1 - midW1, midH1, x2 + midW2, y2, w2 - midW2, midH2);
            double bl = CalculateRecursiveSimilarity(img1, img2, x1, y1 + midH1, midW1, h1 - midH1, x2, y2 + midH2, midW2, h2 - midH2);
            double br = CalculateRecursiveSimilarity(img1, img2, x1 + midW1, y1 + midH1, w1 - midW1, h1 - midH1, x2 + midW2, y2 + midH2, w2 - midW2, h2 - midH2);

            double subImgSimilarity = (tl + tr + bl + br) / 4.0;

            // Weight can be adjusted
            return (fullImgSimilarity * 0.2 + subImgSimilarity * 0.8);
        }
        //ça a marché avec maxdeltaE=10, depthWeight = (w1 / img1.Width); et (fullImgSimilarity * 0.2 + subImgSimilarity * 0.8);
        //ça a marché avec avgColor=8 depthWeight = (w1 / img1.Width); et (fullImgSimilarity * 0.2 + subImgSimilarity * 0.8);
        //ça a marché avec avgColor=8 depthWeight = (w1 / img1.Width)/2; et (fullImgSimilarity * 0.2 + subImgSimilarity * 0.8); (threshold)
        //ça a marché avec maxDeltaE = 8 depthWeight = (w1 / img1.Width) et (fullImgSimilarity * 0.2 + subImgSimilarity * 0.8); (threshold 69)
        //ça a marché avec maxDeltaE = 8 depthWeight = (w1 / img1.Width)/2 et (fullImgSimilarity * 0.2 + subImgSimilarity * 0.8); (threshold 68.125)

        /// <summary>
        /// Computes average RGB color of a rectangular region in the texture.
        /// </summary>
        private static (byte R, byte G, byte B) GetAverageColor(Texture tex, int startX, int startY, int width, int height)
        {
            long sumR = 0, sumG = 0, sumB = 0;
            int count = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int px = startX + x;
                    int py = startY + y;

                    if (px < tex.Width && py < tex.Height)
                    {
                        ushort pixel = tex.Data[py * tex.Width + px];
                        byte r = (byte)((pixel & 0x1F) << 3);
                        byte g = (byte)(((pixel >> 5) & 0x1F) << 3);
                        byte b = (byte)(((pixel >> 10) & 0x1F) << 3);

                        sumR += r;
                        sumG += g;
                        sumB += b;
                        count++;
                    }
                }
            }

            if (count == 0) return (0, 0, 0);

            return (
                (byte)(sumR / count),
                (byte)(sumG / count),
                (byte)(sumB / count)
            );
        }

        /// <summary>
        /// Similarity between two RGB colors (0 to 100).
        /// Uses a simple RGB difference between the colors.
        /// </summary>
        private static double ColorSimilarity((byte R, byte G, byte B) c1, (byte R, byte G, byte B) c2)
        {
            double simR = (255.0 - Math.Abs(c1.R - c2.R)) / 255.0;
            double simG = (255.0 - Math.Abs(c1.G - c2.G)) / 255.0;
            double simB = (255.0 - Math.Abs(c1.B - c2.B)) / 255.0;

            double avgSim = (simR + simG + simB) / 3.0 * 100.0;
            const double maxDelta = 8; //Difference above which we consider 0% similarity

            return Math.Max(0, 100 * ((avgSim - (100 - maxDelta)) / maxDelta));
        }

        /// <summary>
        /// Perceptual similarity between two RGB colors (0 to 100).
        /// Uses RGB → LAB conversion + CIEDE2000 formula.
        /// </summary>
        // private static double ColorSimilarity((byte R, byte G, byte B) c1, (byte R, byte G, byte B) c2)
        // {
        //     if (c1.R == c2.R && c1.G == c2.G && c1.B == c2.B)
        //         return 100.0;

        //     // Conversion RGB → XYZ → LAB
        //     var lab1 = RgbToLab(c1.R, c1.G, c1.B);
        //     var lab2 = RgbToLab(c2.R, c2.G, c2.B);

        //     double deltaE = CieDE2000(lab1.L, lab1.a, lab1.b, lab2.L, lab2.a, lab2.b);

        //     // Mapping Delta E to 0-100 score
        //     // Delta E < 1  = imperceptible
        //     // Delta E ~ 2-4 = very close
        //     // Delta E 10+   = clearly different colors
        //     const double maxDeltaE = 8.0; // Value beyond which we consider 0% similarity

        //     double similarity = Math.Max(0.0, 100.0 * (1.0 - deltaE / maxDeltaE));
        //     return similarity;
        // }


        private static (double L, double a, double b) RgbToLab(byte r, byte g, byte blue)
        {
            // 1. RGB → sRGB linear
            double rr = r / 255.0;
            double gg = g / 255.0;
            double bb = blue / 255.0;

            rr = rr > 0.04045 ? Math.Pow((rr + 0.055) / 1.055, 2.4) : rr / 12.92;
            gg = gg > 0.04045 ? Math.Pow((gg + 0.055) / 1.055, 2.4) : gg / 12.92;
            bb = bb > 0.04045 ? Math.Pow((bb + 0.055) / 1.055, 2.4) : bb / 12.92;

            // 2. sRGB → XYZ (D65)
            double x = 0.4124 * rr + 0.3576 * gg + 0.1805 * bb;
            double y = 0.2126 * rr + 0.7152 * gg + 0.0722 * bb;
            double z = 0.0193 * rr + 0.1192 * gg + 0.9505 * bb;

            // 3. XYZ → LAB (D65)
            x /= 0.95047;
            y /= 1.00000;
            z /= 1.08883;

            x = x > 0.008856 ? Math.Pow(x, 1.0 / 3.0) : (7.787 * x) + (16.0 / 116.0);
            y = y > 0.008856 ? Math.Pow(y, 1.0 / 3.0) : (7.787 * y) + (16.0 / 116.0);
            z = z > 0.008856 ? Math.Pow(z, 1.0 / 3.0) : (7.787 * z) + (16.0 / 116.0);

            double L = 116.0 * y - 16.0;
            double aValue = 500.0 * (x - y);
            double bValue = 200.0 * (y - z);

            return (L, aValue, bValue);
        }

        private static double CieDE2000(double L1, double a1, double b1, double L2, double a2, double b2)
        {
            const double kL = 1.0, kC = 1.0, kH = 1.0;

            double dL = L2 - L1;
            double C1 = Math.Sqrt(a1 * a1 + b1 * b1);
            double C2 = Math.Sqrt(a2 * a2 + b2 * b2);
            double dC = C2 - C1;

            double h1 = Math.Atan2(b1, a1);
            double h2 = Math.Atan2(b2, a2);
            double dh = h2 - h1;

            if (dh > Math.PI) dh -= 2 * Math.PI;
            if (dh < -Math.PI) dh += 2 * Math.PI;

            double dH = 2 * Math.Sqrt(C1 * C2) * Math.Sin(dh / 2.0);

            double Lm = (L1 + L2) / 2.0;
            double Cm = (C1 + C2) / 2.0;
            double hm = (h1 + h2) / 2.0;

            double T = 1 - 0.17 * Math.Cos(hm - Math.PI / 6) 
                    + 0.24 * Math.Cos(2 * hm) 
                    + 0.32 * Math.Cos(3 * hm + Math.PI / 30) 
                    - 0.20 * Math.Cos(4 * hm - 63 * Math.PI / 180);

            double SL = 1 + (0.015 * (Lm - 50) * (Lm - 50)) / Math.Sqrt(20 + (Lm - 50) * (Lm - 50));
            double SC = 1 + 0.045 * Cm;
            double SH = 1 + 0.015 * Cm * T;

            double dTheta = 30 * Math.Exp(-((hm - 275 * Math.PI / 180) * (hm - 275 * Math.PI / 180)) / (25 * 25));
            double RC = 2 * Math.Sqrt(Math.Pow(Cm, 7) / (Math.Pow(Cm, 7) + Math.Pow(25, 7)));
            double RT = -Math.Sin(2 * dTheta) * RC;

            double dL_ = dL / (kL * SL);
            double dC_ = dC / (kC * SC);
            double dH_ = dH / (kH * SH);

            return Math.Sqrt(dL_ * dL_ + dC_ * dC_ + dH_ * dH_ + RT * dC_ * dH_);
        }

        /// <summary>
        /// Simple similarity between two gradient values.
        /// </summary>
        private static double GradientSimilarity(double g1, double g2)
        {
            if (g1 == 0 && g2 == 0) return 100.0;
            if (g1 == 0 || g2 == 0) return 0.0;

            double ratio = Math.Min(g1, g2) / Math.Max(g1, g2);
            return ratio * 100.0;
        }

        /// <summary>
        /// Calculates the mean gradient (average detail/edge strength) of a region.
        /// Higher value = more details/texture variation.
        /// </summary>
        private static double GetMeanGradient(Texture tex, int startX, int startY, int width, int height)
        {
            if (width < 2 || height < 2) return 0.0;

            long totalGradient = 0;
            int count = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int px = startX + x;
                    int py = startY + y;

                    if (px >= tex.Width - 1 || py >= tex.Height - 1)
                        continue;

                    ushort p1 = tex.Data[py * tex.Width + px];           // current pixel
                    ushort p2 = tex.Data[py * tex.Width + (px + 1)];     // right
                    ushort p3 = tex.Data[(py + 1) * tex.Width + px];     // below

                    byte r1 = (byte)((p1 & 0x1F) << 3);
                    byte g1 = (byte)(((p1 >> 5) & 0x1F) << 3);
                    byte b1 = (byte)(((p1 >> 10) & 0x1F) << 3);

                    byte r2 = (byte)((p2 & 0x1F) << 3);
                    byte g2 = (byte)(((p2 >> 5) & 0x1F) << 3);
                    byte b2 = (byte)(((p2 >> 10) & 0x1F) << 3);

                    byte r3 = (byte)((p3 & 0x1F) << 3);
                    byte g3 = (byte)(((p3 >> 5) & 0x1F) << 3);
                    byte b3 = (byte)(((p3 >> 10) & 0x1F) << 3);

                    // Simple luminance approximation
                    int lum1 = (r1 * 299 + g1 * 587 + b1 * 114) / 1000;
                    int lum2 = (r2 * 299 + g2 * 587 + b2 * 114) / 1000;
                    int lum3 = (r3 * 299 + g3 * 587 + b3 * 114) / 1000;

                    totalGradient += Math.Abs(lum1 - lum2) + Math.Abs(lum1 - lum3);
                    count++;
                }
            }

            return count == 0 ? 0.0 : (double)totalGradient / count;
        }

        /// <summary>
        /// Compare this texture with another one using CoenM.ImageHash's algorithm.
        /// </summary>
        /// <param name="other">The texture to compare to.</param>
        /// <returns>A similarity score between 0 (completely different) and 100 (exactly the same).</returns>
        // public double CalculateSimilarityTo(Texture other)
        // {
        //     if (other == null) return 0.0;
        //     if (ReferenceEquals(this, other)) return 100.0; // 100% si même objet (correction : base 0-100)

        //     ulong myHash = this.ImageHashValue;
        //     ulong otherHash = other.ImageHashValue;

        //     return CoenM.ImageHash.CompareHash.Similarity(myHash, otherHash);
        // }

        public SLIS.Image<SLIS.PixelFormats.Rgba32> ToImageSharp()
        {
            // 2. Changement du type d'instanciation ici : Rgba32
            var image = new SLIS.Image<SLIS.PixelFormats.Rgba32>(this.Width, this.Height);
            
            for (int y = 0; y < this.Height; y++)
            {
                for (int x = 0; x < this.Width; x++)
                {
                    ushort abgr555 = this.Data[(y * this.Width) + x];
                    
                    // Décodage natif Saturn ABGR555 -> RGBA 32 bits
                    byte a = (byte)(((abgr555 >> 15) & 0x01) * 255);
                    byte b = (byte)(((abgr555 >> 10) & 0x1F) << 3);
                    byte g = (byte)(((abgr555 >> 5)  & 0x1F) << 3);
                    byte r = (byte)((abgr555         & 0x1F) << 3);

                    // 3. Remplissage avec le constructeur Rgba32 standard
                    image[x, y] = new SLIS.PixelFormats.Rgba32(r, g, b, a);
                }
            }

            return image;
        }

        // private ulong? _imageHashCache;

        // private ulong ImageHashValue
        // {
        //     get
        //     {
        //         if (!_imageHashCache.HasValue)
        //         {
        //             _imageHashCache = _hasher.Hash(ToImageSharp());
        //         }
        //         return _imageHashCache.Value;
        //     }
        // }
    }
}