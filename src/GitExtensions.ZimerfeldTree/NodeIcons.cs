// NodeIcons.cs — 16×16 GDI+ icons for the ZimerfeldTree branch hierarchy.
// MIT License — Copyright (c) 2026 Zimerfeld

using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace GitExtensions.ZimerfeldTree;

/// <summary>
/// Generates and caches a 16×16 <see cref="ImageList"/> used by the branch tree.
/// All icons are rendered at runtime with GDI+ — no embedded resources required.
/// </summary>
internal static class NodeIcons
{
    // ── Image-list indices ────────────────────────────────────────────────────

    // Generic node types (kept at original indices for backward compat)
    public const int Branch       = 0;   // local branch    — orange fork
    public const int Remote       = 1;   // remote group    — blue cloud
    public const int Tag          = 2;   // tag leaf        — teal label
    public const int Folder       = 3;   // path folder     — amber
    public const int RemoteBranch = 4;   // remote branch   — green fork

    // Section root icons
    public const int SectionLocal   = 5;  // LOCAL header    — steel-blue monitor
    public const int SectionRemotes = 6;  // REMOTES header  — dark-blue cloud
    public const int SectionTags    = 7;  // TAGS header     — purple ribbon

    // GitFlow-specific branch icons
    public const int BranchMaster  = 8;  // master / main   — gold shield
    public const int BranchDevelop = 9;  // develop         — gray open-end wrench
    public const int BranchFeature = 10; // feature/*       — green leaf
    public const int BranchBugfix  = 11; // bugfix/*        — red ladybug
    public const int BranchRelease = 12; // release/*       — brown package
    public const int BranchHotfix  = 13; // hotfix/*        — orange warning sign
    public const int BranchSupport = 14; // support/*       — gray gear

    private static ImageList? _list;

    // ── Public factory ────────────────────────────────────────────────────────

    /// <summary>Returns the shared <see cref="ImageList"/> (created once, then cached).</summary>
    public static ImageList GetList()
    {
        if (_list is not null) return _list;
        _list = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };

        // 0  local branch   — orange fork
        _list.Images.Add(Fork(Color.FromArgb(0xE0, 0x78, 0x00)));
        // 1  remote group   — blue cloud
        _list.Images.Add(Cloud(Color.FromArgb(0x17, 0x69, 0xCC)));
        // 2  tag            — teal label
        _list.Images.Add(TagLabel(Color.FromArgb(0x0E, 0x81, 0x61)));
        // 3  folder         — amber
        _list.Images.Add(FolderIcon());
        // 4  remote branch  — green fork
        _list.Images.Add(Fork(Color.FromArgb(0x22, 0x8B, 0x22)));
        // 5  LOCAL section  — steel-blue monitor
        _list.Images.Add(Monitor());
        // 6  REMOTES section — darker-blue cloud
        _list.Images.Add(Cloud(Color.FromArgb(0x09, 0x4C, 0xAB)));
        // 7  TAGS section   — purple ribbon
        _list.Images.Add(Ribbon());
        // 8  master/main   — gold shield
        _list.Images.Add(Shield());
        // 9  develop        — gray open-end wrench
        _list.Images.Add(Wrench());
        // 10 feature/*      — green leaf
        _list.Images.Add(Leaf());
        // 11 bugfix/*       — red ladybug
        _list.Images.Add(Ladybug());
        // 12 release/*      — brown package
        _list.Images.Add(Package());
        // 13 hotfix/*       — orange warning sign
        _list.Images.Add(WarningSign());
        // 14 support/*      — gray gear
        _list.Images.Add(Gear());

