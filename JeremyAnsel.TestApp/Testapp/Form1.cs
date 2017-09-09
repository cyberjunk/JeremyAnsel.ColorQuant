using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using JeremyAnsel.ColorQuant;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Testapp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private unsafe void button1_Click(object sender, EventArgs e)
        {
            WuAlphaColorQuantizer quant = new WuAlphaColorQuantizer();

            Bitmap img = (Bitmap)Image.FromFile("f:/temp/avsham-0.PNG");
            Bitmap bmp = quant.Quantize(img, 256);

/*            Bitmap img = (Bitmap)Image.FromFile("f:/temp/scannen.PNG");
            Bitmap bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format8bppIndexed);

            BitmapData data = img.LockBits(
               Rectangle.FromLTRB(0, 0, img.Width, img.Height),
               ImageLockMode.ReadOnly, img.PixelFormat);

            BitmapData bmpdata = bmp.LockBits(
               Rectangle.FromLTRB(0, 0, bmp.Width, bmp.Height),
               ImageLockMode.WriteOnly, bmp.PixelFormat);

            uint[] res = quant.Quantize(
               (uint*)data.Scan0.ToPointer(),
               256, img.Width, img.Height,
               (byte*)bmpdata.Scan0.ToPointer());

            ColorPalette pal = bmp.Palette;
            for (int i = 0; i < 256; i++)
                pal.Entries[i] = Color.FromArgb((int)res[i]);
            bmp.Palette = pal;

            img.UnlockBits(data);
            bmp.UnlockBits(bmpdata);*/

            bmp.Save("f:/temp/avsham-0_quant.png");

            img.Dispose();
            bmp.Dispose();
        }
    }
}
