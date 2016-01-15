using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing;

namespace png_to_xnb {
	class XCompress {
		public enum XMEMCODEC_TYPE {
			XMEMCODEC_DEFAULT = 0,
			XMEMCODEC_LZX = 1
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct XMEMCODEC_PARAMETERS_LZX {
			[FieldOffset(0)]
			public int Flags;
			[FieldOffset(4)]
			public int WindowSize;
			[FieldOffset(8)]
			public int CompressionPartitionSize;
		}

		[DllImport("xcompress32.dll", EntryPoint = "XMemCompress")]
		public static extern int XMemCompress(int Context,
		                                      byte[] pDestination, ref int pDestSize,
		                                      byte[] pSource, int pSrcSize);

		[DllImport("xcompress32.dll", EntryPoint = "XMemCreateCompressionContext")]
		public static extern int XMemCreateCompressionContext(
			XMEMCODEC_TYPE CodecType, ref XMEMCODEC_PARAMETERS_LZX pCodecParams,
			int Flags, ref int pContext);

		[DllImport("xcompress32.dll", EntryPoint = "XMemDestroyCompressionContext")]
		public static extern void XMemDestroyCompressionContext(int Context);

		[DllImport("xcompress32.dll", EntryPoint = "XMemDecompress")]
		public static extern int XMemDecompress(int Context,
		                                        byte[] pDestination, ref int pDestSize,
		                                        byte[] pSource, int pSrcSize);

		[DllImport("xcompress32.dll", EntryPoint = "XMemCreateDecompressionContext")]
		public static extern int XMemCreateDecompressionContext(
			XMEMCODEC_TYPE CodecType,
			ref XMEMCODEC_PARAMETERS_LZX pCodecParams,
			int Flags, ref int pContext);

		[DllImport("xcompress32.dll", EntryPoint = "XMemDestroyDecompressionContext")]
		public static extern void XMemDestroyDecompressionContext(int Context);

		public static byte[] Compress(byte[] decompressedData) {
			// Setup our compression context
			int compressionContext = 0;

			XMEMCODEC_PARAMETERS_LZX codecParams;
			codecParams.Flags = 0;
			codecParams.WindowSize = 64 * 1024;
			codecParams.CompressionPartitionSize = 256 * 1024;

			XMemCreateCompressionContext(
				         XMEMCODEC_TYPE.XMEMCODEC_LZX,
				         ref codecParams, 0, ref compressionContext);

			// Now lets compress
			int compressedLen = decompressedData.Length * 2;
			byte[] compressed = new byte[compressedLen];
			int decompressedLen = decompressedData.Length;
			XMemCompress(compressionContext,
				compressed, ref compressedLen,
				decompressedData, decompressedLen);
			// Go ahead and destory our context
			XMemDestroyCompressionContext(compressionContext);

			// Resize our compressed data
			Array.Resize<byte>(ref compressed, compressedLen);

			// Now lets return it
			return compressed;
		}

		public static byte[] Decompress(byte[] compressedData, byte[] decompressedData) {
			// Setup our decompression context
			int DecompressionContext = 0;

			XMEMCODEC_PARAMETERS_LZX codecParams;
			codecParams.Flags = 0;
			codecParams.WindowSize = 64 * 1024;
			codecParams.CompressionPartitionSize = 256 * 1024;

			XMemCreateDecompressionContext(
				         XMEMCODEC_TYPE.XMEMCODEC_LZX,
				         ref codecParams, 0, ref DecompressionContext);

			// Now lets decompress
			int compressedLen = compressedData.Length;
			int decompressedLen = decompressedData.Length;
			try {
				XMemDecompress(DecompressionContext,
					decompressedData, ref decompressedLen,
					compressedData, compressedLen);
			} catch {
			}
			// Go ahead and destory our context
			XMemDestroyDecompressionContext(DecompressionContext);
			// Return our decompressed bytes
			return decompressedData;
		}

		public static bool isItAvailable() {
			try {
				Compress(new byte[1]);
				return true;
			} catch (DllNotFoundException e) {
				if (e.Message.Contains("xcompress32.dll")) {
					return false;
				}
				throw e;
			}
		}
	}

	class BinaryWriterWrapper : IDisposable {
		private BinaryWriter bw;

		public BinaryWriterWrapper(BinaryWriter bw) {
			this.bw = bw;
		}

		public void WriteByte(byte b) {
			this.bw.Write(b);
		}

		public void WriteInt(int v) {
			this.bw.Write(v);
		}

		public void WriteChars(string s) {
			byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s);
			this.bw.Write(bytes);
		}

		public void Write7BitEncodedInt(int i) {
			while (i >= 0x80) {
				this.WriteByte((byte) (i & 0xff));
				i >>= 7;
			}
			this.WriteByte((byte) i);
		}

		public void WriteString(String s) {
			Write7BitEncodedInt(s.Length);
			WriteChars(s);
		}

		public void WriteColor(Color c) {
			this.WriteByte(c.R);
			this.WriteByte(c.G);
			this.WriteByte(c.B);
			this.WriteByte(c.A);
		}

		public void WriteByteArray(byte[] data) {
			this.bw.Write(data);
		}

		public void Close() {
			this.bw.Close();
		}

		public void Dispose() {
			Close();
		}
	}

	class MainClass {
		private static bool isFile(string path) {
			return File.Exists(path) && !isExistingDirectory(path);
		}

		private static bool isExistingDirectory(string path) {
			return Directory.Exists(path);
		}

