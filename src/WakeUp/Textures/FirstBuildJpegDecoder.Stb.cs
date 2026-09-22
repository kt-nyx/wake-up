// Derived from StbImageSharp 2.30.15, commit 6fd7aebe1dbf10e28d78745f42a0c095c61d1945.
// Public-domain port by StbImageSharpTeam, based on Sean Barrett's stb_image.
// Managed/bounded adaptation (c) 2026 kt-nyx and contributors.
// See third-party/StbImageSharp-JPEG.md and StbImageSharp-LICENSE.txt.
#nullable disable
using System;
namespace WakeUp;
internal static partial class FirstBuildJpegDecoder
{
		private static readonly uint[] stbi__bmask =
			{ 0, 1, 3, 7, 15, 31, 63, 127, 255, 511, 1023, 2047, 4095, 8191, 16383, 32767, 65535 };

		private static readonly int[] stbi__jbias =
			{ 0, -1, -3, -7, -15, -31, -63, -127, -255, -511, -1023, -2047, -4095, -8191, -16383, -32767 };

		private static readonly byte[] Jpeg_dezigzag =
		{
			0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7,
			14, 21, 28, 35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31, 39, 46,
			53, 60, 61, 54, 47, 55, 62, 63, 63, 63, 63, 63, 63, 63, 63, 63, 63, 63, 63, 63, 63, 63, 63
		};

		private static int stbi__build_huffman(Huffman h, Slice<int> count)
		{
			var i = 0;
			var j = 0;
			var k = 0;
			uint code = 0;
			for (i = 0; i < 16; ++i)
			{
				for (j = 0; j < count[i]; ++j)
				{
					h.size[k++] = (byte)(i + 1);
					if (k >= 257)
						return stbi__err("bad size list");
				}
			}

			h.size[k] = 0;
			code = 0;
			k = 0;
			for (j = 1; j <= 16; ++j)
			{
				h.delta[j] = (int)(k - code);
				if (h.size[k] == j)
				{
					while (h.size[k] == j)
						h.code[k++] = (ushort)code++;

					if (code - 1 >= 1u << j)
						return stbi__err("bad code lengths");
				}

				h.maxcode[j] = code << (16 - j);
				code <<= 1;
			}

			h.maxcode[j] = 0xffffffff;
			for (int fill = 0; fill < 512; fill++) h.fast[fill] = 255;
			for (i = 0; i < k; ++i)
			{
				int s = h.size[i];
				if (s <= 9)
				{
					var c = h.code[i] << (9 - s);
					var m = 1 << (9 - s);
					for (j = 0; j < m; ++j)
						h.fast[c + j] = (byte)i;
				}
			}

			h.Defined = true;
            return 1;
		}

		private static void stbi__build_fast_ac(short[] fast_ac, Huffman h)
		{
			var i = 0;
			for (i = 0; i < 1 << 9; ++i)
			{
				var fast = h.fast[i];
				fast_ac[i] = 0;
				if (fast < 255)
				{
					int rs = h.values[fast];
					var run = (rs >> 4) & 15;
					var magbits = rs & 15;
					int len = h.size[fast];
					if (magbits != 0 && len + magbits <= 9)
					{
						var k = ((i << len) & ((1 << 9) - 1)) >> (9 - magbits);
						var m = 1 << (magbits - 1);
						if (k < m)
							k += (int)((~0U << magbits) + 1);
						if (k >= -128 && k <= 127)
							fast_ac[i] = (short)(k * 256 + run * 16 + len + magbits);
					}
				}
			}
		}

		private static void stbi__grow_buffer_unsafe(Jpeg j)
		{
			do
			{
				if (j.nomore != 0) j.s.PaddingBits += 8;
                var b = (uint)(j.nomore != 0 ? 0 : stbi__get8(j.s));
				if (b == 0xff)
				{
					int c = stbi__get8(j.s);
					while (c == 0xff)
						c = stbi__get8(j.s);

					if (c != 0)
					{
						j.marker = (byte)c;
						j.nomore = 1;
						return;
					}
				}

				j.code_buffer |= b << (24 - j.code_bits);
				j.code_bits += 8;
			} while (j.code_bits <= 24);
		}

		private static int Jpeg_huff_decode(Jpeg j, Huffman h)
		{
			uint temp = 0;
			var c = 0;
			var k = 0;
			if (j.code_bits < 16)
				stbi__grow_buffer_unsafe(j);
			c = (int)((j.code_buffer >> (32 - 9)) & ((1 << 9) - 1));
			k = h.fast[c];
			if (k < 255)
			{
				int s = h.size[k];
				if (s > j.code_bits)
					return -1;
				j.code_buffer <<= s;
				j.code_bits -= s;
				return h.values[k];
			}

			temp = j.code_buffer >> 16;
			for (k = 9 + 1; ; ++k)
				if (temp < h.maxcode[k])
					break;

			if (k == 17)
			{
				j.code_bits -= 16;
				return -1;
			}

			if (k > j.code_bits)
				return -1;
			c = (int)(((j.code_buffer >> (32 - k)) & stbi__bmask[k]) + h.delta[k]);
			if (c < 0 || c >= 256)
				return -1;
			j.code_bits -= k;
			j.code_buffer <<= k;
			return h.values[c];
		}

