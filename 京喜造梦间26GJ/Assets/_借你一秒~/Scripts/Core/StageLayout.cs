using System.Collections.Generic;

namespace JieNiYiMiao
{
    /// <summary>
    /// 与引擎无关的矩形，用来做舞台布局与点击命中计算。
    /// 之所以自己写一个：Core 目录不能引用 UnityEngine（见 _doc/01-技术基线.md）。
    /// </summary>
    public struct LayoutRect
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public LayoutRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float Left
        {
            get { return X - Width * 0.5f; }
        }

        public float Right
        {
            get { return X + Width * 0.5f; }
        }

        public float Top
        {
            get { return Y + Height * 0.5f; }
        }

        public float Bottom
        {
            get { return Y - Height * 0.5f; }
        }

        public bool Contains(float pointX, float pointY)
        {
            return pointX >= Left && pointX <= Right && pointY >= Bottom && pointY <= Top;
        }

        public bool Overlaps(LayoutRect other)
        {
            return Left < other.Right && Right > other.Left && Bottom < other.Top && Top > other.Bottom;
        }

        public float OverlapArea(LayoutRect other)
        {
            float width = Min(Right, other.Right) - Max(Left, other.Left);
            float height = Min(Top, other.Top) - Max(Bottom, other.Bottom);
            if (width <= 0f || height <= 0f)
            {
                return 0f;
            }

            return width * height;
        }

        public override string ToString()
        {
            return "(" + X.ToString("0") + "," + Y.ToString("0") + " " + Width.ToString("0") + "x" + Height.ToString("0") + ")";
        }

        private static float Min(float a, float b)
        {
            return a < b ? a : b;
        }