		private static string TEXTURE_2D_TYPE = "Microsoft.Xna.Framework.Content.Texture2DReader, Microsoft.Xna.Framework.Graphics, Version=4.0.0.0, Culture=neutral, PublicKeyToken=842cf8be1de50553";
		private static int HEADER_SIZE = 3 + 1 + 1 + 1;
		private static int COMPRESSED_FILE_SIZE = 4;
		private static int TYPE_READER_COUNT_SIZE = 1;
		private static int TYPE_SIZE = 2 + TEXTURE_2D_TYPE.Length + 4;
		private static int SHARED_RESOURCE_COUNT_SIZE = 1;
		private static int OBJECT_HEADER_SIZE = 21;

		private static int METADATA_SIZE = HEADER_SIZE + COMPRESSED_FILE_SIZE + TYPE_READER_COUNT_SIZE + TYPE_SIZE + SHARED_RESOURCE_COUNT_SIZE + OBJECT_HEADER_SIZE;

		private static int imageSize(Bitmap png) {
			return 4 * png.Height * png.Width;
		}

		private static int compressedFileSize(Bitmap png) {
			return METADATA_SIZE + imageSize(png);
		}

		private static void writeCompressedData(BinaryWriterWrapper bw, Bitmap png) {
			using (MemoryStream stream = new MemoryStream()) {
				byte[] uncompressedData;
				using (BinaryWriterWrapper mw = new BinaryWriterWrapper(new BinaryWriter(stream))) {
					writeData(png, mw);
					uncompressedData = stream.ToArray();
				}
				byte[] compressedData = XCompress.Compress(uncompressedData);
				bw.WriteInt(6 + 4 + 4 + compressedData.Length); // compressed file size including headers
				bw.WriteInt(uncompressedData.Length); // uncompressed data size (exluding headers! only the data)
				bw.WriteByteArray(compressedData);
			}
		}

		private static void writeData(Bitmap png, BinaryWriterWrapper bw) {
			bw.Write7BitEncodedInt(1);       // type-reader-count
			bw.WriteString(TEXTURE_2D_TYPE); // type-reader-name
			bw.WriteInt(0);                  // reader version number
			bw.Write7BitEncodedInt(0);       // shared-resource-count
			// writing the image pixel data
			bw.WriteByte(1); // type id + 1 (referencing the TEXTURE_2D_TYPE)
			bw.WriteInt(0);  // surface format; 0=color
			bw.WriteInt(png.Width);
			bw.WriteInt(png.Height);
			bw.WriteInt(1); // mip count
			bw.WriteInt(imageSize(png)); // number of bytes in the image pixel data
			for (int y = 0; y < png.Height; y++) {
				for (int x = 0; x < png.Width; x++) {
					bw.WriteColor(png.GetPixel(x, y));
				}
			}
		}

		private static void pngToXnb(string pngFile, string xnbFile, bool compressed, bool reach) {
			Bitmap png = new Bitmap(pngFile);
			using (FileStream outFs = new FileStream(xnbFile, FileMode.Create, FileAccess.Write)) {
				using (BinaryWriterWrapper bw = new BinaryWriterWrapper(new BinaryWriter(outFs))) {
					bw.WriteChars("XNB"); // format-identifier
					bw.WriteChars("w");   // target-platform
					bw.WriteByte((byte) 5);  // xnb-format-version
					byte flagBits = 0;
					if (!reach) {
						flagBits |= 0x01;
					}
					if (compressed) {
						flagBits |= 0x80;
					}
					bw.WriteByte(flagBits); // flag-bits; 00=reach, 01=hiprofile, 80=compressed, 00=uncompressed
					if (compressed) {
						writeCompressedData(bw, png);
					} else {
						bw.WriteInt(compressedFileSize(png)); // compressed file size
						writeData(png, bw);
					}
				}
			}
		}

		private static void execute(string pngFile, string xnbFile, bool compressed, bool reach) {
			if (isFile(pngFile)) {
				if (!isExistingDirectory(xnbFile)) {
					pngToXnb(pngFile, xnbFile, compressed, reach);
				} else {
					Console.WriteLine(xnbFile+" is a directory.");
				}
			} else {
				Console.WriteLine(pngFile+" is not a file.");
			}
		}

		public static void Main(string[] args) {
			if (args.Length < 2) {
				Console.WriteLine("Usage: " + System.AppDomain.CurrentDomain.FriendlyName + " [-c -compressed] [-u -uncompressed] [-hidef] png_file xnb_file");
				Console.WriteLine("");
				Console.WriteLine("The program reads png_file and saves wraps it in an XNB structure and saves it");
				Console.WriteLine("as xnb_file.");
				Console.WriteLine("If xcompress32.dll is available than the XNB file will be compressed by default,");
				Console.WriteLine("use the -uncompressed switch if you always want to have uncompressed XNB file.");
				Console.WriteLine("If -compressed switch is used than you must have xcompress32.dll.");
				Console.WriteLine("XNB's can be either 'reach' or 'hidef'. Default is 'reach', use the -hidef");
				Console.WriteLine("option when necessary.");
				Environment.Exit(1);
			}
			bool compressionAvailable = XCompress.isItAvailable();
			bool compressed = compressionAvailable;
			bool reach = true;
			string pngFile = null;
			string xnbFile = null;
			for (int i = 0; i < args.Length; i++) {
				string v = args[i];
				if (v.Equals("-c") || v.Equals("-compressed")) {
					compressed = true;
				} else if (v.Equals("-d") || v.Equals("-decompressed")) {
					compressed = false;
				} else if (v.Equals("-hidef")) {
					reach = false;
				} else if (pngFile == null) {
					pngFile = v;
				} else if (xnbFile == null) {
					xnbFile = v;
				} else {
					Console.WriteLine("Invalid command line argument: " + v);
				}
			}
			if (!compressionAvailable && compressed) {
				Console.WriteLine("To write compressed XNB files, you must have 'xcompress32.dll' available.");
				Environment.Exit(2);
			}
			execute(pngFile, xnbFile, compressed, reach);
		}
	}
}