		private static int stbi__extend_receive(Jpeg j, int n)
		{
			uint k = 0;
			var sgn = 0;
			if (j.code_bits < n)
				stbi__grow_buffer_unsafe(j);
			if (j.code_bits < n)
				return 0;
			sgn = (int)(j.code_buffer >> 31);
			k = Rotate(j.code_buffer, n);
			j.code_buffer = k & ~stbi__bmask[n];
			k &= stbi__bmask[n];
			j.code_bits -= n;
			return (int)(k + (stbi__jbias[n] & (sgn - 1)));
		}

		private static int Jpeg_get_bits(Jpeg j, int n)
		{
			uint k = 0;
			if (j.code_bits < n)
				stbi__grow_buffer_unsafe(j);
			if (j.code_bits < n)
				return 0;
			k = Rotate(j.code_buffer, n);
			j.code_buffer = k & ~stbi__bmask[n];
			k &= stbi__bmask[n];
			j.code_bits -= n;
			return (int)k;
		}

		private static int Jpeg_get_bit(Jpeg j)
		{
			uint k = 0;
			if (j.code_bits < 1)
				stbi__grow_buffer_unsafe(j);
			if (j.code_bits < 1)
				return 0;
			k = j.code_buffer;
			j.code_buffer <<= 1;
			--j.code_bits;
			return (int)(k & 0x80000000);
		}

		private static int Jpeg_decode_block(Jpeg j, Slice<short> data, Huffman hdc, Huffman hac,
			short[] fac, int b, ushort[] dequant)
		{
            j.s.Token.ThrowIfCancellationRequested(); j.s.DecodedBlocks++;
			var diff = 0;
			var dc = 0;
			var k = 0;
			var t = 0;
			if (j.code_bits < 16)
				stbi__grow_buffer_unsafe(j);
			t = Jpeg_huff_decode(j, hdc);
			if (t < 0 || t > 15)
				return stbi__err("bad huffman code");
			data.Clear(64);
			diff = t != 0 ? stbi__extend_receive(j, t) : 0;
			if (stbi__addints_valid(j.img_comp[b].dc_pred, diff) == 0)
				return stbi__err("bad delta");
			dc = j.img_comp[b].dc_pred + diff;
			j.img_comp[b].dc_pred = dc;
			if (stbi__mul2shorts_valid(dc, dequant[0]) == 0)
				return stbi__err("can't merge dc and ac");
			data[0] = CoefficientValue((long)dc * dequant[0]);
			k = 1;
			do
			{
				uint zig = 0;
				var c = 0;
				var r = 0;
				var s = 0;
				if (j.code_bits < 16)
					stbi__grow_buffer_unsafe(j);
				c = (int)((j.code_buffer >> (32 - 9)) & ((1 << 9) - 1));
				r = fac[c];
				if (r != 0)
				{
					k += (r >> 4) & 15;
					s = r & 15;
					if (s > j.code_bits)
						return stbi__err("bad huffman code");
					j.code_buffer <<= s;
					j.code_bits -= s;
					zig = Jpeg_dezigzag[k++];
					data[zig] = CoefficientValue((long)(r >> 8) * dequant[zig]);
				}
				else
				{
					var rs = Jpeg_huff_decode(j, hac);
					if (rs < 0)
						return stbi__err("bad huffman code");
					s = rs & 15;
					r = rs >> 4;
					if (s == 0)
					{
						if (rs != 0xf0)
							break;
						k += 16;
					}
					else
					{
						k += r;
						zig = Jpeg_dezigzag[k++];
						data[zig] = CoefficientValue((long)stbi__extend_receive(j, s) * dequant[zig]);
					}
				}
			} while (k < 64);

			return 1;
		}

		private static int Jpeg_decode_block_prog_dc(Jpeg j, Slice<short> data, Huffman hdc, int b)
		{
            j.s.Token.ThrowIfCancellationRequested(); j.s.DecodedBlocks++;
			var diff = 0;
			var dc = 0;
			var t = 0;
			if (j.spec_end != 0)
				return stbi__err("can't merge dc and ac");
			if (j.code_bits < 16)
				stbi__grow_buffer_unsafe(j);
			if (j.succ_high == 0)
			{
				data.Clear(64);
				t = Jpeg_huff_decode(j, hdc);
				if (t < 0 || t > 15)
					return stbi__err("can't merge dc and ac");
				diff = t != 0 ? stbi__extend_receive(j, t) : 0;
				if (stbi__addints_valid(j.img_comp[b].dc_pred, diff) == 0)
					return stbi__err("bad delta");
				dc = j.img_comp[b].dc_pred + diff;
				j.img_comp[b].dc_pred = dc;
				if (stbi__mul2shorts_valid(dc, 1 << j.succ_low) == 0)
					return stbi__err("can't merge dc and ac");
				data[0] = CoefficientValue((long)dc * (1 << j.succ_low));
			}
			else
			{
				if (Jpeg_get_bit(j) != 0)
					data[0] = CoefficientValue((long)data[0] + (1 << j.succ_low));
			}

			return 1;
		}

