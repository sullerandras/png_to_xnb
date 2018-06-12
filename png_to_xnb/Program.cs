using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

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
		private static bool keepRunning = true;
		private static SimpleGUI simpleGUI;

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

		private static void writeCompressedData(BinaryWriterWrapper bw, Bitmap png, bool premultiply) {
			using (MemoryStream stream = new MemoryStream()) {
				byte[] uncompressedData;
				using (BinaryWriterWrapper mw = new BinaryWriterWrapper(new BinaryWriter(stream))) {
					writeData(png, mw, premultiply);
					uncompressedData = stream.ToArray();
				}
				byte[] compressedData = XCompress.Compress(uncompressedData);
				bw.WriteInt(6 + 4 + 4 + compressedData.Length); // compressed file size including headers
				bw.WriteInt(uncompressedData.Length); // uncompressed data size (exluding headers! only the data)
				bw.WriteByteArray(compressedData);
			}
		}

		private static void writeData(Bitmap png, BinaryWriterWrapper bw, bool premultiply) {
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
			BitmapData bitmapData = png.LockBits(new Rectangle(0, 0, png.Width, png.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			try {
				var length = bitmapData.Stride * bitmapData.Height;
				byte[] bytes = new byte[length];
				Marshal.Copy(bitmapData.Scan0, bytes, 0, length);
				for (int i = 0; i < bytes.Length; i += 4) {
					// always swap red and blue channels
					// premultiply alpha if requested
					int a = bytes[i + 3];
					if (!premultiply || a == 255) {
						// No premultiply necessary
						byte b = bytes[i];
						bytes[i] = bytes[i + 2];
						bytes[i + 2] = b;
					} else if (a != 0) {
						byte b = bytes[i];
						bytes[i] = (byte)(bytes[i + 2] * a / 255);
						bytes[i + 1] = (byte)(bytes[i + 1] * a / 255);
						bytes[i + 2] = (byte)(b * a / 255);
					} else {
						// alpha is zero, so just zero everything
						bytes[i] = 0;
						bytes[i + 1] = 0;
						bytes[i + 2] = 0;
					}
				}
				bw.WriteByteArray(bytes);
			} finally {
				png.UnlockBits(bitmapData);
			}
		}

		private static int pngToXnb(string pngFile, string xnbFile, bool compressed, bool reach, bool premultiply) {
			infoMessage(Path.GetFileName(pngFile));
			using (Bitmap png = new Bitmap (pngFile)) {
				using (FileStream outFs = new FileStream (xnbFile, FileMode.Create, FileAccess.Write)) {
					using (BinaryWriterWrapper bw = new BinaryWriterWrapper (new BinaryWriter (outFs))) {
						bw.WriteChars ("XNB"); // format-identifier
						bw.WriteChars ("w");   // target-platform
						bw.WriteByte ((byte)5);  // xnb-format-version
						byte flagBits = 0;
						if (!reach) {
							flagBits |= 0x01;
						}
						if (compressed) {
							flagBits |= 0x80;
						}
						bw.WriteByte (flagBits); // flag-bits; 00=reach, 01=hiprofile, 80=compressed, 00=uncompressed
						if (compressed) {
							writeCompressedData (bw, png, premultiply);
						} else {
							bw.WriteInt (compressedFileSize (png)); // compressed file size
							writeData (png, bw, premultiply);
						}
						return 1;
					}
				}
			}
		}

		private static int pngToDirectory(string pngFile, string xnbDirectory, bool compressed, bool reach, bool premultiply) {
			string fileName = Path.GetFileNameWithoutExtension(pngFile);
			string xnbFile = Path.Combine(xnbDirectory, fileName + ".xnb");
			return pngToXnb(pngFile, xnbFile, compressed, reach, premultiply);
		}

		private static int pngsToDirectory(string pngDirectory, string xnbDirectory, bool compressed, bool reach, bool premultiply) {
			string[] files = Directory.GetFiles(pngDirectory, "*.png");
			int count = 0;
			foreach (string pngFile in files) {
				if (!keepRunning) {
					break;
				}
				count += pngToDirectory(pngFile, xnbDirectory, compressed, reach, premultiply);
			}
			return count;
		}

		private static void execute(string pngFileOrDir, string xnbFileOrDir, bool compressed, bool reach, bool premultiply) {
			if (!File.Exists(pngFileOrDir) && !Directory.Exists(pngFileOrDir)) {
				throw new ArgumentException("The png_file does not exist: "+pngFileOrDir);
			}
			int count;
			if (isFile(pngFileOrDir)) {
				if (isExistingDirectory(xnbFileOrDir)) {
					count = pngToDirectory(pngFileOrDir, xnbFileOrDir, compressed, reach, premultiply);
				} else {
					count = pngToXnb(pngFileOrDir, xnbFileOrDir, compressed, reach, premultiply);
				}
			} else {
				if (isExistingDirectory(xnbFileOrDir)) {
					count = pngsToDirectory(pngFileOrDir, xnbFileOrDir, compressed, reach, premultiply);
				} else {
					throw new ArgumentException("xnb_file parameter must be a directory when png_file is a directory.");
				}
			}
			infoMessage ("Converted " + count + " file"+(count != 1 ? "s" : "")+".");
		}

		public static void infoMessage(string message) {
			if (simpleGUI != null) {
				simpleGUI.statusBar.Text = message;
				Application.DoEvents();
			} else {
				Console.WriteLine (message);
			}
		}

		public static void errorMessage(string message) {
			if (simpleGUI != null) {
				simpleGUI.statusBar.Text = message;
			} else {
				Console.WriteLine (message);
			}
		}

		public class SimpleGUI : Form {
			TextBox textBoxInputFile;
			TextBox textBoxOutputFile;
			CheckBox checkBoxCompress;
			CheckBox checkBoxPremultiply;
			RadioButton radioReach;
			RadioButton radioHidef;
			public StatusBar statusBar;

			public SimpleGUI() {
				Text = "PNG to XNB";
				Size = new Size(640, 250);

				MainMenu mainMenu = new MainMenu();
				MenuItem file = mainMenu.MenuItems.Add("&File");
				file.MenuItems.Add(new MenuItem("E&xit",
					new EventHandler(this.OnExit), Shortcut.CtrlQ));

				Menu = mainMenu;

				ToolTip toolTip = new ToolTip();

				Label labelInputFile = new Label();
				labelInputFile.Parent = this;
				labelInputFile.Text = "Input file:";
				labelInputFile.Width = 70;
				labelInputFile.Location = new Point(10, 18);

				textBoxInputFile = new TextBox();
				textBoxInputFile.Parent = this;
				textBoxInputFile.Multiline = false;
				textBoxInputFile.Location = new Point(80, 15);
				textBoxInputFile.Width = 400;
				textBoxInputFile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

				Button buttonInputFile = new Button();
				buttonInputFile.Parent = this;
				buttonInputFile.Text = "File...";
				buttonInputFile.Width = 60;
				buttonInputFile.Location = new Point(490, 15);
				buttonInputFile.Click += new EventHandler(onChooseInputFile);
				buttonInputFile.Anchor = AnchorStyles.Top | AnchorStyles.Right;

				Button buttonInputFolder = new Button();
				buttonInputFolder.Parent = this;
				buttonInputFolder.Text = "Folder...";
				buttonInputFolder.Width = 60;
				buttonInputFolder.Location = new Point(560, 15);
				buttonInputFolder.Click += new EventHandler(onChooseInputFolder);
				buttonInputFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;

				Label labelOutputFile = new Label();
				labelOutputFile.Parent = this;
				labelOutputFile.Text = "Output file:";
				labelOutputFile.Width = 70;
				labelOutputFile.Location = new Point(10, 53);

				textBoxOutputFile = new TextBox();
				textBoxOutputFile.Parent = this;
				textBoxOutputFile.Multiline = false;
				textBoxOutputFile.Location = new Point(80, 50);
				textBoxOutputFile.Width = 400;
				textBoxOutputFile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

				Button buttonOutputFile = new Button();
				buttonOutputFile.Parent = this;
				buttonOutputFile.Text = "File...";
				buttonOutputFile.Width = 60;
				buttonOutputFile.Location = new Point(490, 50);
				buttonOutputFile.Click += new EventHandler(onChooseOutputFile);
				buttonOutputFile.Anchor = AnchorStyles.Top | AnchorStyles.Right;

				Button buttonOutputFolder = new Button();
				buttonOutputFolder.Parent = this;
				buttonOutputFolder.Text = "Folder...";
				buttonOutputFolder.Width = 60;
				buttonOutputFolder.Location = new Point(560, 50);
				buttonOutputFolder.Click += new EventHandler(onChooseOutputFolder);
				buttonOutputFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;

				checkBoxCompress = new CheckBox();
				checkBoxCompress.Parent = this;
				checkBoxCompress.Width = 275;
				checkBoxCompress.Location = new Point(10, 80);
				checkBoxCompress.Enabled = XCompress.isItAvailable();
				checkBoxCompress.Checked = checkBoxCompress.Enabled;
				if (checkBoxCompress.Enabled) {
					checkBoxCompress.Text = "Compress XNB file";
				} else {
					checkBoxCompress.Text = "Compress XNB file (xcompress32.dll not found)";
				}

				checkBoxPremultiply = new CheckBox();
				checkBoxPremultiply.Parent = this;
				checkBoxPremultiply.Text = "Premultiply alpha";
				checkBoxPremultiply.Checked = true;
				checkBoxPremultiply.Location = new Point(285, 80);
				checkBoxPremultiply.Width = 200;
				toolTip.SetToolTip(checkBoxPremultiply, "RGB channels are multiplied by the alpha channel");

				radioReach = new RadioButton();
				radioReach.Parent = this;
				radioReach.Text = "reach";
				radioReach.Width = 60;
				radioReach.Location = new Point(10, 110);
				radioReach.Checked = true;

				radioHidef = new RadioButton();
				radioHidef.Parent = this;
				radioHidef.Text = "hidef";
				radioHidef.Width = 60;
				radioHidef.Location = new Point(80, 110);

				Button buttonConvert = new Button();
				buttonConvert.Parent = this;
				buttonConvert.Text = "Convert";
				buttonConvert.Location = new Point(10, 140);
				buttonConvert.Click += new EventHandler(onConvert);

				statusBar = new StatusBar();
				statusBar.Parent = this;
				statusBar.Text = "Ready";

				CenterToScreen();
			}

			void onConvert(object sender, EventArgs e) {
				bool compressed = checkBoxCompress.Checked;
				bool reach = radioReach.Checked;
				bool premultiply = checkBoxPremultiply.Checked;
				string pngFile = textBoxInputFile.Text;
				string xnbFile = textBoxOutputFile.Text;
				if (pngFile.Length == 0) {
					errorMessage ("Please select an input file or folder");
				} else if (xnbFile.Length == 0) {
					errorMessage ("Please select an output file or folder");
				} else if (!isFile (pngFile) && !isExistingDirectory (pngFile)) {
					errorMessage ("Input file or folder does not exists: " + pngFile);
				} else {
					execute (pngFile, xnbFile, compressed, reach, premultiply);
				}
			}

			void OnExit(object sender, EventArgs e) {
				Close();
			}

			void onChooseInputFile(object sender, EventArgs e) {
				OpenFileDialog openFileDialog1 = new OpenFileDialog();

				openFileDialog1.InitialDirectory = ".";
				openFileDialog1.Filter = "PNG files (*.png)|*.png|GIF files (*.gif)|*.gif|All files (*.*)|*.*";
				openFileDialog1.FilterIndex = 1;
				openFileDialog1.RestoreDirectory = true;

				if(openFileDialog1.ShowDialog() == DialogResult.OK)
				{
					textBoxInputFile.Text = openFileDialog1.FileName;
				}
			}

			void onChooseInputFolder(object sender, EventArgs e) {
				FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();

				folderBrowserDialog1.Description = "Select the folder with the PNG files you want to convert.";
				folderBrowserDialog1.SelectedPath = Path.GetFullPath (".");
				folderBrowserDialog1.ShowNewFolderButton = false;

				if(folderBrowserDialog1.ShowDialog() == DialogResult.OK)
				{
					textBoxInputFile.Text = folderBrowserDialog1.SelectedPath;
				}
			}

			void onChooseOutputFile(object sender, EventArgs e) {
				SaveFileDialog openFileDialog1 = new SaveFileDialog();

				openFileDialog1.InitialDirectory = ".";
				openFileDialog1.Filter = "XNB files (*.xnb)|*.xnb|All files (*.*)|*.*";
				openFileDialog1.FilterIndex = 1;
				openFileDialog1.RestoreDirectory = true;

				if(openFileDialog1.ShowDialog() == DialogResult.OK)
				{
					textBoxOutputFile.Text = openFileDialog1.FileName;
				}
			}

			void onChooseOutputFolder(object sender, EventArgs e) {
				FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();

				folderBrowserDialog1.Description = "Select the folder where you want to save the XNB files.";
				folderBrowserDialog1.SelectedPath = Path.GetFullPath (".");
				folderBrowserDialog1.ShowNewFolderButton = true;

				if(folderBrowserDialog1.ShowDialog() == DialogResult.OK)
				{
					textBoxOutputFile.Text = folderBrowserDialog1.SelectedPath;
				}
			}
		}

		private static void openGUI() {
			simpleGUI = new SimpleGUI ();
			Application.Run (simpleGUI);
		}

		[STAThreadAttribute]
		public static void Main(string[] args) {
			Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) {
				e.Cancel = true;
				MainClass.keepRunning = false;
			};
			if (args.Length == 0) {
				openGUI ();
				Environment.Exit (0);
			}
			if (args.Length == 1 && (args[0] == "-h" || args[0] == "--help" || args[0] == "/?" || args[0] == "/h")) {
				Console.WriteLine("Save images as XNB.");
				Console.WriteLine("Usage: " + System.AppDomain.CurrentDomain.FriendlyName + " [-h|--help] [-c] [-u] [-hidef] [-nopre] png_file [xnb_file]");
				Console.WriteLine("");
				Console.WriteLine("The program reads the image 'png_file' and saves as an XNB file as 'xnb_file'.");
				Console.WriteLine("Start without any input parameters to launch a GUI.");
				Console.WriteLine("");
				Console.WriteLine("Options:");
				Console.WriteLine("  -h      Prints this help.");
				Console.WriteLine("  -c      Compress the XNB file. This is the default if xcompress32.dll is");
				Console.WriteLine("          available. Note that the compression might take significant time, but");
				Console.WriteLine("          of course the result XNB file will be much smaller.");
				Console.WriteLine("  -u      Save uncompressed XNB file, even if xcompress32.dll is available.");
				Console.WriteLine("  -hidef  XNB's can be either 'reach' or 'hidef'. Default is 'reach', so use");
				Console.WriteLine("          this -hidef option when necessary. I don't know what 'reach' or");
				Console.WriteLine("          'hidef' means, but for example Terraria cannot load 'hidef' XNB files.");
				Console.WriteLine("  -nopre  RGB channels will not be premultiplied by the alpha. By default, XNB's");
				Console.WriteLine("          use premultiplied alpha.");
				Console.WriteLine("");
				Console.WriteLine("png_file  This can either be a file or a directory. If this is a directory");
				Console.WriteLine("          then it will convert all *.png files in the directory (not recursive).");
				Console.WriteLine("xnb_file  This can also be a file or a directory. If this is a directory then");
				Console.WriteLine("          the filename will be name.xnb if the image file was name.png");
				Console.WriteLine("          If this is omitted then it converts the png_file into the same folder.");
				Environment.Exit(1);
			}
			bool compressionAvailable = XCompress.isItAvailable();
			bool compressed = compressionAvailable;
			bool reach = true;
			bool premultiply = true;
			string pngFile = null;
			string xnbFile = null;
			for (int i = 0; i < args.Length; i++) {
				string v = args[i];
				if (v.Equals("-c")) {
					compressed = true;
				} else if (v.Equals("-u")) {
					compressed = false;
				} else if (v.Equals("-hidef")) {
					reach = false;
				} else if (v.Equals("-nopre")) {
					premultiply = false;
				} else if (pngFile == null) {
					pngFile = v;
				} else if (xnbFile == null) {
					xnbFile = v;
				} else {
					Console.WriteLine("Invalid command line argument: " + v);
					Environment.Exit(3);
				}
			}
			if (pngFile != null && xnbFile == null && isFile(pngFile)) {
				xnbFile = Path.ChangeExtension (pngFile, ".xnb");
			}
			if (!compressionAvailable && compressed) {
				Console.WriteLine("To write compressed XNB files, you must have 'xcompress32.dll' available.");
				Environment.Exit(2);
			}
			try {
				execute(pngFile, xnbFile, compressed, reach, premultiply);
			} catch (Exception ex) {
				Console.WriteLine(ex);
			}
		}
	}
}
