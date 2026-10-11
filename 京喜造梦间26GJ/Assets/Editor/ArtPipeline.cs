using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CuoGuo.EditorTools
{
    /// <summary>
    /// 美术资产管线：把 Assets/Art/Source_Keep 下的 24 张源图处理成可用素材。
    ///
    /// 原则（照最新美术执行方案）：
    /// - 源图只读，绝不覆盖；所有产物写到 Processed；
    /// - 源图全是 RGB JPEG、没有 Alpha，图中棋盘格是**烘焙进去的像素**，不是透明，
    ///   所以独立 Sprite 必须真的做抠图，不能只改扩展名；
    /// - 背景不需要透明，可直接用，但要清掉烘焙进去的 HUD / 标题 / 装饰边；
    /// - 处理后自动裁到不透明内容的包围盒，否则人物会缩在画面中间一小块；
    /// - 处理完把成品镜像一份到 Assets/Resources/Art，运行时用 Resources.Load 取图。
    ///
    /// 调用方式：
    ///   Unity.exe -batchmode -quit -nographics -projectPath &lt;工程&gt; -logFile &lt;日志&gt; `
    ///     -executeMethod CuoGuo.EditorTools.ArtPipeline.RunAll
    /// </summary>
    public static class ArtPipeline
    {
        private const string SourceDir = "Assets/Art/Source_Keep";
        private const string ProcessedDir = "Assets/Art/Processed";
        private const string RuntimeDir = "Assets/Resources/Art";

        /// <summary>
        /// 顺着渐变推进时相邻像素允许的色差。
        /// 11 对水彩柔边太大——填充会沿人物柔和的边缘"渗"进去，在手臂和衣服上啃出缺口；
        /// 5 能走平纸底的渐变，同时在水彩轮廓那一跳上停住。
        /// </summary>
        private const int StepTolerance = 5;

        private static readonly List<string> Report = new List<string>();

        [MenuItem("错过了那一秒/4. 处理美术资源（抠图/修补/裁边/导入）")]
        public static void RunAll()
        {
            Report.Clear();
            Report.Add("# 美术资产审计与处理报告");
            Report.Add(string.Empty);
            Report.Add("生成方式：`Unity -batchmode -executeMethod CuoGuo.EditorTools.ArtPipeline.RunAll`");
            Report.Add("源图目录：`" + SourceDir + "`（只读，未修改）；产物目录：`" + ProcessedDir + "`");
            Report.Add(string.Empty);

            // 这两个目录是**完全由本管线生成的产物**，每次重跑先清空。
            // 否则改动（例如收紧 Resources 白名单、调整某张图的判据）之后，
            // 上一轮留下的旧文件会继续留在原地，看起来像"改动没生效"。
            ClearGenerated(ProcessedDir);
            ClearGenerated(RuntimeDir);

            // ---- 背景：不需要透明，直接转 PNG，必要时先修补/裁边 ----
            PlainBackground("A01", "Prologue/PR01_BG_Archive");
            PlainBackground("A02", "Prologue/PR02_BG_Boat");
            PlainBackground("A03", "Chapter01_Family/CH01_BG_Far");

            // A11：右上角烘焙了"罗盘 + 时间条"HUD，外圈还有旧纸相框。先裁边再去 HUD。
            PatchThenSave("A11", "Chapter02_Friendship/CH02_BG_Clean",
                new[] { 20, 20, 20, 20 },                       // 上左下右各裁 20 像素，去掉相框
                new[] { 1800, 30, 900, 170, -760, 0 });         // HUD 区域 → 从左边 -760 处取干净天空盖住

            PlainBackground("A23", "Ending/END_Restored");
            PlainBackground("A24", "Ending/END_Wide");

            // 参数依次是：与边框底色的**色距容差**、**亮度下限**。
            // 亮度下限负责吃掉米黄纸底和棋盘格的两档灰白；色距负责吃掉与底色接近的杂边。
            // 纸船与海图的主体本身就接近白色，所以把亮度下限提到 250（等于关掉这条），
            // 只靠"与边框色接近"来抠——这两张上一版已经抠干净了，不要动它。

            // ---- 人物：暖色纸底，做真透明 + 自动裁边 ----
            KeySprite("A06", "Chapter01_Family/CH01_Mother", 44, 205);
            KeySprite("A07", "Chapter01_Family/CH01_Boy", 44, 205);
            KeySprite("A12", "Chapter02_Friendship/CH02_YoungMan", 44, 205);
            KeySprite("A13", "Chapter02_Friendship/CH02_Friend", 44, 205);
            KeySprite("A18", "Chapter03_Love/CH03_Man", 44, 205);

            // ---- 共享木船母版：三章共用同一艘（策划案要求船头方向与三角刻痕一致）----
            KeySprite("A08", "Shared/Boat_Master", 44, 205);

            // ---- 关键道具 ----
            KeySprite("A09", "Shared/Compass", 46, 192);                    // 棋盘格 + 浅色分隔线
            KeySprite("A15", "Chapter02_Friendship/CH02_SeaChart", 46, 250);
            KeySprite("A21", "Chapter03_Love/CH03_PaperBoat", 34, 250);

            // ---- 未处理：如实登记 ----
            Report.Add("## 未处理 / 仅作参考的源图");
            Report.Add(string.Empty);
            Report.Add("| ID | 决定 | 原因 |");
            Report.Add("|---|---|---|");
            Report.Add("| A17 | **仅作风格参考，不进可玩场景** | 是海报排版：正中压着 1500 像素宽的大标题、底部整条白色撕纸装饰带、右上角有 logo。"
                       + "在水平线上抹掉这么一大块只会糊成一片，比留着更糟。按方案原话「无法自然清理时重生成」，"
                       + "第三章继续使用程序化夜港背景。 |");
            Report.Add("| A19 | **不承担角色动作** | 是带红色字母标记的剪影海报，不是可动画的透明角色 Sprite。"
                       + "方案原文也写「不可承担关卡动作」，故第三章女孩仍用程序化角色，等重生成。 |");
            Report.Add("| A20 | 不用作独立木船 | 含纸拼贴底与底部搜索栏式 UI，是组合图不是单体 Sprite。 |");
            Report.Add("| A04/A05/A14 | 本轮未接入 | 需要先分层/蒙版；当前背景已由 A03/A11 承担，时间盒内不冒险。 |");
            Report.Add("| A10/A16/A22 | 不整张使用 | 分别是带长安装板的门把手、带整条静态轨道的滑环、嵌在装饰蓝底板里的手柄。"
                       + "游戏内滑块已是「可拖动把手 + 静态轨道」的分离结构，逻辑与命中区都已通过运行时断言，不作替换以免破坏已验证的交互。 |");
            Report.Add(string.Empty);

            string reportPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "_doc", "ART_ASSET_AUDIT.md");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, string.Join("\n", Report.ToArray()), new System.Text.UTF8Encoding(true));

            AssetDatabase.Refresh();
            ApplyImportSettings();

            Debug.Log("[美术管线] 完成。报告：" + reportPath);
            for (int i = 0; i < Report.Count; i++)
            {
                if (Report[i].StartsWith("| ") || Report[i].StartsWith("## ") || Report[i].StartsWith("- "))
                {
                    Debug.Log("[美术管线] " + Report[i]);
                }
            }
        }

        /// <summary>清空一个生成目录（连同 .meta）。</summary>
        private static void ClearGenerated(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                return;
            }

            string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // .meta 交给 AssetDatabase 处理
                }

                File.Delete(files[i]);
            }
        }

        // ------------------------------------------------------------ 背景

        /// <summary>背景不需要透明：直接把源图写成 PNG。</summary>
        private static void PlainBackground(string id, string outName)
        {
            Texture2D tex = LoadSource(id);
            if (tex == null)
            {
                return;
            }

            SavePng(tex, outName);
            Report.Add("- `" + id + "` → `" + outName + ".png`（背景，直接使用，未改像素）");
        }

        /// <summary>先按四边裁掉相框，再从 sourceOffset 处取一块干净画面盖住 rect 区域的烘焙 UI。</summary>
        private static void PatchThenSave(string id, string outName, int[] cropLtrb, int[] patch)
        {
            Texture2D tex = LoadSource(id);
            if (tex == null)
            {
                return;
            }

            if (cropLtrb != null && cropLtrb.Length == 4)
            {
                tex = Crop(tex, cropLtrb[0], cropLtrb[1], tex.width - cropLtrb[0] - cropLtrb[2], tex.height - cropLtrb[1] - cropLtrb[3]);
            }

            if (patch != null && patch.Length == 6)
            {
                PatchRect(tex, patch[0], patch[1], patch[2], patch[3], patch[4], patch[5]);
            }

            SavePng(tex, outName);
            Report.Add("- `" + id + "` → `" + outName + ".png`（裁掉外圈旧纸相框 20 像素；"
                       + "用左侧干净天空覆盖了右上角烘焙的「罗盘 + 时间条」HUD）");
        }

        // ------------------------------------------------------------ 抠图

        /// <summary>
        /// 把纸底/棋盘格变成真透明。
        ///
        /// 判据不是"够不够白"，而是**离边框实际颜色有多远**——
        /// 因为源图的底有米黄纸底、冷灰纸底、棋盘格好几种，用固定亮度阈值会漏掉暖色底
        /// （第一版就踩了这个坑：米黄底色差 30，被判成前景，等于没抠）。
        ///
        /// 仍然坚持"从四边泛洪、只吃连通区域"，所以被深色轮廓包住的浅色内部
        /// （罗盘的玻璃面、纸船的纸面）不会被误吃。
        /// </summary>
        private static void KeySprite(string id, string outName, int lightTolerance, int lightFloor)
        {
            Texture2D tex = LoadSource(id);
            if (tex == null)
            {
                return;
            }

            int w = tex.width;
            int h = tex.height;
            Color32[] px = tex.GetPixels32();

            // 边框基准色：取四边的平均值
            Color32 bg = BorderColor(px, w, h);
            int tolSq = lightTolerance * lightTolerance;
            int stepSq = StepTolerance * StepTolerance;

            bool[] isBg = new bool[w * h];
            var queue = new Queue<int>();

            for (int x = 0; x < w; x++)
            {
                Seed(px, isBg, queue, x, 0, w, bg, tolSq, lightFloor);
                Seed(px, isBg, queue, x, h - 1, w, bg, tolSq, lightFloor);
            }

            for (int y = 0; y < h; y++)
            {
                Seed(px, isBg, queue, 0, y, w, bg, tolSq, lightFloor);
                Seed(px, isBg, queue, w - 1, y, w, bg, tolSq, lightFloor);
            }

            // 泛洪：只沿与底色相近的连通区域扩散
            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                int cx = idx % w;
                int cy = idx / w;

                TrySpread(px, isBg, queue, idx, cx - 1, cy, w, h, bg, tolSq, lightFloor, stepSq);
                TrySpread(px, isBg, queue, idx, cx + 1, cy, w, h, bg, tolSq, lightFloor, stepSq);
                TrySpread(px, isBg, queue, idx, cx, cy - 1, w, h, bg, tolSq, lightFloor, stepSq);
                TrySpread(px, isBg, queue, idx, cx, cy + 1, w, h, bg, tolSq, lightFloor, stepSq);
            }

            // 写入 alpha：背景透明，前景保留；再对边缘做一次轻微羽化，去掉硬锯齿
            var alpha = new byte[w * h];
            int opaque = 0;
            for (int i = 0; i < alpha.Length; i++)
            {
                alpha[i] = isBg[i] ? (byte)0 : (byte)255;
                if (!isBg[i])
                {
                    opaque++;
                }
            }

            alpha = FeatherAlpha(alpha, w, h);

            var out32 = new Color32[w * h];
            for (int i = 0; i < out32.Length; i++)
            {
                out32[i] = new Color32(px[i].r, px[i].g, px[i].b, alpha[i]);
            }

            float keepRatio = opaque / (float)(w * h);

            // 关键：包围盒与裁剪**全部在这个数组上算**，不再写回纹理再读回来。
            // 之前那版 SetPixels32 之后没有再 Apply，读回来的还是原图，
            // 于是 alpha 全是 255、包围盒永远等于整张图——抠图等于白做。
            int minX;
            int minY;
            int maxX;
            int maxY;
            if (!BoundsFromAlpha(alpha, w, h, out minX, out minY, out maxX, out maxY))
            {
                Report.Add("- `" + id + "` ✗ 抠图后没有任何不透明内容，已跳过（请人工确认）");
                return;
            }

            int pad = 4;
            minX = Mathf.Max(0, minX - pad);
            minY = Mathf.Max(0, minY - pad);
            maxX = Mathf.Min(w - 1, maxX + pad);
            maxY = Mathf.Min(h - 1, maxY + pad);

            int cw = maxX - minX + 1;
            int ch = maxY - minY + 1;
            Color32[] cropped = SliceArray(out32, w, minX, minY, cw, ch);

            var outTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            outTex.SetPixels32(cropped);
            outTex.Apply();
            SavePng(outTex, outName);

            Report.Add("- `" + id + "` → `" + outName + ".png`（真透明：四边泛洪抠掉浅底，边缘羽化，"
                       + "裁到内容包围盒 " + cw + "×" + ch + "；保留内容占原图 "
                       + (keepRatio * 100f).ToString("0") + "%）");
            if (keepRatio < 0.02f || keepRatio > 0.98f)
            {
                Report.Add("  - ⚠ 抠图比例异常，需人工确认 `" + id + "`");
            }
        }

        /// <summary>从 alpha 数组直接算不透明内容的包围盒，按行/列计数，避免零星噪点撑满整张图。</summary>
        private static bool BoundsFromAlpha(byte[] alpha, int w, int h, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = w;
            maxX = -1;
            minY = h;
            maxY = -1;

            int need = Mathf.Max(8, w / 80);
            int inset = 4;

            for (int y = inset; y < h - inset; y++)
            {
                int count = 0;
                int row = y * w;
                for (int x = inset; x < w - inset; x++)
                {
                    if (alpha[row + x] > 60)
                    {
                        count++;
                    }
                }

                if (count >= need)
                {
                    if (y < minY) { minY = y; }
                    if (y > maxY) { maxY = y; }
                }
            }

            for (int x = inset; x < w - inset; x++)
            {
                int count = 0;
                for (int y = inset; y < h - inset; y++)
                {
                    if (alpha[y * w + x] > 60)
                    {
                        count++;
                    }
                }

                if (count >= need)
                {
                    if (x < minX) { minX = x; }
                    if (x > maxX) { maxX = x; }
                }
            }

            return maxX >= minX && maxY >= minY;
        }

        /// <summary>从像素数组里切一块出来（不做纹理往返）。</summary>
        private static Color32[] SliceArray(Color32[] src, int srcWidth, int x, int y, int w, int h)
        {
            var dst = new Color32[w * h];
            for (int row = 0; row < h; row++)
            {
                Array.Copy(src, (y + row) * srcWidth + x, dst, row * w, w);
            }

            return dst;
        }

        /// <summary>取四边的平均颜色作为"底色"基准（隔几个像素采一次，够准又快）。</summary>
        private static Color32 BorderColor(Color32[] px, int w, int h)
        {
            long r = 0;
            long g = 0;
            long b = 0;
            long n = 0;

            for (int x = 0; x < w; x += 4)
            {
                Add(px, x, 0, w, ref r, ref g, ref b, ref n);
                Add(px, x, h - 1, w, ref r, ref g, ref b, ref n);
            }

            for (int y = 0; y < h; y += 4)
            {
                Add(px, 0, y, w, ref r, ref g, ref b, ref n);
                Add(px, w - 1, y, w, ref r, ref g, ref b, ref n);
            }

            return new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
        }

        private static void Add(Color32[] px, int x, int y, int w, ref long r, ref long g, ref long b, ref long n)
        {
            Color32 c = px[y * w + x];
            r += c.r;
            g += c.g;
            b += c.b;
            n++;
        }

        /// <summary>
        /// 是否为背景。两条并列的判据：
        /// 1. 与边框底色足够接近（适合彩色纸底）；
        /// 2. 本身足够亮（适合棋盘格——它的两档灰白和之间的浅色分隔线都够亮）。
        ///
        /// 之前只让"色距接近"生效、亮度门槛又定到 244，导致米黄纸底（最低通道 215）
        /// 被当成前景，整张人物图原样留着底——这是本次返工的起因。
        /// </summary>
        private static bool IsBackground(Color32 c, Color32 bg, int tolSq, int lightFloor)
        {
            if (Mathf.Min(c.r, Mathf.Min(c.g, c.b)) >= lightFloor)
            {
                return true;
            }

            int dr = c.r - bg.r;
            int dg = c.g - bg.g;
            int db = c.b - bg.b;
            return dr * dr + dg * dg + db * db <= tolSq;
        }

        private static void Seed(Color32[] px, bool[] bg, Queue<int> q, int x, int y, int w, Color32 reference, int tolSq, int lightFloor)
        {
            int idx = y * w + x;
            if (!bg[idx] && IsBackground(px[idx], reference, tolSq, lightFloor))
            {
                bg[idx] = true;
                q.Enqueue(idx);
            }
        }

        /// <summary>
        /// 从 (x,y) 向邻居扩散。关键点：除了"够亮"和"接近边框底色"，还接受
        /// **与当前像素接近**——因为纸底往往是渐变的（第一版整张人物图的底都是渐变，
        /// 用任何固定全局基准都追不上：远端的底色和边框色差得很远，于是整块底被判成前景）。
        /// 逐像素推进能顺着渐变一路走，遇到人物轮廓那种硬边则立刻停住。
        /// </summary>
        private static void TrySpread(Color32[] px, bool[] bg, Queue<int> q, int from, int x, int y, int w, int h, Color32 reference, int tolSq, int lightFloor, int stepSq)
        {
            if (x < 0 || y < 0 || x >= w || y >= h)
            {
                return;
            }

            int idx = y * w + x;
            if (bg[idx])
            {
                return;
            }

            Color32 c = px[idx];
            bool ok = IsBackground(c, reference, tolSq, lightFloor) || IsNear(c, px[from], stepSq);
            if (!ok)
            {
                return;
            }

            bg[idx] = true;
            q.Enqueue(idx);
        }

        /// <summary>与给定像素的色距是否在步进容差内（用于顺着渐变推进）。</summary>
        private static bool IsNear(Color32 a, Color32 b, int stepSq)
        {
            int dr = a.r - b.r;
            int dg = a.g - b.g;
            int db = a.b - b.b;
            return dr * dr + dg * dg + db * db <= stepSq;
        }

        /// <summary>对 alpha 做一遍 3x3 均值，让边缘不那么硬。</summary>
        private static byte[] FeatherAlpha(byte[] src, int w, int h)
        {
            var dst = new byte[src.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int sum = 0;
                    int n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= h)
                        {
                            continue;
                        }

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= w)
                            {
                                continue;
                            }

                            sum += src[ny * w + nx];
                            n++;
                        }
                    }

                    dst[y * w + x] = (byte)(sum / n);
                }
            }

            return dst;
        }

        /// <summary>
        /// 裁到不透明内容的包围盒（人物图四周留白很多，不裁的话在游戏里会缩成很小一块）。
        ///
        /// 判定按**行/列的不透明像素数量**，而不是"这一行有没有一个不透明像素"——
        /// 抠图难免在边缘留下零星残留，只要有几个噪点就会把包围盒撑满整张图。
        /// </summary>
        private static Texture2D TrimToAlpha(Texture2D tex)
        {
            int w = tex.width;
            int h = tex.height;
            Color32[] px = tex.GetPixels32();

            int need = Mathf.Max(3, w / 200);
            int inset = 6;

            int minX = w;
            int maxX = -1;
            int minY = h;
            int maxY = -1;

            for (int y = inset; y < h - inset; y++)
            {
                int count = 0;
                for (int x = inset; x < w - inset; x++)
                {
                    if (px[y * w + x].a > 16)
                    {
                        count++;
                    }
                }

                if (count >= need)
                {
                    if (y < minY) { minY = y; }
                    if (y > maxY) { maxY = y; }
                }
            }

            for (int x = inset; x < w - inset; x++)
            {
                int count = 0;
                for (int y = inset; y < h - inset; y++)
                {
                    if (px[y * w + x].a > 16)
                    {
                        count++;
                    }
                }

                if (count >= need)
                {
                    if (x < minX) { minX = x; }
                    if (x > maxX) { maxX = x; }
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return tex;
            }

            // 四周再留一点余量，避免把边缘贴得太紧
            int pad = 4;
            minX = Mathf.Max(0, minX - pad);
            minY = Mathf.Max(0, minY - pad);
            maxX = Mathf.Min(w - 1, maxX + pad);
            maxY = Mathf.Min(h - 1, maxY + pad);

            return Crop(tex, minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        // ------------------------------------------------------------ 基础图像操作

        private static Texture2D Crop(Texture2D tex, int x, int y, int w, int h)
        {
            w = Mathf.Clamp(w, 1, tex.width - x);
            h = Mathf.Clamp(h, 1, tex.height - y);
            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels32(Slice(tex, x, y, w, h));
            outTex.Apply();
            return outTex;
        }

        private static Color32[] Slice(Texture2D tex, int x, int y, int w, int h)
        {
            Color32[] src = tex.GetPixels32();
            var dst = new Color32[w * h];
            for (int row = 0; row < h; row++)
            {
                Array.Copy(src, (y + row) * tex.width + x, dst, row * w, w);
            }

            return dst;
        }

        /// <summary>把 (x+dx, y+dy) 处的画面整块复制到 (x, y)。用于盖掉烘焙进去的 UI。</summary>
        private static void PatchRect(Texture2D tex, int x, int y, int w, int h, int dx, int dy)
        {
            Color32[] px = tex.GetPixels32();
            for (int row = 0; row < h; row++)
            {
                int ty = y + row;
                int sy = ty + dy;
                if (ty < 0 || ty >= tex.height || sy < 0 || sy >= tex.height)
                {
                    continue;
                }

                for (int col = 0; col < w; col++)
                {
                    int tx = x + col;
                    int sx = tx + dx;
                    if (tx < 0 || tx >= tex.width || sx < 0 || sx >= tex.width)
                    {
                        continue;
                    }

                    px[ty * tex.width + tx] = px[sy * tex.width + sx];
                }
            }

            tex.SetPixels32(px);
        }

        /// <summary>
        /// 直接读文件字节再 LoadImage。
        /// 不走 AssetDatabase 是因为工程里的源图默认不可读（isReadable=false），
        /// GetPixels32 会直接抛异常；而且这样也不必为了处理去改源图的导入设置。
        /// </summary>
        private static Texture2D LoadSource(string id)
        {
            string path = SourceDir + "/" + id + ".jpg";
            if (!File.Exists(path))
            {
                Report.Add("- `" + id + "` ✗ 源图缺失（" + path + "）");
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                Report.Add("- `" + id + "` ✗ 解码失败");
                return null;
            }

            return tex;
        }

        /// <summary>
        /// 只有游戏运行时会真正用到的图才镜像到 Assets/Resources ——
        /// Resources 下的每一张都会被无条件打进包，第一版把所有成品都放进去，
        /// 包体从 96 MB 涨到 281 MB。素材库留在 Processed，运行时资源只放用到的。
        /// </summary>
        private static readonly HashSet<string> UsedAtRuntime = new HashSet<string>
        {
            "CH02_BG_Clean",   // 第二章背景（A11 已清掉烘焙 HUD）
            "CH01_Mother", "CH01_Boy", "CH02_YoungMan", "CH02_Friend",
            "Boat_Master", "Compass", "CH02_SeaChart", "CH03_PaperBoat"
            // CH03_Man 暂不列入：抠图在白色线稿缝上啃出了缺口，未接入（见 ART_FIX_LOG）
        };

        private static void SavePng(Texture2D tex, string outName)
        {
            byte[] bytes = tex.EncodeToPNG();

            string path = ProcessedDir + "/" + outName + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);

            string flatName = Path.GetFileName(outName);
            if (!UsedAtRuntime.Contains(flatName))
            {
                return;
            }

            string runtimePath = RuntimeDir + "/" + flatName + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(runtimePath));
            File.WriteAllBytes(runtimePath, bytes);
        }

        // ------------------------------------------------------------ 导入设置

        /// <summary>
        /// 统一设置导入参数：Sprite、真实透明、不压缩、不生成 Mipmap。
        /// 背景保持宽高比，不加 padding；人物保持中心轴心——游戏里按目标身高缩放并让脚底落在栈道上，
        /// 与现有的时间轴坐标体系（SceneMetrics 的栈道面）保持一致。
        /// </summary>
        private static void ApplyImportSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { RuntimeDir });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;

                // 画布是 1920×1080，源图最大 2848 宽，压到 2048 足够；
                // 不压缩的 PNG 会让包体从 96 MB 涨到 281 MB，这里必须压。
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
        }
    }
}