		private static int Jpeg_decode_block_prog_ac(Jpeg j, Slice<short> data, Huffman hac, short[] fac)
		{
            j.s.Token.ThrowIfCancellationRequested(); j.s.DecodedBlocks++;
			var k = 0;
			if (j.spec_start == 0)
				return stbi__err("can't merge dc and ac");
			if (j.succ_high == 0)
			{
				var shift = j.succ_low;
				if (j.eob_run != 0)
				{
					--j.eob_run;
					return 1;
				}

				k = j.spec_start;
				do
				{
					uint zig = 0;
					var c = 0;
					var r = 0;
					var s = 0;
					if (j.code_bits < 16)
						stbi__grow_buffer_unsafe(j);
					c = (int)((j.code_buffer >> (32 - 9)) & ((1 << 9) - 1));
					r = fac[c];
					if (r != 0)
					{
						k += (r >> 4) & 15;
						s = r & 15;
						if (s > j.code_bits)
							return stbi__err("bad huffman code");
						j.code_buffer <<= s;
						j.code_bits -= s;
						zig = Jpeg_dezigzag[k++];
						data[zig] = CoefficientValue((long)(r >> 8) * (1 << shift));
					}
					else
					{
						var rs = Jpeg_huff_decode(j, hac);
						if (rs < 0)
							return stbi__err("bad huffman code");
						s = rs & 15;
						r = rs >> 4;
						if (s == 0)
						{
							if (r < 15)
							{
								j.eob_run = 1 << r;
								if (r != 0)
									j.eob_run += Jpeg_get_bits(j, r);
								--j.eob_run;
								break;
							}

							k += 16;
						}
						else
						{
							k += r;
							zig = Jpeg_dezigzag[k++];
							data[zig] = CoefficientValue((long)stbi__extend_receive(j, s) * (1 << shift));
						}
					}
				} while (k <= j.spec_end);
			}
			else
			{
				var bit = (short)(1 << j.succ_low);
				if (j.eob_run != 0)
				{
					--j.eob_run;
					for (k = j.spec_start; k <= j.spec_end; ++k)
					{
						var p = data + Jpeg_dezigzag[k];
						if (p[0] != 0)
							if (Jpeg_get_bit(j) != 0)
								if ((p[0] & bit) == 0)
								{
									if (p[0] > 0)
										p[0] = CoefficientValue((long)p[0] + bit);
									else
										p[0] = CoefficientValue((long)p[0] - bit);
								}
					}
				}
				else
				{
					k = j.spec_start;
					do
					{
						var r = 0;
						var s = 0;
						var rs = Jpeg_huff_decode(j, hac);
						if (rs < 0)
							return stbi__err("bad huffman code");
						s = rs & 15;
						r = rs >> 4;
						if (s == 0)
						{
							if (r < 15)
							{
								j.eob_run = (1 << r) - 1;
								if (r != 0)
									j.eob_run += Jpeg_get_bits(j, r);
								r = 64;
							}
						}
						else
						{
							if (s != 1)
								return stbi__err("bad huffman code");
							if (Jpeg_get_bit(j) != 0)
								s = bit;
							else
								s = -bit;
						}

						while (k <= j.spec_end)
						{
							var p = data + Jpeg_dezigzag[k++];
							if (p[0] != 0)
							{
								if (Jpeg_get_bit(j) != 0)
									if ((p[0] & bit) == 0)
									{
										if (p[0] > 0)
											p[0] = CoefficientValue((long)p[0] + bit);
										else
											p[0] = CoefficientValue((long)p[0] - bit);
									}
							}
							else
							{
								if (r == 0)
								{
									p[0] = (short)s;
									break;
								}

								--r;
							}
						}
					} while (k <= j.spec_end);
				}
			}

			return 1;
		}

        private static short CoefficientValue(long value)
        {
            if (value < short.MinValue || value > short.MaxValue) throw Invalid("coefficient-range");
            return (short)value;
        }

