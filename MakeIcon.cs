using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class IconGen
{
    static readonly Color ColorTop = Color.FromArgb(74, 158, 255);
    static readonly Color ColorBottom = Color.FromArgb(29, 95, 208);

    static void Main()
    {
        int[] sizes = { 256, 48, 32, 16 };
        List<byte[]> pngs = new List<byte[]>();

        pngs.Add(RenderPng(256));
        File.WriteAllBytes("icon-preview.png", pngs[0]);
        for (int i = 1; i < sizes.Length; i++)
        {
            pngs.Add(RenderPng(sizes[i]));
        }

        using (FileStream fs = new FileStream("Open-UnitLogin.ico", FileMode.Create))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((ushort)0);   // reserved
            w.Write((ushort)1);   // 1 = icon
            w.Write((ushort)sizes.Length);

            uint offset = (uint)(6 + 16 * sizes.Length);
            for (int i = 0; i < sizes.Length; i++)
            {
                byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                w.Write(dim);        // width
                w.Write(dim);        // height
                w.Write((byte)0);    // palette colors
                w.Write((byte)0);    // reserved
                w.Write((ushort)1);  // color planes
                w.Write((ushort)32); // bits per pixel
                w.Write((uint)pngs[i].Length);
                w.Write(offset);
                offset += (uint)pngs[i].Length;
            }
            foreach (byte[] png in pngs) w.Write(png);
        }
        Console.WriteLine("Icon written: Open-UnitLogin.ico");
    }

    static byte[] RenderPng(int size)
    {
        using (Bitmap bmp = new Bitmap(size, size))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float k = size / 256f;

                // Rounded-square gradient background.
                using (GraphicsPath round = RoundedRect(new RectangleF(0, 0, size, size), 56f * k))
                using (LinearGradientBrush grad = new LinearGradientBrush(
                    new PointF(0, 0), new PointF(size, size), ColorTop, ColorBottom))
                {
                    g.FillPath(grad, round);
                }

                // White key rotated 45 degrees counter-clockwise, drawn in a
                // 256x256 coordinate space centered on the canvas.
                g.TranslateTransform(size / 2f, size / 2f);
                g.RotateTransform(-45f);
                g.ScaleTransform(k, k);
                g.TranslateTransform(-120f, -125f);

                using (SolidBrush white = new SolidBrush(Color.White))
                {
                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddEllipse(28f, 78f, 100f, 100f);          // bow (outer circle)
                        g.FillPath(white, p);
                    }
                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddRectangle(new RectangleF(110f, 114f, 102f, 28f)); // shaft
                        p.AddRectangle(new RectangleF(172f, 142f, 18f, 30f));  // tooth 1
                        p.AddRectangle(new RectangleF(196f, 142f, 16f, 20f));  // tooth 2
                        g.FillPath(white, p);
                    }
                }

                // Punch the bow hole by repainting the same background gradient
                // at the hole's rotated position, so it matches the backdrop exactly.
                using (GraphicsPath hole = new GraphicsPath())
                {
                    hole.AddEllipse(58f, 108f, 40f, 40f);
                    hole.Transform(g.Transform);
                    g.ResetTransform();
                    using (LinearGradientBrush grad = new LinearGradientBrush(
                        new PointF(0, 0), new PointF(size, size), ColorTop, ColorBottom))
                    {
                        g.FillPath(grad, hole);
                    }
                }
            }

            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }

    static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        GraphicsPath p = new GraphicsPath();
        float d = radius * 2f;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
