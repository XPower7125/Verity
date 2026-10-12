using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Verity.Shared
{
    /// <summary>
    /// Loads the Verity artwork (verity.ico, verity.png) from embedded resources
    /// so both builds share one copy and nothing depends on files next to the exe.
    /// </summary>
    public static class Brand
    {
        private static Icon? _icon;
        private static Image? _logo;

        public static Icon Icon
        {
            get
            {
                if (_icon == null)
                {
                    var res = Assembly.GetExecutingAssembly().GetManifestResourceStream("Verity.ico");
                    _icon = res != null ? new Icon(res) : SystemIcons.Application;
                }
                return _icon;
            }
        }

        public static Image Logo
        {
            get
            {
                if (_logo == null)
                {
                    var res = Assembly.GetExecutingAssembly().GetManifestResourceStream("Verity.png");
                    if (res != null)
                    {
                        // Clone into a Bitmap so the stream can close without
                        // invalidating the image the forms hold on to.
                        using var ms = new MemoryStream();
                        res.CopyTo(ms);
                        ms.Position = 0;
                        using var temp = Image.FromStream(ms);
                        _logo = new Bitmap(temp);
                    }
                    else
                    {
                        _logo = new Bitmap(1, 1);
                    }
                }
                return _logo;
            }
        }

        /// <summary>Applies the brand icon to a form. Call after the constructor sets Text.</summary>
        public static T Apply<T>(this T form) where T : Form
        {
            form.Icon = Icon;
            return form;
        }
    /// <summary>
        /// Circular version of the logo for the transparent pet window.
        /// The source PNG has opaque white corners, which would show as white
        /// wedges against any desktop, so anything outside the disc is dropped.
        /// </summary>
        public static Bitmap RoundFace(int size)
        {
            var src = Logo;
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            var diameter = (float)size * 0.94f;
            var offset = (size - diameter) / 2f;
            using var clip = new System.Drawing.Drawing2D.GraphicsPath();
            clip.AddEllipse(offset, offset, diameter, diameter);
            g.SetClip(clip);
            g.DrawImage(src, new RectangleF(0, 0, size, size));
            g.ResetClip();

            // Knock the corners back to the transparency key colour.
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var dx = x - (size / 2f);
                    var dy = y - (size / 2f);
                    if (dx * dx + dy * dy > (diameter / 2f) * (diameter / 2f))
                        bmp.SetPixel(x, y, Color.Magenta);
                }
            }
            return bmp;
        }
    }
}