		private static void stbi__idct_block(Slice<byte> _out_, int out_stride, Slice<short> data, int[] scratch)
		{
            // Do not publish a wrapped result for extreme/unqualified coefficients.
            checked
            {
            // Same pinned stb factorization, with 13-bit signed-rounded constants.
            // Native GOG grayscale oracle: jpeg/idct-oracle-results.json.
			var i = 0;
			Slice<int> val = scratch;
			var v = val;
			Slice<byte> o;
			var d = data;
			for (i = 0; i < 8; ++i, ++d, ++v)
				if (d[8] == 0 && d[16] == 0 && d[24] == 0 && d[32] == 0 && d[40] == 0 && d[48] == 0 && d[56] == 0)
				{
					var dcterm = d[0] * 4;
					v[0] = v[8] = v[16] = v[24] = v[32] = v[40] = v[48] = v[56] = dcterm;
				}
				else
				{
					var t0 = 0;
					var t1 = 0;
					var t2 = 0;
					var t3 = 0;
					var p1 = 0;
					var p2 = 0;
					var p3 = 0;
					var p4 = 0;
					var p5 = 0;
					var x0 = 0;
					var x1 = 0;
					var x2 = 0;
					var x3 = 0;
					p2 = d[16];
					p3 = d[48];
					p1 = (p2 + p3) * 4433;
					t2 = p1 + p3 * -15137;
					t3 = p1 + p2 * 6270;
					p2 = d[0];
					p3 = d[32];
					t0 = (p2 + p3) * 8192;
					t1 = (p2 - p3) * 8192;
					x0 = t0 + t3;
					x3 = t0 - t3;
					x1 = t1 + t2;
					x2 = t1 - t2;
					t0 = d[56];
					t1 = d[40];
					t2 = d[24];
					t3 = d[8];
					p3 = t0 + t2;
					p4 = t1 + t3;
					p1 = t0 + t3;
					p2 = t1 + t2;
					p5 = (p3 + p4) * 9633;
					t0 = t0 * 2446;
					t1 = t1 * 16819;
					t2 = t2 * 25172;
					t3 = t3 * 12299;
					p1 = p5 + p1 * -7373;
					p2 = p5 + p2 * -20995;
					p3 = p3 * -16069;
					p4 = p4 * -3196;
					t3 += p1 + p4;
					t2 += p2 + p3;
					t1 += p2 + p4;
					t0 += p1 + p3;
					x0 += 1024;
					x1 += 1024;
					x2 += 1024;
					x3 += 1024;
					v[0] = (x0 + t3) >> 11;
					v[56] = (x0 - t3) >> 11;
					v[8] = (x1 + t2) >> 11;
					v[48] = (x1 - t2) >> 11;
					v[16] = (x2 + t1) >> 11;
					v[40] = (x2 - t1) >> 11;
					v[24] = (x3 + t0) >> 11;
					v[32] = (x3 - t0) >> 11;
				}

			for (i = 0, v = val, o = _out_; i < 8; ++i, v += 8, o += out_stride)
			{
				var t0 = 0;
				var t1 = 0;
				var t2 = 0;
				var t3 = 0;
				var p1 = 0;
				var p2 = 0;
				var p3 = 0;
				var p4 = 0;
				var p5 = 0;
				var x0 = 0;
				var x1 = 0;
				var x2 = 0;
				var x3 = 0;
				p2 = v[2];
				p3 = v[6];
				p1 = (p2 + p3) * 4433;
				t2 = p1 + p3 * -15137;
				t3 = p1 + p2 * 6270;
				p2 = v[0];
				p3 = v[4];
				t0 = (p2 + p3) * 8192;
				t1 = (p2 - p3) * 8192;
				x0 = t0 + t3;
				x3 = t0 - t3;
				x1 = t1 + t2;
				x2 = t1 - t2;
				t0 = v[7];
				t1 = v[5];
				t2 = v[3];
				t3 = v[1];
				p3 = t0 + t2;
				p4 = t1 + t3;
				p1 = t0 + t3;
				p2 = t1 + t2;
				p5 = (p3 + p4) * 9633;
				t0 = t0 * 2446;
				t1 = t1 * 16819;
				t2 = t2 * 25172;
				t3 = t3 * 12299;
				p1 = p5 + p1 * -7373;
				p2 = p5 + p2 * -20995;
				p3 = p3 * -16069;
				p4 = p4 * -3196;
				t3 += p1 + p4;
				t2 += p2 + p3;
				t1 += p2 + p4;
				t0 += p1 + p3;
				x0 += 131072 + (128 << 18);
				x1 += 131072 + (128 << 18);
				x2 += 131072 + (128 << 18);
				x3 += 131072 + (128 << 18);
				o[0] = stbi__clamp((x0 + t3) >> 18);
				o[7] = stbi__clamp((x0 - t3) >> 18);
				o[1] = stbi__clamp((x1 + t2) >> 18);
				o[6] = stbi__clamp((x1 - t2) >> 18);
				o[2] = stbi__clamp((x2 + t1) >> 18);
				o[5] = stbi__clamp((x2 - t1) >> 18);
				o[3] = stbi__clamp((x3 + t0) >> 18);
				o[4] = stbi__clamp((x3 - t0) >> 18);
			}
		            }
        }

		private static byte stbi__get_marker(Jpeg j)
		{
            if (++j.s.Markers > 4096) throw Invalid("marker-count");
			byte x = 0;
			if (j.marker != 0xff)
			{
				x = j.marker;
				j.marker = 0xff;
				return x;
			}

			x = stbi__get8(j.s);
			if (x != 0xff)
				return 0xff;
			while (x == 0xff)
				x = stbi__get8(j.s);

			return x;
		}

		private static void Jpeg_reset(Jpeg j)
		{
			j.s.PaddingBits = 0;
            j.code_bits = 0;
			j.code_buffer = 0;
			j.nomore = 0;
			j.img_comp[0].dc_pred = j.img_comp[1].dc_pred = j.img_comp[2].dc_pred = j.img_comp[3].dc_pred = 0;
			j.marker = 0xff;
			j.todo = j.restart_interval != 0 ? j.restart_interval : 0x7fffffff;
			j.eob_run = 0;
		}

