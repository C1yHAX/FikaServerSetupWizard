using System.Drawing;

namespace FikaServerSetupWizard;

static class Theme
{
    public static Color Bg0      = Color.FromArgb(5,   5,   5);
    public static Color Bg1      = Color.FromArgb(10,  9,   8);
    public static Color Bg2      = Color.FromArgb(16,  15,  13);
    public static Color Bg3      = Color.FromArgb(24,  22,  18);
    public static Color BgActive = Color.FromArgb(20,  17,  10);
    public static Color Line     = Color.FromArgb(34,  30,  24);
    public static Color LineHL   = Color.FromArgb(78,  64,  38);
    public static Color Gold     = Color.FromArgb(196, 165, 105);
    public static Color GoldD    = Color.FromArgb(116, 95,  50);
    public static Color GoldL    = Color.FromArgb(224, 196, 146);
    public static Color GoldBg   = Color.FromArgb(18,  14,  5);
    public static Color Green    = Color.FromArgb(62,  102, 66);
    public static Color GreenL   = Color.FromArgb(96,  152, 102);
    public static Color GreenBg  = Color.FromArgb(6,   14,  8);
    public static Color Red      = Color.FromArgb(122, 44,  44);
    public static Color RedL     = Color.FromArgb(188, 80,  80);
    public static Color RedBg    = Color.FromArgb(18,  4,   4);
    public static Color Amber    = Color.FromArgb(184, 144, 44);
    public static Color AmberL   = Color.FromArgb(214, 176, 88);
    public static Color AmberBg  = Color.FromArgb(20,  14,  3);
    public static Color Tx0      = Color.FromArgb(188, 184, 176);
    public static Color Tx1      = Color.FromArgb(108, 104, 98);
    public static Color Tx2      = Color.FromArgb(50,  48,  44);

    public static Font H1  = new("Segoe UI", 13f, FontStyle.Bold);
    public static Font H2  = new("Segoe UI", 10f, FontStyle.Bold);
    public static Font H3  = new("Segoe UI", 9f,  FontStyle.Bold);
    public static Font Bd  = new("Segoe UI", 9f);
    public static Font Sm  = new("Segoe UI", 8f);
    public static Font Nav = new("Segoe UI", 8f,  FontStyle.Bold);
    public static Font Cap = new("Segoe UI", 7f,  FontStyle.Bold);
    public static Font Mn  = new("Consolas", 9f);
    public static Font Mn2 = new("Consolas", 8f);
    public static Font Hdr = new("Segoe UI", 7f);
}