        return _list;
    }

    // ── Icon renderers ────────────────────────────────────────────────────────

    /// <summary>Git-branch fork: trunk + diagonal arm + 3 circles.</summary>
    private static Bitmap Fork(Color c)
    {
        var bmp = Blank(); using var g = AA(bmp);
        using var pen = Round(c, 1.5f);
        using var b   = new SolidBrush(c);
        g.DrawLine(pen,  4f, 4f, 4f, 12f);   // trunk
        g.DrawLine(pen,  4f, 8f, 12f, 4f);   // branch arm
        g.FillEllipse(b,  2,  0, 4, 4);      // trunk-top   circle
        g.FillEllipse(b, 10,  0, 4, 4);      // feature-tip circle
        g.FillEllipse(b,  2, 12, 4, 4);      // base        circle
        return bmp;
    }

    /// <summary>Cloud shape: 3 overlapping bumps + flat rectangular base.</summary>
    private static Bitmap Cloud(Color c)
    {
        var bmp = Blank(); using var g = AA(bmp);
        using var b = new SolidBrush(c);
        g.FillEllipse(b,  1, 5, 7, 7);    // left bump
        g.FillEllipse(b,  5, 2, 8, 8);    // top  bump
        g.FillEllipse(b,  9, 6, 6, 6);    // right bump
        g.FillRectangle(b, 1, 9, 14, 4);  // flat base
        return bmp;
    }

    /// <summary>Pointed-left price-tag shape with a white string-hole.</summary>
    private static Bitmap TagLabel(Color c)
    {
        var bmp = Blank(); using var g = AA(bmp);
        using var b = new SolidBrush(c);
        using var w = new SolidBrush(Color.White);
        PointF[] pts = [new(5, 2), new(14, 2), new(14, 14), new(5, 14), new(1, 8)];
        g.FillPolygon(b, pts);
        g.FillEllipse(w, 3.5f, 5.5f, 4f, 5f);   // string hole
        return bmp;
    }

    /// <summary>Classic amber folder: darker tab + lighter body.</summary>
    private static Bitmap FolderIcon()
    {
        var bmp = Blank(); using var g = AA(bmp);
        using var dark  = new SolidBrush(Color.FromArgb(0xC9, 0x71, 0x0C));
        using var light = new SolidBrush(Color.FromArgb(0xFB, 0xBF, 0x24));
        g.FillRectangle(dark,  1, 4, 7, 4);   // tab
        g.FillRectangle(light, 1, 7, 14, 8);  // body
        return bmp;
    }

    /// <summary>Steel-blue monitor with light screen and stand — LOCAL section.</summary>
    private static Bitmap Monitor()
    {
        var bmp = Blank(); using var g = AA(bmp);
        using var frame = new SolidBrush(Color.FromArgb(0x2B, 0x5F, 0x8E));  // steel blue
        using var glow  = new SolidBrush(Color.FromArgb(0xA8, 0xD0, 0xF0));  // screen glow
        g.FillRectangle(frame, 2,  1, 12, 9);   // bezel
        g.FillRectangle(glow,  3,  2, 10, 7);   // screen
        g.FillRectangle(frame, 7, 10,  2, 3);   // stand
        g.FillRectangle(frame, 4, 13,  8, 2);   // base
        return bmp;
    }

    /// <summary>
    /// Product label/tag: amber rectangular body, punched hole at top-center,
    /// dark border, and three text-line stripes — TAGS section root.
    /// </summary>
    private static Bitmap Ribbon()
    {
        var bmp = Blank(); using var g = AA(bmp);
        using var bg     = new SolidBrush(Color.FromArgb(0xFF, 0xEC, 0x6A));   // light amber
        using var border = new Pen(Color.FromArgb(0xB5, 0x7C, 0x00), 1f);      // dark amber
        using var lines  = new SolidBrush(Color.FromArgb(0x80, 0x50, 0x00));   // text lines
        using var hole   = new SolidBrush(Color.White);

        // Label body
        g.FillRectangle(bg, 2, 4, 12, 11);
        g.DrawRectangle(border, 2, 4, 12, 11);

        // Hole punch at top-center
        g.FillEllipse(hole, 5, 1, 6, 6);
        g.DrawEllipse(border, 5, 1, 6, 6);

        // Simulated text lines
        g.FillRectangle(lines, 4,  8, 8, 1);
        g.FillRectangle(lines, 4, 11, 6, 1);
        g.FillRectangle(lines, 4, 13, 4, 1);

        return bmp;
    }

    /// <summary>
    /// Heraldic gold shield: rounded-top outline, dark-gold border, and a small
    /// 5-pointed star emblem in the center — master / main branch.
    /// </summary>
    private static Bitmap Shield()
    {
        var bmp = Blank(); using var g = AA(bmp);
        using var fill    = new SolidBrush(Color.FromArgb(0xFF, 0xD7, 0x00));   // gold
        using var border  = new Pen(Color.FromArgb(0xB8, 0x86, 0x00), 1.2f);   // dark gold
        using var starFill = new SolidBrush(Color.FromArgb(0x80, 0x50, 0x00)); // darker star

        // Shield body (pentagon with wider base)
        using var path = new GraphicsPath();
        path.AddArc(2, 1, 12, 6, 180, 180);     // rounded top arch
        path.AddLine(14, 4, 14, 9);              // right side
        path.AddLine(14, 9, 8, 15);              // right-bottom diagonal
        path.AddLine(8, 15, 2, 9);              // left-bottom diagonal
        path.AddLine(2, 9, 2, 4);               // left side
        path.CloseFigure();
        g.FillPath(fill, path);
        g.DrawPath(border, path);

        // 5-pointed star at center
        const float SX = 8f, SY = 7.5f, RO = 3.5f, RI = 1.5f;
        var star = new PointF[10];
        for (int i = 0; i < 10; i++)
        {
            float a = i * MathF.PI / 5f - MathF.PI / 2f;
            float r = i % 2 == 0 ? RO : RI;
            star[i] = new PointF(SX + r * MathF.Cos(a), SY + r * MathF.Sin(a));
        }
        g.FillPolygon(starFill, star);

        return bmp;
    }

    /// <summary>
    /// Gray open-end wrench (spanner): a diagonal handle with a solid jaw head whose
    /// "mouth" (open slot) is carved out toward the upper-right — develop branch.
    /// </summary>
    private static Bitmap Wrench()
    {
        var bmp = Blank(); using var g = AA(bmp);

        Color gray = Color.FromArgb(0x8A, 0x8A, 0x8A);   // tool gray
        using var body  = new SolidBrush(gray);
        using var shaft = new Pen(gray, 3.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        // Handle: diagonal from lower-left up to the head
        g.DrawLine(shaft, 4.5f, 13f, 11f, 5.5f);

        // Jaw head: a solid disc at the upper end (center ≈ (11.5, 5), r ≈ 4)
        g.FillEllipse(body, 7.5f, 1f, 8f, 8f);

        // Carve the open "mouth": erase a slanted slot from the head center out
        // past the upper-right rim, leaving two prongs — the open-end jaw.
        var prevMode = g.CompositingMode;
        g.CompositingMode = CompositingMode.SourceCopy;
        using var erase = new SolidBrush(Color.Transparent);
        PointF[] mouth =
        [
            new(10.30f, 3.80f),   // inner-left  (near center)
            new(12.70f, 6.20f),   // inner-right (near center)
            new(16.24f, 2.66f),   // outer-right (beyond rim)
            new(13.84f, 0.26f),   // outer-left  (beyond rim)
        ];
        g.FillPolygon(erase, mouth);
        g.CompositingMode = prevMode;

        return bmp;
    }

    /// <summary>
    /// Single large leaf: bezier teardrop body + central vein + two side veins.
    /// </summary>
    private static Bitmap Leaf()
    {
        var bmp = Blank(); using var g = AA(bmp);
        using var b    = new SolidBrush(Color.FromArgb(0x2E, 0xA0, 0x43));  // rich green
        using var vein = new Pen(Color.FromArgb(0x14, 0x60, 0x1A), 1f);

        // Teardrop leaf via bezier: pointed tip at top, rounded base
        using var path = new GraphicsPath();
        path.AddBezier(8, 1,  14, 4,  14, 12, 8, 15);   // right half
        path.AddBezier(8, 15,  2, 12,  2,  4, 8,  1);   // left half
        g.FillPath(b, path);

        // Veins
        g.DrawLine(vein, 8f, 2f, 8f, 14f);               // central vein
        g.DrawLine(vein, 8f, 7f, 4f, 11f);               // left side-vein
        g.DrawLine(vein, 8f, 7f, 12f, 11f);              // right side-vein
        return bmp;
    }

    /// <summary>Red body, black head, black center line and four spots — bugfix.</summary>
    private static Bitmap Ladybug()
    {
        var bmp = Blank(); using var g = AA(bmp);
        using var head = new SolidBrush(Color.Black);
        using var body = new SolidBrush(Color.FromArgb(0xDD, 0x20, 0x20));
        using var line = new Pen(Color.Black, 1f);
        using var spot = new SolidBrush(Color.Black);
        g.FillEllipse(head, 4,  1, 8, 6);    // head
        g.FillEllipse(body, 3,  5, 10, 10);  // body
        g.DrawLine(line, 8,  5, 8, 15);       // center line
        g.FillEllipse(spot,  4,  7, 2, 2);   // spot TL
        g.FillEllipse(spot, 10,  7, 2, 2);   // spot TR
        g.FillEllipse(spot,  4, 11, 2, 2);   // spot BL
        g.FillEllipse(spot, 10, 11, 2, 2);   // spot BR
        return bmp;
    }

    /// <summary>Brown box with a white cross-ribbon — release.</summary>
    private static Bitmap Package()
    {
        var bmp = Blank(); using var g = AA(bmp);
        using var light = new SolidBrush(Color.FromArgb(0xD0, 0x95, 0x30));  // tan
        using var dark  = new SolidBrush(Color.FromArgb(0xA0, 0x65, 0x00));  // brown lid
        using var white = new SolidBrush(Color.White);
        g.FillRectangle(light, 2,  4, 12, 11);  // body
        g.FillRectangle(dark,  2,  4, 12,  3);  // lid (darker top)
        g.FillRectangle(white, 2,  8, 12,  2);  // horizontal ribbon
        g.FillRectangle(white, 7,  4,  2, 11);  // vertical ribbon
        return bmp;
    }

    /// <summary>
    /// Red fire extinguisher: gray handle, red cylinder with white gauge dot,
    /// dark hose and nozzle — hotfix branch.
    /// </summary>
    private static Bitmap WarningSign()
    {
        var bmp = Blank(); using var g = AA(bmp);
        using var red    = new SolidBrush(Color.FromArgb(0xCC, 0x22, 0x22));
        using var dkRed  = new SolidBrush(Color.FromArgb(0x88, 0x11, 0x11));
        using var silver = new SolidBrush(Color.FromArgb(0xC0, 0xC0, 0xC0));
        using var white  = new SolidBrush(Color.White);
        using var dark   = new SolidBrush(Color.FromArgb(0x35, 0x35, 0x35));
        using var hose   = new Pen(Color.FromArgb(0x35, 0x35, 0x35), 1.3f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };

        // Handle (top)
        g.FillRectangle(silver, 5, 0, 5, 2);   // horizontal grip bar
        g.FillRectangle(silver, 8, 0, 2, 4);   // vertical post

        // Cylinder: top cap + body + bottom cap
        g.FillEllipse(red,   3,  4, 7, 4);     // top dome
        g.FillRectangle(red, 3,  6, 7, 8);     // body
        g.FillEllipse(dkRed, 3, 12, 7, 3);     // bottom cap

        // Pressure gauge (white circle on body)
        g.FillEllipse(white, 5, 7, 3, 3);

        // Hose + nozzle (right side)
        g.DrawLine(hose, 10f, 8f, 13f, 11f);   // hose
        g.FillRectangle(dark, 12, 10, 4, 2);   // nozzle

        return bmp;
    }

    /// <summary>
    /// First-aid kit bag: light-gray rectangular body with a dark border, an arch
    /// handle at the top, and a bold red medical cross in the center — support branch.
    /// </summary>
    private static Bitmap Gear()
    {
        var bmp = Blank(); using var g = AA(bmp);
        using var bg     = new SolidBrush(Color.FromArgb(0xF2, 0xF2, 0xF2));  // light gray bag
        using var border = new Pen(Color.FromArgb(0x30, 0x30, 0x30), 1f);
        using var handle = new Pen(Color.FromArgb(0x30, 0x30, 0x30), 1.5f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var cross  = new Pen(Color.FromArgb(0xCC, 0x00, 0x00), 2.5f)
            { StartCap = LineCap.Square, EndCap = LineCap.Square };

        // Arch handle at top
        g.DrawArc(handle, 6, 1, 4, 4, 180, 180);

        // Bag body
        g.FillRectangle(bg,     2,  5, 12, 10);
        g.DrawRectangle(border, 2,  5, 12, 10);

        // Red cross centred in the bag body (center x=8, center y=10)
        g.DrawLine(cross, 4f, 10f, 12f, 10f);   // horizontal bar
        g.DrawLine(cross, 8f,  7f,  8f, 13f);   // vertical bar

        return bmp;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Bitmap Blank()
        => new(16, 16, PixelFormat.Format32bppArgb);

    private static Graphics AA(Bitmap bmp)
    {
        var g = Graphics.FromImage(bmp);
        g.SmoothingMode   = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        return g;
    }

    private static Pen Round(Color c, float w)
        => new(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
}