		private static int stbi__parse_entropy_coded_data(Jpeg z)
		{
			Jpeg_reset(z);
			if (z.progressive == 0)
			{
				if (z.scan_n == 1)
				{
					var i = 0;
					var j = 0;
					Slice<short> data = z.s.BlockScratch;
					var n = z.order[0];
					var w = (z.img_comp[n].x + 7) >> 3;
					var h = (z.img_comp[n].y + 7) >> 3;
					for (j = 0; j < h; ++j)
						for (i = 0; i < w; ++i)
						{
							var ha = z.img_comp[n].ha;
							using (var dptr = z.huff_dc[z.img_comp[n].hd])
using (var aptr = z.huff_ac[ha])
{
								if (Jpeg_decode_block(z, data, dptr, aptr,
									z.fast_ac[ha], n, z.dequant[z.img_comp[n].tq]) == 0)
									return 0;
							}
							z.idct_block_kernel(z.img_comp[n].data + z.img_comp[n].w2 * j * 8 + i * 8, z.img_comp[n].w2,
								data);
							z.s.CheckEntropy(z);
                            if (--z.todo <= 0)
							{
								if (z.code_bits < 24)
									stbi__grow_buffer_unsafe(z);
								if (!(z.marker >= 0xd0 && z.marker <= 0xd7))
									return 1;
								Jpeg_reset(z);
							}
						}

					return 1;
				}
				else
				{
					var i = 0;
					var j = 0;
					var k = 0;
					var x = 0;
					var y = 0;
					Slice<short> data = z.s.BlockScratch;
					for (j = 0; j < z.img_mcu_y; ++j)
						for (i = 0; i < z.img_mcu_x; ++i)
						{
							for (k = 0; k < z.scan_n; ++k)
							{
								var n = z.order[k];
								for (y = 0; y < z.img_comp[n].v; ++y)
									for (x = 0; x < z.img_comp[n].h; ++x)
									{
										var x2 = (i * z.img_comp[n].h + x) * 8;
										var y2 = (j * z.img_comp[n].v + y) * 8;
										var ha = z.img_comp[n].ha;

										using (var dptr = z.huff_dc[z.img_comp[n].hd])
using (var aptr = z.huff_ac[ha])
{
											if (Jpeg_decode_block(z, data, dptr, aptr,
											z.fast_ac[ha], n, z.dequant[z.img_comp[n].tq]) == 0)
												return 0;
										}
										z.idct_block_kernel(z.img_comp[n].data + z.img_comp[n].w2 * y2 + x2, z.img_comp[n].w2,
											data);
									}
							}

							z.s.CheckEntropy(z);
                            if (--z.todo <= 0)
							{
								if (z.code_bits < 24)
									stbi__grow_buffer_unsafe(z);
								if (!(z.marker >= 0xd0 && z.marker <= 0xd7))
									return 1;
								Jpeg_reset(z);
							}
						}

					return 1;
				}
			}

			if (z.scan_n == 1)
			{
				var i = 0;
				var j = 0;
				var n = z.order[0];
				var w = (z.img_comp[n].x + 7) >> 3;
				var h = (z.img_comp[n].y + 7) >> 3;
				for (j = 0; j < h; ++j)
					for (i = 0; i < w; ++i)
					{
						var data = z.img_comp[n].coeff + 64 * (i + j * z.img_comp[n].coeff_w);
						if (z.spec_start == 0)
						{
							using (var dptr = z.huff_dc[z.img_comp[n].hd])
{
								if (Jpeg_decode_block_prog_dc(z, data, dptr, n) == 0)
									return 0;
							}
						}
						else
						{
							var ha = z.img_comp[n].ha;
							using (var aptr = z.huff_ac[ha])
{
								if (Jpeg_decode_block_prog_ac(z, data, aptr, z.fast_ac[ha]) == 0)
									return 0;
							}
						}

						z.s.CheckEntropy(z);
                            if (--z.todo <= 0)
						{
							if (z.code_bits < 24)
								stbi__grow_buffer_unsafe(z);
							if (!(z.marker >= 0xd0 && z.marker <= 0xd7))
								return 1;
							Jpeg_reset(z);
						}
					}

				return 1;
			}
			else
			{
				var i = 0;
				var j = 0;
				var k = 0;
				var x = 0;
				var y = 0;
				for (j = 0; j < z.img_mcu_y; ++j)
					for (i = 0; i < z.img_mcu_x; ++i)
					{
						for (k = 0; k < z.scan_n; ++k)
						{
							var n = z.order[k];
							for (y = 0; y < z.img_comp[n].v; ++y)
								for (x = 0; x < z.img_comp[n].h; ++x)
								{
									var x2 = i * z.img_comp[n].h + x;
									var y2 = j * z.img_comp[n].v + y;
									var data = z.img_comp[n].coeff + 64 * (x2 + y2 * z.img_comp[n].coeff_w);


									using (var dptr = z.huff_dc[z.img_comp[n].hd])
{
										if (Jpeg_decode_block_prog_dc(z, data, dptr, n) == 0)
											return 0;
									}
								}
						}

						z.s.CheckEntropy(z);
                            if (--z.todo <= 0)
						{
							if (z.code_bits < 24)
								stbi__grow_buffer_unsafe(z);
							if (!(z.marker >= 0xd0 && z.marker <= 0xd7))
								return 1;
							Jpeg_reset(z);
						}
					}

				return 1;
			}
		}

		private static void Jpeg_dequantize(Slice<short> data, ushort[] dequant)
		{
			var i = 0;
			for (i = 0; i < 64; ++i)
				data[i] = CoefficientValue((long)data[i] * dequant[i]);
		}

		private static void Jpeg_finish(Jpeg z)
		{
			if (z.progressive != 0)
			{
				var i = 0;
				var j = 0;
				var n = 0;
				for (n = 0; n < z.s.img_n; ++n)
				{
					var w = (z.img_comp[n].x + 7) >> 3;
					var h = (z.img_comp[n].y + 7) >> 3;
					for (j = 0; j < h; ++j)
						for (i = 0; i < w; ++i)
						{
							var data = z.img_comp[n].coeff + 64 * (i + j * z.img_comp[n].coeff_w);
							Jpeg_dequantize(data, z.dequant[z.img_comp[n].tq]);
							z.idct_block_kernel(z.img_comp[n].data + z.img_comp[n].w2 * j * 8 + i * 8, z.img_comp[n].w2,
								data);
						}
				}
			}
		}

