﻿// <copyright file="WuAlphaColorQuantizer.cs" company="Jérémy Ansel">
// Copyright (c) 2014-2015 Jérémy Ansel
// </copyright>
// <license>
// Licensed under the MIT license. See LICENSE.txt
// </license>

namespace JeremyAnsel.ColorQuant
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A Wu's color quantizer with alpha channel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Based on C Implementation of Xiaolin Wu's Color Quantizer (v. 2)
    /// (see Graphics Gems volume II, pages 126-133)
    /// (<see href="http://www.ece.mcmaster.ca/~xwu/cq.c"/>).
    /// </para>
    /// <para>
    /// Algorithm: Greedy orthogonal bipartition of RGB space for variance
    /// minimization aided by inclusion-exclusion tricks.
    /// For speed no nearest neighbor search is done. Slightly
    /// better performance can be expected by more sophisticated
    /// but more expensive versions.
    /// </para>
    /// </remarks>
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Wu", Justification = "Reviewed")]
    public sealed class WuAlphaColorQuantizer : IColorQuantizer
    {
        /// <summary>
        /// Maximum supported colors
        /// </summary>
        private const int MaxColors = 256;

        /// <summary>
        /// The index bits.
        /// </summary>
        private const int IndexBits = 6;

        /// <summary>
        /// The index alpha bits.
        /// </summary>
        private const int IndexAlphaBits = 3;

        /// <summary>
        /// The index count.
        /// </summary>
        private const int IndexCount = (1 << WuAlphaColorQuantizer.IndexBits) + 1;

        /// <summary>
        /// The index alpha count.
        /// </summary>
        private const int IndexAlphaCount = (1 << WuAlphaColorQuantizer.IndexAlphaBits) + 1;

        /// <summary>
        /// IndexCount * IndexAlphaCount
        /// </summary>
        private const int WorkArraySize = WuAlphaColorQuantizer.IndexCount * WuAlphaColorQuantizer.IndexAlphaCount;

        /// <summary>
        /// The table length.
        /// </summary>
        private const int TableLength = WuAlphaColorQuantizer.IndexCount * WuAlphaColorQuantizer.IndexCount * WuAlphaColorQuantizer.IndexCount * WuAlphaColorQuantizer.IndexAlphaCount;

        /// <summary>
        /// Moment of <c>P(c)</c>.
        /// </summary>
        private readonly long[] vwt = new long[WuAlphaColorQuantizer.TableLength];

        /// <summary>
        /// Moment of <c>r*P(c)</c>.
        /// </summary>
        private readonly long[] vmr = new long[WuAlphaColorQuantizer.TableLength];

        /// <summary>
        /// Moment of <c>g*P(c)</c>.
        /// </summary>
        private readonly long[] vmg = new long[WuAlphaColorQuantizer.TableLength];

        /// <summary>
        /// Moment of <c>b*P(c)</c>.
        /// </summary>
        private readonly long[] vmb = new long[WuAlphaColorQuantizer.TableLength];

        /// <summary>
        /// Moment of <c>a*P(c)</c>.
        /// </summary>
        private readonly long[] vma = new long[WuAlphaColorQuantizer.TableLength];

        /// <summary>
        /// Moment of <c>c^2*P(c)</c>.
        /// </summary>
        private readonly double[] m2 = new double[WuAlphaColorQuantizer.TableLength];

        /// <summary>
        /// Color space tag.
        /// </summary>
        private readonly byte[] tag = new byte[WuAlphaColorQuantizer.TableLength];

        #region Temporary Arrays for GetMoments3D
        /// <summary>
        /// Temporary Array Data used in Get3DMoments()
        /// </summary>
        private readonly long[] volume = new long[WuAlphaColorQuantizer.WorkArraySize];

        /// <summary>
        /// Temporary Array Data used in Get3DMoments()
        /// </summary>
        private readonly long[] volumeR = new long[WuAlphaColorQuantizer.WorkArraySize];

        /// <summary>
        /// Temporary Array Data used in Get3DMoments()
        /// </summary>
        private readonly long[] volumeG = new long[WuAlphaColorQuantizer.WorkArraySize];

        /// <summary>
        /// Temporary Array Data used in Get3DMoments()
        /// </summary>
        private readonly long[] volumeB = new long[WuAlphaColorQuantizer.WorkArraySize];

        /// <summary>
        /// Temporary Array Data used in Get3DMoments()
        /// </summary>
        private readonly long[] volumeA = new long[WuAlphaColorQuantizer.WorkArraySize];

        /// <summary>
        /// Temporary Array Data used in Get3DMoments()
        /// </summary>
        private readonly double[] volume2 = new double[WuAlphaColorQuantizer.WorkArraySize];

        /// <summary>
        /// Temporary Array Data used in Get3DMoments()
        /// </summary>
        private readonly long[] area = new long[WuAlphaColorQuantizer.IndexAlphaCount];

        /// <summary>
        /// Temporary Array Data used in Get3DMoments()
        /// </summary>
        private readonly long[] areaR = new long[WuAlphaColorQuantizer.IndexAlphaCount];

        /// <summary>
        /// Temporary Array Data used in Get3DMoments()
        /// </summary>
        private readonly long[] areaG = new long[WuAlphaColorQuantizer.IndexAlphaCount];

        /// <summary>
        /// Temporary Array Data used in Get3DMoments()
        /// </summary>
        private readonly long[] areaB = new long[WuAlphaColorQuantizer.IndexAlphaCount];

        /// <summary>
        /// Temporary Array Data used in Get3DMoments()
        /// </summary>
        private readonly long[] areaA = new long[WuAlphaColorQuantizer.IndexAlphaCount];

        /// <summary>
        /// Temporary Array Data used in Get3DMoments()
        /// </summary>
        private readonly double[] area2 = new double[WuAlphaColorQuantizer.IndexAlphaCount];
        #endregion

        /// <summary>
        /// Quantizes an image.
        /// </summary>
        /// <param name="image">The image (ARGB).</param>
        /// <returns>The result.</returns>
        public ColorQuantizerResult Quantize(byte[] image)
        {
            return this.Quantize(image, WuAlphaColorQuantizer.MaxColors);
        }

        /// <summary>
        /// Quantizes an image.
        /// </summary>
        /// <param name="image">The image (ARGB).</param>
        /// <param name="colorCount">The color count.</param>
        /// <returns>The result.</returns>
        public ColorQuantizerResult Quantize(byte[] image, int colorCount)
        {
            if (image == null)
            {
                throw new ArgumentNullException("image");
            }

            if (colorCount < 1 || colorCount > WuAlphaColorQuantizer.MaxColors)
            {
                throw new ArgumentOutOfRangeException("colorCount");
            }

            this.Clear();

            this.Build3DHistogram(image);
            this.Get3DMoments();

            Box[] cube;
            this.BuildCube(out cube, ref colorCount);

            return this.GenerateResult(image, colorCount, cube);
        }

        /// <summary>
        /// Quantizes an image.
        /// </summary>
        /// <param name="image">The image (ARGB).</param>
        /// <param name="colorCount">The color count.</param>
        /// <param name="width">Width of image</param>
        /// <param name="height">Height of image</param>
        /// <param name="destPixels">Indexed pixelData will be written there</param>
        /// <param name="padMultiple4">True to pad rows to multiple of 4</param>
        /// <returns>Palette with ARGB colors</returns>
        [CLSCompliantAttribute(false)]
        public unsafe uint[] Quantize(uint* image, int colorCount, int width, int height, byte* destPixels, bool padMultiple4)
        {
            if (image == null)
            {
                throw new ArgumentNullException("image");
            }

            if (destPixels == null)
            {
                throw new ArgumentNullException("destPixels");
            }

            if (colorCount < 1 || colorCount > WuAlphaColorQuantizer.MaxColors)
            {
                throw new ArgumentOutOfRangeException("colorCount");
            }

            this.Clear();

            this.Build3DHistogram(image, width, height);
            this.Get3DMoments();

            Box[] cube;
            this.BuildCube(out cube, ref colorCount);

            return this.GenerateResult(image, colorCount, cube, width, height, destPixels, padMultiple4);
        }

        /// <summary>
        /// Quantizes an image.
        /// </summary>
        /// <param name="image">The image (ARGB).</param>
        /// <param name="colorCount">The color count.</param>
        /// <returns>Bitmap with indexed colors</returns>
        [CLSCompliantAttribute(false)]
        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Reviewed")]
        public unsafe Bitmap Quantize(Bitmap image, int colorCount)
        {
            if (image == null)
            {
                throw new ArgumentNullException("image");
            }
            
            if (colorCount < 1 || colorCount > WuAlphaColorQuantizer.MaxColors)
            {
                throw new ArgumentOutOfRangeException("colorCount");
            }

            Bitmap bmp = new Bitmap(image.Width, image.Height, PixelFormat.Format8bppIndexed);

            BitmapData imgdata = image.LockBits(
                Rectangle.FromLTRB(0, 0, image.Width, image.Height), 
                ImageLockMode.ReadOnly, 
                image.PixelFormat);

            BitmapData bmpdata = bmp.LockBits(
                Rectangle.FromLTRB(0, 0, bmp.Width, bmp.Height), 
                ImageLockMode.WriteOnly, 
                bmp.PixelFormat);

            uint[] res = this.Quantize(
               (uint*)imgdata.Scan0.ToPointer(),
               colorCount, 
               image.Width, 
               image.Height,
               (byte*)bmpdata.Scan0.ToPointer(),
               true);

            ColorPalette pal = bmp.Palette;
            for (int i = 0; i < res.Length; i++)
            {
                pal.Entries[i] = Color.FromArgb((int)res[i]);
            }

            for (int i = res.Length; i < 256; i++)
            {
                pal.Entries[i] = Color.FromArgb(0);
            }

            bmp.Palette = pal;

            image.UnlockBits(imgdata);
            bmp.UnlockBits(bmpdata);

            return bmp;
        }

        /// <summary>
        /// Quantizes an image.
        /// </summary>
        /// <param name="image">The image (ARGB).</param>
        /// <param name="colorCount">The color count.</param>
        /// <param name="destPixels">Indexed pixelData will be written there</param>
        /// <param name="padMultiple4">True to pad rows to multiple of 4</param>
        /// <returns>Bitmap with indexed colors</returns>
        [CLSCompliantAttribute(false)]
        public unsafe uint[] Quantize(Bitmap image, int colorCount, byte[] destPixels, bool padMultiple4)
        {
            if (image == null)
            {
                throw new ArgumentNullException("image");
            }

            if (destPixels == null)
            {
                throw new ArgumentNullException("destPixels");
            }

            if (colorCount < 1 || colorCount > 256)
            {
                throw new ArgumentOutOfRangeException("colorCount");
            }
            
            BitmapData imgdata = image.LockBits(
                Rectangle.FromLTRB(0, 0, image.Width, image.Height),
                ImageLockMode.ReadOnly,
                image.PixelFormat);

            uint[] res;
            fixed (byte* p = destPixels)
            {
                res = this.Quantize(
                   (uint*)imgdata.Scan0.ToPointer(),
                   colorCount,
                   image.Width,
                   image.Height,
                   p,
                   padMultiple4);
            }
            
            image.UnlockBits(imgdata);

            return res;
        }

        /// <summary>
        /// Gets an index.
        /// </summary>
        /// <param name="r">The red value.</param>
        /// <param name="g">The green value.</param>
        /// <param name="b">The blue value.</param>
        /// <param name="a">The alpha value.</param>
        /// <returns>The index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndex(int r, int g, int b, int a)
        {
            return (r << ((WuAlphaColorQuantizer.IndexBits * 2) + WuAlphaColorQuantizer.IndexAlphaBits))
                + (r << (WuAlphaColorQuantizer.IndexBits + WuAlphaColorQuantizer.IndexAlphaBits + 1))
                + (g << (WuAlphaColorQuantizer.IndexBits + WuAlphaColorQuantizer.IndexAlphaBits))
                + (r << (WuAlphaColorQuantizer.IndexBits * 2))
                + (r << (WuAlphaColorQuantizer.IndexBits + 1))
                + (g << WuAlphaColorQuantizer.IndexBits)
                + ((r + g + b) << WuAlphaColorQuantizer.IndexAlphaBits)
                + r + g + b + a;
        }

        /// <summary>
        /// Computes sum over a box of any given statistic.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="moment">The moment.</param>
        /// <returns>The result.</returns>
        private static double Volume(Box cube, long[] moment)
        {
            return moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B1, cube.A1)]
                - moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B1, cube.A0)]
                - moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B0, cube.A1)]
                + moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B0, cube.A0)]
                - moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B1, cube.A1)]
                + moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B1, cube.A0)]
                + moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B0, cube.A1)]
                - moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B1, cube.A1)]
                + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B1, cube.A0)]
                + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B0, cube.A1)]
                - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B1, cube.A1)]
                - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B0, cube.A0)];
        }

        /// <summary>
        /// Computes part of Volume(cube, moment) that doesn't depend on r1, g1, or b1 (depending on direction).
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="moment">The moment.</param>
        /// <returns>The result.</returns>
        private static long Bottom(Box cube, int direction, long[] moment)
        {
            switch (direction)
            {
                // Red
                case 3:
                    return -moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B1, cube.A1)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B1, cube.A0)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B0, cube.A1)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B1, cube.A1)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

                // Green
                case 2:
                    return -moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B1, cube.A1)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B1, cube.A0)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B0, cube.A1)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B1, cube.A1)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

                // Blue
                case 1:
                    return -moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B0, cube.A1)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B0, cube.A0)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B0, cube.A1)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B0, cube.A1)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

                // Alpha
                case 0:
                    return -moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B1, cube.A0)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B0, cube.A0)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B1, cube.A0)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B1, cube.A0)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

                default:
                    throw new ArgumentOutOfRangeException("direction");
            }
        }

        /// <summary>
        /// Computes remainder of Volume(cube, moment), substituting position for r1, g1, or b1 (depending on direction).
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="position">The position.</param>
        /// <param name="moment">The moment.</param>
        /// <returns>The result.</returns>
        private static long Top(Box cube, int direction, int position, long[] moment)
        {
            switch (direction)
            {
                // Red
                case 3:
                    return moment[WuAlphaColorQuantizer.GetIndex(position, cube.G1, cube.B1, cube.A1)]
                        - moment[WuAlphaColorQuantizer.GetIndex(position, cube.G1, cube.B1, cube.A0)]
                        - moment[WuAlphaColorQuantizer.GetIndex(position, cube.G1, cube.B0, cube.A1)]
                        + moment[WuAlphaColorQuantizer.GetIndex(position, cube.G1, cube.B0, cube.A0)]
                        - moment[WuAlphaColorQuantizer.GetIndex(position, cube.G0, cube.B1, cube.A1)]
                        + moment[WuAlphaColorQuantizer.GetIndex(position, cube.G0, cube.B1, cube.A0)]
                        + moment[WuAlphaColorQuantizer.GetIndex(position, cube.G0, cube.B0, cube.A1)]
                        - moment[WuAlphaColorQuantizer.GetIndex(position, cube.G0, cube.B0, cube.A0)];

                // Green
                case 2:
                    return moment[WuAlphaColorQuantizer.GetIndex(cube.R1, position, cube.B1, cube.A1)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R1, position, cube.B1, cube.A0)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R1, position, cube.B0, cube.A1)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R1, position, cube.B0, cube.A0)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, position, cube.B1, cube.A1)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, position, cube.B1, cube.A0)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, position, cube.B0, cube.A1)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, position, cube.B0, cube.A0)];

                // Blue
                case 1:
                    return moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, position, cube.A1)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, position, cube.A0)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, position, cube.A1)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, position, cube.A0)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, position, cube.A1)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, position, cube.A0)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, position, cube.A1)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, position, cube.A0)];

                // Alpha
                case 0:
                    return moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B1, position)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B0, position)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B1, position)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B0, position)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B1, position)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B0, position)]
                        + moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B1, position)]
                        - moment[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B0, position)];

                default:
                    throw new ArgumentOutOfRangeException("direction");
            }
        }

        /// <summary>
        /// Clears the tables.
        /// </summary>
        private void Clear()
        {
            Array.Clear(this.vwt, 0, WuAlphaColorQuantizer.TableLength);
            Array.Clear(this.vmr, 0, WuAlphaColorQuantizer.TableLength);
            Array.Clear(this.vmg, 0, WuAlphaColorQuantizer.TableLength);
            Array.Clear(this.vmb, 0, WuAlphaColorQuantizer.TableLength);
            Array.Clear(this.vma, 0, WuAlphaColorQuantizer.TableLength);
            Array.Clear(this.m2, 0, WuAlphaColorQuantizer.TableLength);

            Array.Clear(this.tag, 0, WuAlphaColorQuantizer.TableLength);
        }

        /// <summary>
        /// Builds a 3-D color histogram of <c>counts, r/g/b, c^2</c>.
        /// </summary>
        /// <param name="image">The image.</param>
        private void Build3DHistogram(byte[] image)
        {
            for (int i = 0; i < image.Length; i += 4)
            {
                int a = image[i + 3];
                int r = image[i + 2];
                int g = image[i + 1];
                int b = image[i];

                int inr = r >> (8 - WuAlphaColorQuantizer.IndexBits);
                int ing = g >> (8 - WuAlphaColorQuantizer.IndexBits);
                int inb = b >> (8 - WuAlphaColorQuantizer.IndexBits);
                int ina = a >> (8 - WuAlphaColorQuantizer.IndexAlphaBits);

                int ind = WuAlphaColorQuantizer.GetIndex(inr + 1, ing + 1, inb + 1, ina + 1);

                this.vwt[ind]++;
                this.vmr[ind] += r;
                this.vmg[ind] += g;
                this.vmb[ind] += b;
                this.vma[ind] += a;
                this.m2[ind] += (r * r) + (g * g) + (b * b) + (a * a);
            }
        }

        /// <summary>
        /// Builds a 3-D color histogram of <c>counts, r/g/b, c^2</c>.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="width">Width of image</param>
        /// <param name="height">Height of image</param>
        private unsafe void Build3DHistogram(uint* image, int width, int height)
        {
            int pixels = width * height;

            for (int i = 0; i < pixels; i++)
            {
                uint pix = image[i];
                uint a = (pix & 0xFF000000) >> 24;
                uint r = (pix & 0x00FF0000) >> 16;
                uint g = (pix & 0x0000FF00) >> 8;
                uint b = pix & 0x000000FF;

                uint inr = r >> (8 - WuAlphaColorQuantizer.IndexBits);
                uint ing = g >> (8 - WuAlphaColorQuantizer.IndexBits);
                uint inb = b >> (8 - WuAlphaColorQuantizer.IndexBits);
                uint ina = a >> (8 - WuAlphaColorQuantizer.IndexAlphaBits);

                int ind = WuAlphaColorQuantizer.GetIndex((int)inr + 1, (int)ing + 1, (int)inb + 1, (int)ina + 1);

                this.vwt[ind]++;
                this.vmr[ind] += r;
                this.vmg[ind] += g;
                this.vmb[ind] += b;
                this.vma[ind] += a;
                this.m2[ind] += (r * r) + (g * g) + (b * b) + (a * a);
            }
        }

        /// <summary>
        /// Converts the histogram into moments so that we can rapidly calculate
        /// the sums of the above quantities over any desired box.
        /// </summary>
        private void Get3DMoments()
        {
            for (int r = 1; r < WuAlphaColorQuantizer.IndexCount; r++)
            {
                Array.Clear(this.volume, 0, WuAlphaColorQuantizer.IndexCount * WuAlphaColorQuantizer.IndexAlphaCount);
                Array.Clear(this.volumeR, 0, WuAlphaColorQuantizer.IndexCount * WuAlphaColorQuantizer.IndexAlphaCount);
                Array.Clear(this.volumeG, 0, WuAlphaColorQuantizer.IndexCount * WuAlphaColorQuantizer.IndexAlphaCount);
                Array.Clear(this.volumeB, 0, WuAlphaColorQuantizer.IndexCount * WuAlphaColorQuantizer.IndexAlphaCount);
                Array.Clear(this.volumeA, 0, WuAlphaColorQuantizer.IndexCount * WuAlphaColorQuantizer.IndexAlphaCount);
                Array.Clear(this.volume2, 0, WuAlphaColorQuantizer.IndexCount * WuAlphaColorQuantizer.IndexAlphaCount);

                for (int g = 1; g < WuAlphaColorQuantizer.IndexCount; g++)
                {
                    Array.Clear(this.area, 0, WuAlphaColorQuantizer.IndexAlphaCount);
                    Array.Clear(this.areaR, 0, WuAlphaColorQuantizer.IndexAlphaCount);
                    Array.Clear(this.areaG, 0, WuAlphaColorQuantizer.IndexAlphaCount);
                    Array.Clear(this.areaB, 0, WuAlphaColorQuantizer.IndexAlphaCount);
                    Array.Clear(this.areaA, 0, WuAlphaColorQuantizer.IndexAlphaCount);
                    Array.Clear(this.area2, 0, WuAlphaColorQuantizer.IndexAlphaCount);

                    for (int b = 1; b < WuAlphaColorQuantizer.IndexCount; b++)
                    {
                        long line = 0;
                        long lineR = 0;
                        long lineG = 0;
                        long lineB = 0;
                        long lineA = 0;
                        double line2 = 0;

                        for (int a = 1; a < WuAlphaColorQuantizer.IndexAlphaCount; a++)
                        {
                            int ind1 = WuAlphaColorQuantizer.GetIndex(r, g, b, a);

                            line += this.vwt[ind1];
                            lineR += this.vmr[ind1];
                            lineG += this.vmg[ind1];
                            lineB += this.vmb[ind1];
                            lineA += this.vma[ind1];
                            line2 += this.m2[ind1];

                            this.area[a] += line;
                            this.areaR[a] += lineR;
                            this.areaG[a] += lineG;
                            this.areaB[a] += lineB;
                            this.areaA[a] += lineA;
                            this.area2[a] += line2;

                            int inv = (b * WuAlphaColorQuantizer.IndexAlphaCount) + a;

                            this.volume[inv] += this.area[a];
                            this.volumeR[inv] += this.areaR[a];
                            this.volumeG[inv] += this.areaG[a];
                            this.volumeB[inv] += this.areaB[a];
                            this.volumeA[inv] += this.areaA[a];
                            this.volume2[inv] += this.area2[a];

                            int ind2 = ind1 - WuAlphaColorQuantizer.GetIndex(1, 0, 0, 0);

                            this.vwt[ind1] = this.vwt[ind2] + this.volume[inv];
                            this.vmr[ind1] = this.vmr[ind2] + this.volumeR[inv];
                            this.vmg[ind1] = this.vmg[ind2] + this.volumeG[inv];
                            this.vmb[ind1] = this.vmb[ind2] + this.volumeB[inv];
                            this.vma[ind1] = this.vma[ind2] + this.volumeA[inv];
                            this.m2[ind1] = this.m2[ind2] + this.volume2[inv];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Computes the weighted variance of a box.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <returns>The result.</returns>
        private double Variance(Box cube)
        {
            double dr = WuAlphaColorQuantizer.Volume(cube, this.vmr);
            double dg = WuAlphaColorQuantizer.Volume(cube, this.vmg);
            double db = WuAlphaColorQuantizer.Volume(cube, this.vmb);
            double da = WuAlphaColorQuantizer.Volume(cube, this.vma);

            double xx =
                this.m2[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B1, cube.A1)]
                - this.m2[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B1, cube.A0)]
                - this.m2[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B0, cube.A1)]
                + this.m2[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G1, cube.B0, cube.A0)]
                - this.m2[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B1, cube.A1)]
                + this.m2[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B1, cube.A0)]
                + this.m2[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B0, cube.A1)]
                - this.m2[WuAlphaColorQuantizer.GetIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                - this.m2[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B1, cube.A1)]
                + this.m2[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B1, cube.A0)]
                + this.m2[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B0, cube.A1)]
                - this.m2[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                + this.m2[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B1, cube.A1)]
                - this.m2[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                - this.m2[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                + this.m2[WuAlphaColorQuantizer.GetIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

            return xx - (((dr * dr) + (dg * dg) + (db * db) + (da * da)) / WuAlphaColorQuantizer.Volume(cube, this.vwt));
        }

        /// <summary>
        /// We want to minimize the sum of the variances of two sub-boxes.
        /// The sum(c^2) terms can be ignored since their sum over both sub-boxes
        /// is the same (the sum for the whole box) no matter where we split.
        /// The remaining terms have a minus sign in the variance formula,
        /// so we drop the minus sign and maximize the sum of the two terms.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="first">The first position.</param>
        /// <param name="last">The last position.</param>
        /// <param name="cut">The cutting point.</param>
        /// <param name="wholeR">The whole red.</param>
        /// <param name="wholeG">The whole green.</param>
        /// <param name="wholeB">The whole blue.</param>
        /// <param name="wholeA">The whole alpha.</param>
        /// <param name="wholeW">The whole weight.</param>
        /// <returns>The result.</returns>
        private double Maximize(Box cube, int direction, int first, int last, out int cut, double wholeR, double wholeG, double wholeB, double wholeA, double wholeW)
        {
            long baseR = WuAlphaColorQuantizer.Bottom(cube, direction, this.vmr);
            long baseG = WuAlphaColorQuantizer.Bottom(cube, direction, this.vmg);
            long baseB = WuAlphaColorQuantizer.Bottom(cube, direction, this.vmb);
            long baseA = WuAlphaColorQuantizer.Bottom(cube, direction, this.vma);
            long baseW = WuAlphaColorQuantizer.Bottom(cube, direction, this.vwt);

            double max = 0.0;
            cut = -1;

            for (int i = first; i < last; i++)
            {
                double halfR = baseR + WuAlphaColorQuantizer.Top(cube, direction, i, this.vmr);
                double halfG = baseG + WuAlphaColorQuantizer.Top(cube, direction, i, this.vmg);
                double halfB = baseB + WuAlphaColorQuantizer.Top(cube, direction, i, this.vmb);
                double halfA = baseA + WuAlphaColorQuantizer.Top(cube, direction, i, this.vma);
                double halfW = baseW + WuAlphaColorQuantizer.Top(cube, direction, i, this.vwt);

                if (halfW == 0)
                {
                    continue;
                }

                double temp = ((halfR * halfR) + (halfG * halfG) + (halfB * halfB) + (halfA * halfA)) / halfW;

                halfR = wholeR - halfR;
                halfG = wholeG - halfG;
                halfB = wholeB - halfB;
                halfA = wholeA - halfA;
                halfW = wholeW - halfW;

                if (halfW == 0)
                {
                    continue;
                }

                temp += ((halfR * halfR) + (halfG * halfG) + (halfB * halfB) + (halfA * halfA)) / halfW;

                if (temp > max)
                {
                    max = temp;
                    cut = i;
                }
            }

            return max;
        }

        /// <summary>
        /// Cuts a box.
        /// </summary>
        /// <param name="set1">The first set.</param>
        /// <param name="set2">The second set.</param>
        /// <returns>Returns a value indicating whether the box has been split.</returns>
        private bool Cut(Box set1, Box set2)
        {
            double wholeR = WuAlphaColorQuantizer.Volume(set1, this.vmr);
            double wholeG = WuAlphaColorQuantizer.Volume(set1, this.vmg);
            double wholeB = WuAlphaColorQuantizer.Volume(set1, this.vmb);
            double wholeA = WuAlphaColorQuantizer.Volume(set1, this.vma);
            double wholeW = WuAlphaColorQuantizer.Volume(set1, this.vwt);

            int cutr;
            int cutg;
            int cutb;
            int cuta;

            double maxr = this.Maximize(set1, 3, set1.R0 + 1, set1.R1, out cutr, wholeR, wholeG, wholeB, wholeA, wholeW);
            double maxg = this.Maximize(set1, 2, set1.G0 + 1, set1.G1, out cutg, wholeR, wholeG, wholeB, wholeA, wholeW);
            double maxb = this.Maximize(set1, 1, set1.B0 + 1, set1.B1, out cutb, wholeR, wholeG, wholeB, wholeA, wholeW);
            double maxa = this.Maximize(set1, 0, set1.A0 + 1, set1.A1, out cuta, wholeR, wholeG, wholeB, wholeA, wholeW);

            int dir;

            if ((maxr >= maxg) && (maxr >= maxb) && (maxr >= maxa))
            {
                dir = 3;

                if (cutr < 0)
                {
                    return false;
                }
            }
            else if ((maxg >= maxr) && (maxg >= maxb) && (maxg >= maxa))
            {
                dir = 2;
            }
            else if ((maxb >= maxr) && (maxb >= maxg) && (maxb >= maxa))
            {
                dir = 1;
            }
            else
            {
                dir = 0;
            }

            set2.R1 = set1.R1;
            set2.G1 = set1.G1;
            set2.B1 = set1.B1;
            set2.A1 = set1.A1;

            switch (dir)
            {
                // Red
                case 3:
                    set2.R0 = set1.R1 = cutr;
                    set2.G0 = set1.G0;
                    set2.B0 = set1.B0;
                    set2.A0 = set1.A0;
                    break;

                // Green
                case 2:
                    set2.G0 = set1.G1 = cutg;
                    set2.R0 = set1.R0;
                    set2.B0 = set1.B0;
                    set2.A0 = set1.A0;
                    break;

                // Blue
                case 1:
                    set2.B0 = set1.B1 = cutb;
                    set2.R0 = set1.R0;
                    set2.G0 = set1.G0;
                    set2.A0 = set1.A0;
                    break;

                // Alpha
                case 0:
                    set2.A0 = set1.A1 = cuta;
                    set2.R0 = set1.R0;
                    set2.G0 = set1.G0;
                    set2.B0 = set1.B0;
                    break;
            }

            set1.Volume = (set1.R1 - set1.R0) * (set1.G1 - set1.G0) * (set1.B1 - set1.B0) * (set1.A1 - set1.A0);
            set2.Volume = (set2.R1 - set2.R0) * (set2.G1 - set2.G0) * (set2.B1 - set2.B0) * (set2.A1 - set2.A0);

            return true;
        }

        /// <summary>
        /// Marks a color space tag.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="label">A label.</param>
        private void Mark(Box cube, byte label)
        {
            for (int r = cube.R0 + 1; r <= cube.R1; r++)
            {
                for (int g = cube.G0 + 1; g <= cube.G1; g++)
                {
                    for (int b = cube.B0 + 1; b <= cube.B1; b++)
                    {
                        for (int a = cube.A0 + 1; a <= cube.A1; a++)
                        {
                            this.tag[WuAlphaColorQuantizer.GetIndex(r, g, b, a)] = label;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds the cube.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="colorCount">The color count.</param>
        private void BuildCube(out Box[] cube, ref int colorCount)
        {
            cube = new Box[colorCount];
            double[] vv = new double[colorCount];

            for (int i = 0; i < colorCount; i++)
            {
                cube[i] = new Box();
            }

            cube[0].R0 = cube[0].G0 = cube[0].B0 = cube[0].A0 = 0;
            cube[0].R1 = cube[0].G1 = cube[0].B1 = WuAlphaColorQuantizer.IndexCount - 1;
            cube[0].A1 = WuAlphaColorQuantizer.IndexAlphaCount - 1;

            int next = 0;

            for (int i = 1; i < colorCount; i++)
            {
                if (this.Cut(cube[next], cube[i]))
                {
                    vv[next] = cube[next].Volume > 1 ? this.Variance(cube[next]) : 0.0;
                    vv[i] = cube[i].Volume > 1 ? this.Variance(cube[i]) : 0.0;
                }
                else
                {
                    vv[next] = 0.0;
                    i--;
                }

                next = 0;

                double temp = vv[0];
                for (int k = 1; k <= i; k++)
                {
                    if (vv[k] > temp)
                    {
                        temp = vv[k];
                        next = k;
                    }
                }

                if (temp <= 0.0)
                {
                    colorCount = i + 1;
                    break;
                }
            }
        }

        /// <summary>
        /// Generates the quantized result.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="colorCount">The color count.</param>
        /// <param name="cube">The cube.</param>
        /// <returns>The result.</returns>
        private ColorQuantizerResult GenerateResult(byte[] image, int colorCount, Box[] cube)
        {
            var quantizedImage = new ColorQuantizerResult(image.Length / 4, colorCount);

            for (int k = 0; k < colorCount; k++)
            {
                this.Mark(cube[k], (byte)k);

                double weight = WuAlphaColorQuantizer.Volume(cube[k], this.vwt);

                if (weight != 0)
                {
                    quantizedImage.Palette[(k * 4) + 3] = (byte)(WuAlphaColorQuantizer.Volume(cube[k], this.vma) / weight);
                    quantizedImage.Palette[(k * 4) + 2] = (byte)(WuAlphaColorQuantizer.Volume(cube[k], this.vmr) / weight);
                    quantizedImage.Palette[(k * 4) + 1] = (byte)(WuAlphaColorQuantizer.Volume(cube[k], this.vmg) / weight);
                    quantizedImage.Palette[k * 4] = (byte)(WuAlphaColorQuantizer.Volume(cube[k], this.vmb) / weight);
                }
                else
                {
                    quantizedImage.Palette[(k * 4) + 3] = 0xff;
                    quantizedImage.Palette[(k * 4) + 2] = 0;
                    quantizedImage.Palette[(k * 4) + 1] = 0;
                    quantizedImage.Palette[k * 4] = 0;
                }
            }

            for (int i = 0; i < image.Length / 4; i++)
            {
                int a = image[(i * 4) + 3] >> (8 - WuAlphaColorQuantizer.IndexAlphaBits);
                int r = image[(i * 4) + 2] >> (8 - WuAlphaColorQuantizer.IndexBits);
                int g = image[(i * 4) + 1] >> (8 - WuAlphaColorQuantizer.IndexBits);
                int b = image[i * 4] >> (8 - WuAlphaColorQuantizer.IndexBits);

                int ind = WuAlphaColorQuantizer.GetIndex(r + 1, g + 1, b + 1, a + 1);

                quantizedImage.Bytes[i] = this.tag[ind];
            }

            return quantizedImage;
        }

        /// <summary>
        /// Generates the quantized result.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="colorCount">The color count.</param>
        /// <param name="cube">The cube.</param>
        /// <param name="width">Width of image</param>
        /// <param name="height">Height of image</param>
        /// <param name="destPixels">Pixel values are written here. Must provide width*height memory.</param>
        /// <param name="padMultiple4">True to write zero padding to make row bytes multiple of 4</param>
        /// <returns>Receives colors</returns>
        private unsafe uint[] GenerateResult(uint* image, int colorCount, Box[] cube, int width, int height, byte* destPixels, bool padMultiple4)
        {
            uint[] palette = new uint[colorCount];

            // rows must be a multiple of 4, hence padding up to 3 bytes for 8-bit indexed pixels
            int widthMod4 = width % 4;
            int widthZeros = widthMod4 != 0 ? 4 - widthMod4 : 0;

            for (int k = 0; k < colorCount; k++)
            {
                this.Mark(cube[k], (byte)k);

                double weight = WuAlphaColorQuantizer.Volume(cube[k], this.vwt);

                if (weight != 0)
                {
                    uint a = (byte)(WuAlphaColorQuantizer.Volume(cube[k], this.vma) / weight);
                    uint r = (byte)(WuAlphaColorQuantizer.Volume(cube[k], this.vmr) / weight);
                    uint g = (byte)(WuAlphaColorQuantizer.Volume(cube[k], this.vmg) / weight);
                    uint b = (byte)(WuAlphaColorQuantizer.Volume(cube[k], this.vmb) / weight);

                    palette[k] = (a << 24) | (r << 16) | (g << 8) | b;
                }
                else
                {
                    palette[k] = 0xFF000000;
                }
            }

            for (int ri = 0; ri < height; ri++)
            {
                for (int ci = 0; ci < width; ci++)
                {
                    uint pix = image[0];

                    uint a = ((pix & 0xFF000000) >> 24) >> (8 - WuAlphaColorQuantizer.IndexAlphaBits);
                    uint r = ((pix & 0x00FF0000) >> 16) >> (8 - WuAlphaColorQuantizer.IndexBits);
                    uint g = ((pix & 0x0000FF00) >> 8) >> (8 - WuAlphaColorQuantizer.IndexBits);
                    uint b = (pix & 0x000000FF) >> (8 - WuAlphaColorQuantizer.IndexBits);

                    int ind = WuAlphaColorQuantizer.GetIndex((int)r + 1, (int)g + 1, (int)b + 1, (int)a + 1);

                    destPixels[0] = this.tag[ind];
                    destPixels++;
                    image++;
                }

                // write additional zero bytes if requested
                if (padMultiple4)
                { 
                    for (int c = 0; c < widthZeros; c++)
                    {
                        destPixels[0] = 0x00;
                        destPixels++;
                    }
                }
            }

            return palette;
        }
    }
}