        private static float Max(float a, float b)
        {
            return a > b ? a : b;
        }
    }

    /// <summary>舞台上一个可点击对象的位置与尺寸。</summary>
    public class StageBox
    {
        public string Id;
        public bool IsProp;
        public LayoutRect Rect;

        public StageBox(string id, bool isProp, LayoutRect rect)
        {
            Id = id;
            IsProp = isProp;
            Rect = rect;
        }
    }

    /// <summary>
    /// 舞台布局的纯计算部分。SceneBuilder 拿它来摆位置，校验程序拿它来断言
    /// "人物和关键物件不重叠""每段记忆的主角都点得到"。
    ///
    /// 坐标系统：画布中心为原点，1 单位 = 1 个 UI 像素，参考分辨率 1920x1080。
    /// </summary>
    public static class StageLayout
    {
        public const float CanvasWidth = 1920f;
        public const float CanvasHeight = 1080f;

        public const float ActorWidth = 150f;
        public const float ActorHeight = 330f;
        public const float SideActorWidth = 140f;
        public const float SideActorHeight = 300f;

        /// <summary>点击范围相对造型外框收一点，避免边缘误判。</summary>
        public const float HitShrink = 0.92f;

        /// <summary>
        /// 人物的可点矩形（画一个人的外框）。
        /// 注意：这个矩形比 ActorHeight 略矮、中心略低，因为造型本身的
        /// 头顶（头发）和脚下（鞋）并不是可点区域，硬套外框会让点击落在空气上。
        /// </summary>
        public const float ActorHitWidth = 150f;
        public const float ActorHitHeight = 314f;
        public const float ActorHitCenterOffsetY = -10f;

        /// <summary>第二关手机的中心点（SceneBuilder 的桌边造型也用这一份数据）。</summary>
        public const float PhoneX = -260f;
        public const float PhoneY = -200f;

        /// <summary>每段记忆三个槽位的中心点。</summary>
        public static void SlotAnchor(StageSlot slot, out float x, out float y)
        {
            switch (slot)
            {
                case StageSlot.Left:
                    x = -520f;
                    y = -20f;
                    break;
                case StageSlot.Right:
                    x = 430f;
                    y = -20f;
                    break;
                default:
                    x = -40f;
                    y = -20f;
                    break;
            }
        }

        public static LayoutRect ActorRect(StageSlot slot)
        {
            float x;
            float y;
            SlotAnchor(slot, out x, out y);
            return new LayoutRect(x, y + ActorHitCenterOffsetY, ActorHitWidth, ActorHitHeight);
        }

        /// <summary>场上第二个人（母亲 / 同事 / 孙女）的站位。</summary>
        public static void SideActorAnchor(MemoryLevelData level, out float x, out float y)
        {
            if (level != null && level.Slot == StageSlot.Right)
            {
                x = -520f;
                y = -120f;
            }
            else
            {
                x = 520f;
                y = -130f;
            }
        }

        public static LayoutRect SideActorRect(MemoryLevelData level)
        {
            float x;
            float y;
            SideActorAnchor(level, out x, out y);
            return new LayoutRect(x, y, SideActorWidth, SideActorHeight);
        }

        /// <summary>关键物件的中心点与尺寸（按每段记忆各自的造型）。</summary>
        public static LayoutRect PropRect(MemoryLevelData level)
        {
            float x;
            float y;
            float width;
            float height;
            PropAnchor(level, out x, out y, out width, out height);
            return new LayoutRect(x, y, width, height);
        }

        public static void PropAnchor(MemoryLevelData level, out float x, out float y, out float width, out float height)
        {
            string style = level != null ? level.BackdropStyle : "station";
            switch (style)
            {
                case "office":
                    // 手机：放在手够得到的桌角，必须避开人物脚下的可点范围
                    x = PhoneX;
                    y = PhoneY;
                    width = 76f;
                    height = 132f;
                    break;
                case "attic":
                    // 木船：摊在窗边的工作台上，船头让开孙女的站位
                    x = -90f;
                    y = -225f;
                    width = 300f;
                    height = 190f;
                    break;
                default:
                    // 木船：放在站台黄线旁的地面上
                    x = -240f;
                    y = -220f;
                    width = 170f;
                    height = 112f;
                    break;
            }
        }

        /// <summary>当前这段记忆里所有可点击对象（人物 + 关键物件）。</summary>
        public static List<StageBox> Interactives(MemoryLevelData level)
        {
            var list = new List<StageBox>();
            if (level == null)
            {
                return list;
            }

            list.Add(new StageBox(level.ActorName, false, ActorRect(level.Slot)));
            list.Add(new StageBox(SideActorId(level), false, SideActorRect(level)));
            list.Add(new StageBox(level.PropName, true, PropRect(level)));
            return list;
        }

        /// <summary>布景角色在流程里的 id（和 SceneBuilder 里保持一致）。</summary>
        public static string SideActorId(MemoryLevelData level)
        {
            string style = level != null ? level.BackdropStyle : "station";
            switch (style)
            {
                case "office": return "colleague";
                case "attic": return "granddaughter";
                default: return "mother";
            }
        }

        /// <summary>
        /// 在给定点上"点到的是谁"。返回 null 表示点到了空白处。
        /// 顺序即绘制顺序（后加入的在上层），用来模拟 UGUI 的射线优先级。
        /// </summary>
        public static string HitTest(MemoryLevelData level, float pointX, float pointY)
        {
            string top = null;
            var boxes = Interactives(level);
            for (int i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                if (Shrunk(box.Rect).Contains(pointX, pointY))
                {
                    top = box.Id;
                }
            }

            return top;
        }

        /// <summary>两个可点击对象是否真正互相遮挡（重叠面积超过较小者的 25% 才算问题）。</summary>
        public static bool BoxesConflict(LayoutRect a, LayoutRect b)
        {
            float area = a.OverlapArea(b);
            if (area <= 0f)
            {
                return false;
            }

            float smaller = a.Width * a.Height < b.Width * b.Height ? a.Width * a.Height : b.Width * b.Height;
            return area > smaller * 0.25f;
        }

        public static LayoutRect Shrunk(LayoutRect rect)
        {
            return new LayoutRect(rect.X, rect.Y, rect.Width * HitShrink, rect.Height * HitShrink);
        }
    }
}