		private static int stbi__process_marker(Jpeg z, int m)
		{
			var L = 0;
			switch (m)
			{
				case 0xff:
					return stbi__err("expected marker");
				case 0xDD:
					if (stbi__get16be(z.s) != 4)
						return stbi__err("bad DRI len");
					z.restart_interval = stbi__get16be(z.s);
					return 1;
				case 0xDB:
					L = stbi__get16be(z.s) - 2;
					while (L > 0)
					{
						int q = stbi__get8(z.s);
						var p = q >> 4;
						var sixteen = p != 0 ? 1 : 0;
						var t = q & 15;
						var i = 0;
						if (p != 0 && p != 1)
							return stbi__err("bad DQT type");
						if (t > 3)
							return stbi__err("bad DQT table");
						z.s.QuantizationTables[t] = true;
                        for (i = 0; i < 64; ++i)
							z.dequant[t][Jpeg_dezigzag[i]] =
								(ushort)(sixteen != 0 ? stbi__get16be(z.s) : stbi__get8(z.s));
						L -= sixteen != 0 ? 129 : 65;
					}

					return L == 0 ? 1 : 0;
				case 0xC4:
					L = stbi__get16be(z.s) - 2;
					Slice<int> sizes = z.s.HuffmanScratch;
					while (L > 0)
					{
						var i = 0;
						var n = 0;
						int q = stbi__get8(z.s);
						var tc = q >> 4;
						var th = q & 15;
						if (tc > 1 || th > 3)
							return stbi__err("bad DHT header");
						for (i = 0; i < 16; ++i)
						{
							sizes[i] = stbi__get8(z.s);
							n += sizes[i];
						}
						if (n > 256)
							return stbi__err("bad DHT header");

						L -= 17;
						if (tc == 0)
						{
							using (var hptr = z.huff_dc[th])
{
								if (stbi__build_huffman(hptr, sizes) == 0)
									return 0;

								var v = hptr.values;
								for (i = 0; i < n; ++i)
									v[i] = stbi__get8(z.s);
							}
						}
						else
						{
							using (var aptr = z.huff_ac[th])
{
								if (stbi__build_huffman(aptr, sizes) == 0)
									return 0;

								var v = aptr.values;
								for (i = 0; i < n; ++i)
									v[i] = stbi__get8(z.s);
							}
						}

						if (tc != 0)
							using (var aptr = z.huff_ac[th])
{
								stbi__build_fast_ac(z.fast_ac[th], aptr);
							}
						L -= n;
					}

					return L == 0 ? 1 : 0;
			}

			if (m >= 0xE0 && m <= 0xEF || m == 0xFE)
			{
				L = stbi__get16be(z.s);
				if (L < 2)
				{
					if (m == 0xFE)
						return stbi__err("bad COM len");
					return stbi__err("bad APP len");
				}

				L -= 2;
                z.s.ObserveApp(m, L);
				if (m == 0xE0 && L >= 5)
				{
					var ok = 1;
					var i = 0;
					for (i = 0; i < 5; ++i)
						if (stbi__get8(z.s) != (m == 0xE0 ? JfifTag[i] : AdobeTag[i]))
							ok = 0;
					L -= 5;
					if (ok != 0)
						z.jfif = 1;
				}
				else if (m == 0xEE && L >= 12)
				{
					var ok = 1;
					var i = 0;
					for (i = 0; i < 5; ++i)
						if (stbi__get8(z.s) != AdobeTag[i])
							ok = 0;
					L -= 5;
					if (ok != 0)
					{
						stbi__get16be(z.s); // two-byte version follows the five-byte identifier
						stbi__get16be(z.s);
						stbi__get16be(z.s);
						z.app14_color_transform = stbi__get8(z.s);
						L -= 7;
					}
				}

				stbi__skip(z.s, L);
				return 1;
			}

			return stbi__err("unknown marker");
		}

		private static int stbi__process_scan_header(Jpeg z)
		{
            if (++z.s.Scans > 256) throw Invalid("scan-count");
			var i = 0;
			var Ls = stbi__get16be(z.s);
			z.scan_n = stbi__get8(z.s);
			if (z.scan_n < 1 || z.scan_n > 4 || z.scan_n > z.s.img_n)
				return stbi__err("bad SOS component count");
			if (Ls != 6 + 2 * z.scan_n)
				return stbi__err("bad SOS len");
			for (i = 0; i < z.scan_n; ++i)
			{
				int id = stbi__get8(z.s);
				var which = 0;
				int q = stbi__get8(z.s);
				for (which = 0; which < z.s.img_n; ++which)
					if (z.img_comp[which].id == id)
						break;

				if (which == z.s.img_n)
					return 0;
				z.img_comp[which].hd = q >> 4;
				if (z.img_comp[which].hd > 3)
					return stbi__err("bad DC huff");
				z.img_comp[which].ha = q & 15;
				if (z.img_comp[which].ha > 3)
					return stbi__err("bad AC huff");
				z.order[i] = which;
			}

			{
				var aa = 0;
				z.spec_start = stbi__get8(z.s);
				z.spec_end = stbi__get8(z.s);
				aa = stbi__get8(z.s);
				z.succ_high = aa >> 4;
				z.succ_low = aa & 15;
				if (z.progressive != 0)
				{
					if (z.spec_start > 63 || z.spec_end > 63 || z.spec_start > z.spec_end || z.succ_high > 13 ||
						z.succ_low > 13)
						return stbi__err("bad SOS");
				}
				else
				{
					if (z.spec_start != 0)
						return stbi__err("bad SOS");
					if (z.succ_high != 0 || z.succ_low != 0)
						return stbi__err("bad SOS");
					z.spec_end = 63;
				}
			}

			return 1;
		}

		private static int stbi__process_frame_header(Jpeg z, int scan)
		{
			var s = z.s;
			var Lf = 0;
			var p = 0;
			var i = 0;
			var q = 0;
			var h_max = 1;
			var v_max = 1;
			var c = 0;
			Lf = stbi__get16be(s);
			if (Lf < 11)
				return stbi__err("bad SOF len");
			p = stbi__get8(s);
			if (p != 8)
				return stbi__err("only 8-bit");
			s.img_y = (uint)stbi__get16be(s);
			if (s.img_y == 0)
				return stbi__err("no header height");
			s.img_x = (uint)stbi__get16be(s);
			if (s.img_x == 0)
				return stbi__err("0 width");
			if (s.img_y > MaximumDimension)
				return stbi__err("too large");
			if (s.img_x > MaximumDimension)
				return stbi__err("too large");
			c = stbi__get8(s);
			if (c != 3 && c != 1 && c != 4)
				return stbi__err("bad component count");
			s.img_n = c;
			for (i = 0; i < c; ++i)
			{
				z.img_comp[i].data = default;
				z.img_comp[i].linebuf = default;
			}

			if (Lf != 8 + 3 * s.img_n)
				return stbi__err("bad SOF len");
			z.rgb = 0;
			for (i = 0; i < s.img_n; ++i)
			{
				z.img_comp[i].id = stbi__get8(s);
				if (s.img_n == 3 && z.img_comp[i].id == RgbTag[i])
					++z.rgb;
				q = stbi__get8(s);
				z.img_comp[i].h = q >> 4;
				if (z.img_comp[i].h == 0 || z.img_comp[i].h > 4)
					return stbi__err("bad H");
				z.img_comp[i].v = q & 15;
				if (z.img_comp[i].v == 0 || z.img_comp[i].v > 4)
					return stbi__err("bad V");
				z.img_comp[i].tq = stbi__get8(s);
				if (z.img_comp[i].tq > 3)
					return stbi__err("bad TQ");
			}

			if (scan != STBI__SCAN_load)
				return 1;
			if (stbi__mad3sizes_valid((int)s.img_x, (int)s.img_y, s.img_n, 0) == 0)
				return stbi__err("too large");
			for (i = 0; i < s.img_n; ++i)
			{
				if (z.img_comp[i].h > h_max)
					h_max = z.img_comp[i].h;
				if (z.img_comp[i].v > v_max)
					v_max = z.img_comp[i].v;
			}

			for (i = 0; i < s.img_n; ++i)
			{
				if (h_max % z.img_comp[i].h != 0)
					return stbi__err("bad H");
				if (v_max % z.img_comp[i].v != 0)
					return stbi__err("bad V");
			}

			z.s.CheckFrame(z);
            z.img_h_max = h_max;
			z.img_v_max = v_max;
			z.img_mcu_w = h_max * 8;
			z.img_mcu_h = v_max * 8;
			z.img_mcu_x = (int)((s.img_x + z.img_mcu_w - 1) / z.img_mcu_w);
			z.img_mcu_y = (int)((s.img_y + z.img_mcu_h - 1) / z.img_mcu_h);
			for (i = 0; i < s.img_n; ++i)
			{
				z.img_comp[i].x = (int)((s.img_x * z.img_comp[i].h + h_max - 1) / h_max);
				z.img_comp[i].y = (int)((s.img_y * z.img_comp[i].v + v_max - 1) / v_max);
				z.img_comp[i].w2 = z.img_mcu_x * z.img_comp[i].h * 8;
				z.img_comp[i].h2 = z.img_mcu_y * z.img_comp[i].v * 8;
				z.img_comp[i].coeff = default;
				z.img_comp[i].linebuf = default;
                z.img_comp[i].data = z.s.Bytes(checked(z.img_comp[i].w2 * z.img_comp[i].h2));
				if (z.progressive != 0)
				{
					z.img_comp[i].coeff_w = z.img_comp[i].w2 / 8;
					z.img_comp[i].coeff_h = z.img_comp[i].h2 / 8;
                    z.img_comp[i].coeff = z.s.Shorts(checked(z.img_comp[i].w2 * z.img_comp[i].h2));
				}
			}

			return 1;
		}

		private static int stbi__decode_jpeg_header(Jpeg z, int scan)
		{
			var m = 0;
			z.jfif = 0;
			z.app14_color_transform = -1;
			z.marker = 0xff;
			m = stbi__get_marker(z);
			if (!(m == 0xd8))
				return stbi__err("no SOI");
			if (scan == STBI__SCAN_type)
				return 1;
			m = stbi__get_marker(z);
			while (!(m == 0xc0 || m == 0xc1 || m == 0xc2))
			{
				if (stbi__process_marker(z, m) == 0)
					return 0;
				m = stbi__get_marker(z);
				while (m == 0xff)
				{
					if (stbi__at_eof(z.s) != 0)
						return stbi__err("no SOF");
					m = stbi__get_marker(z);
				}
			}

			z.progressive = m == 0xc2 ? 1 : 0;
			if (stbi__process_frame_header(z, scan) == 0)
				return 0;
			return 1;
		}

		private static byte stbi__skip_jpeg_junk_at_end(Jpeg j)
		{
			while (stbi__at_eof(j.s) == 0)
			{
				var x = stbi__get8(j.s);
				while (x == 0xff)
				{
					if (stbi__at_eof(j.s) != 0)
						return 0xff;
					x = stbi__get8(j.s);
					if (x != 0x00 && x != 0xff)
						return x;
				}
			}

			return 0xff;
		}

		private static int stbi__decode_jpeg_image(Jpeg j)
		{
			var m = 0;
			for (m = 0; m < 4; m++)
			{
			}

			j.restart_interval = 0;
			if (stbi__decode_jpeg_header(j, STBI__SCAN_load) == 0)
				return 0;
			m = stbi__get_marker(j);
			while (!(m == 0xd9))
				if (m == 0xda)
				{
					if (stbi__process_scan_header(j) == 0)
						return 0;
					j.s.BeginScan(j);
                    if (stbi__parse_entropy_coded_data(j) == 0)
						return 0;
					j.s.EndScan(j);
                    if (j.marker == 0xff)
						j.marker = stbi__skip_jpeg_junk_at_end(j);

					m = stbi__get_marker(j);
					if (m >= 0xd0 && m <= 0xd7)
						m = stbi__get_marker(j);
				}
				else if (m == 0xdc)
				{
					var Ld = stbi__get16be(j.s);
					var NL = (uint)stbi__get16be(j.s);
					if (Ld != 4)
						return stbi__err("bad DNL len");
					if (NL != j.s.img_y)
						return stbi__err("bad DNL height");
					m = stbi__get_marker(j);
				}
				else
				{
					if (stbi__process_marker(j, m) == 0)
						throw Invalid("malformed-marker-after-scan");
					m = stbi__get_marker(j);
				}

			if (j.progressive != 0)
				Jpeg_finish(j);
			return 1;
		}

		private static Slice<byte> resample_row_1(Slice<byte> _out_, Slice<byte> in_near, Slice<byte> in_far, int w, int hs)
		{
			return in_near;
		}

		private static Slice<byte> stbi__resample_row_v_2(Slice<byte> _out_, Slice<byte> in_near, Slice<byte> in_far, int w, int hs)
		{
			var i = 0;
			for (i = 0; i < w; ++i)
				_out_[i] = (byte)((3 * in_near[i] + in_far[i] + 2) >> 2);

			return _out_;
		}

		private static Slice<byte> stbi__resample_row_h_2(Slice<byte> _out_, Slice<byte> in_near, Slice<byte> in_far, int w, int hs)
		{
			var i = 0;
			var input = in_near;
			if (w == 1)
			{
				_out_[0] = _out_[1] = input[0];
				return _out_;
			}

			_out_[0] = input[0];
			_out_[1] = (byte)((input[0] * 3 + input[1] + 2) >> 2);
			for (i = 1; i < w - 1; ++i)
			{
				var n = 3 * input[i];
				_out_[i * 2 + 0] = (byte)((n + input[i - 1] + 1) >> 2);
				_out_[i * 2 + 1] = (byte)((n + input[i + 1] + 2) >> 2);
			}

			_out_[i * 2 + 0] = (byte)((input[w - 1] * 3 + input[w - 2] + 1) >> 2);
			_out_[i * 2 + 1] = input[w - 1];
			return _out_;
		}

		private static Slice<byte> stbi__resample_row_hv_2(Slice<byte> _out_, Slice<byte> in_near, Slice<byte> in_far, int w, int hs)
		{
			var i = 0;
			var t0 = 0;
			var t1 = 0;
			if (w == 1)
			{
				_out_[0] = _out_[1] = (byte)((3 * in_near[0] + in_far[0] + 2) >> 2);
				return _out_;
			}

			t1 = 3 * in_near[0] + in_far[0];
			_out_[0] = (byte)((t1 + 2) >> 2);
			for (i = 1; i < w; ++i)
			{
				t0 = t1;
				t1 = 3 * in_near[i] + in_far[i];
				_out_[i * 2 - 1] = (byte)((3 * t0 + t1 + 7) >> 4);
				_out_[i * 2] = (byte)((3 * t1 + t0 + 8) >> 4);
			}

			_out_[w * 2 - 1] = (byte)((4 * t1 + 7) >> 4);
			return _out_;
		}

		private static Slice<byte> stbi__resample_row_generic(Slice<byte> _out_, Slice<byte> in_near, Slice<byte> in_far, int w, int hs)
		{
			var i = 0;
			var j = 0;
			for (i = 0; i < w; ++i)
				for (j = 0; j < hs; ++j)
					_out_[i * hs + j] = in_near[i];

			return _out_;
		}

		private static void stbi__YCbCr_to_RGB_row(Slice<byte> _out_, Slice<byte> y, Slice<byte> pcb, Slice<byte> pcr, int count, int step)
		{
			var i = 0;
			for (i = 0; i < count; ++i)
			{
				var y_fixed = (y[i] << 16) + (1 << 15);
				var r = 0;
				var g = 0;
				var b = 0;
				var cr = pcr[i] - 128;
				var cb = pcb[i] - 128;
				// Existing YCbCr transform, rounded at 16-bit precision without
                // truncating the green Cb contribution before the final sum.
                r = y_fixed + cr * 91881;
                g = y_fixed - cr * 46802 - cb * 22554;
                b = y_fixed + cb * 116130;
				r >>= 16;
				g >>= 16;
				b >>= 16;
				if ((uint)r > 255)
				{
					if (r < 0)
						r = 0;
					else
						r = 255;
				}

				if ((uint)g > 255)
				{
					if (g < 0)
						g = 0;
					else
						g = 255;
				}

				if ((uint)b > 255)
				{
					if (b < 0)
						b = 0;
					else
						b = 255;
				}

				_out_[0] = (byte)r;
				_out_[1] = (byte)g;
				_out_[2] = (byte)b;
				if (step == 4) _out_[3] = 255;
				_out_ += step;
			}
		}

		private static void stbi__setup_jpeg(Jpeg j)
		{
			j.idct_block_kernel = (o, stride, data) => { j.s.Token.ThrowIfCancellationRequested(); stbi__idct_block(o, stride, data, j.s.IdctScratch); };
			j.YCbCr_to_RGB_kernel = stbi__YCbCr_to_RGB_row;
			j.resample_row_hv_2_kernel = stbi__resample_row_hv_2;
		}

}
