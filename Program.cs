using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Text;

//Copyright (c) 2017 Anna Eerkes All rights reserved;
//License (CC BY-SA 4.0)
//compile with: /unsafe

namespace RMAOECompiler
{
	class Program
	{

		public static Settings compileSettings = new Settings();

		public static int maxResolution
		{
			get
			{
				return compileSettings.resolution;
			}
		}

		public enum EPostFix
		{
			_R,
			_M,
			_AO,
			_E
		}

		public enum ETextureType
		{
			Roughness,
			Metallicity,
			AmbientOcclusion,
			Emissive
		}

		public static int threads = Environment.ProcessorCount;
		public static List<Bitmap> tempResult = new List<Bitmap>();

		//we have this set here so we can generate this image once and just re-use it.
		private static Bitmap _defaultColour = null;
		public static Bitmap defaultColour
		{
			get
			{
				if (_defaultColour == null)
				{
					Bitmap b = new Bitmap(maxResolution, maxResolution);
					using (Graphics graph = Graphics.FromImage(b))
					{
						int AlphaColor = 255 - compileSettings.emissiveDefaultValue;
						if (!compileSettings.invertAlpha)
						{
							AlphaColor = compileSettings.emissiveDefaultValue;
						}
						SolidBrush brush = new SolidBrush(Color.FromArgb(AlphaColor, compileSettings.roughnessDefaultValue,
							compileSettings.metallicityDefaultValue, compileSettings.ambientOcclusionDefaultValue));

						graph.FillRectangle(brush, 0, 0, maxResolution, maxResolution);
					}
					_defaultColour = b;
				}

				return _defaultColour;
			}
		}

		static void Main(string[] args)
		{
			CopyRight();
			initialize();

			//we set a default resolution; can be overwritten with a commandprompt
			if (args.Length != 0)
			{
				int num = 0;
				Int32.TryParse(args[0], out num);
				compileSettings.resolution = maxResolution;
			}

			List <string> allFiles = new List<string>();

			//We gather all textures, we can have textures where we don't have certain values; those default to black.
			foreach (ETextureType textureType in Enum.GetValues(typeof(ETextureType)))
			{
				allFiles = Index(textureType, allFiles);
			}

			//once we get the name of all the textures; actually compile them.
			foreach (string fileName in allFiles)
			{
				CreateTexture(fileName);
				//we run a garbage collect after each file has been created
				System.GC.Collect();
			}

			//Keep the console open until ENTER has been pressed.
			Console.WriteLine("");
			Console.WriteLine("==============================================================");
			Console.WriteLine("                    Press ENTER to exit...                    ");
			Console.ReadLine();
		}

		//Gather the clean names of the textures
		static string CleanName(string dirtyName)
		{
			string cleanName = dirtyName.Replace(".png", "");
			cleanName = cleanName.Replace("\\", "");
			//we replace all the different postfixes
			foreach (EPostFix postFix in Enum.GetValues(typeof(EPostFix)))
			{
				cleanName = cleanName.Replace(postFix.ToString(), "");
			}
			return cleanName;
		}

		//Create a list of all the different textures
		static List<string> Index(ETextureType type, List<string> allFiles)
		{
			string typeName = type.ToString();
			string[] fileNames = Directory.GetFiles(typeName);
			foreach (string file in fileNames)
			{
				string fn = CleanName(file).Replace(typeName,"");
				if (!allFiles.Contains(fn))
				{
					allFiles.Add(fn);
				}
			}
			return allFiles;
		}

		//Create and save the texture
		static void CreateTexture(string fileName)
		{
			Console.WriteLine(string.Format("Compiling texture: {0}",fileName));

			string filePath = null;
			Bitmap[] image = new Bitmap[4];
			Bitmap endResult = new Bitmap(maxResolution, maxResolution);

			//this can be hardcoded; because we only have 4 channels anyways;
			for (int i = 0; i < 4; i++)
			{
				filePath = string.Format("{0}/{1}{2}.png", Enum.GetNames(typeof(ETextureType))[i], fileName, Enum.GetNames(typeof(EPostFix))[i]);
				if (File.Exists(filePath))
				{
					Bitmap thisBitmap = (Bitmap)Image.FromFile(filePath);
					if (thisBitmap.Height != maxResolution || thisBitmap.Width != maxResolution)
					{
						//Here we resize the image;
						thisBitmap = new Bitmap(thisBitmap, maxResolution, maxResolution);
					}
					image[i] = thisBitmap;
				}
				else 
				{
					//We set the texture to white, so we have control over it in-engine;
					image[i] = defaultColour;
				}	
			}
			
			//Actually compile the image
			endResult = compileImage(image);

			//now save:
			string fullFilePath = string.Format("RMAOE/{0}_RMAOE.png", fileName);

			if (File.Exists(fullFilePath))
			{
				File.Delete(fullFilePath);
			}
			endResult.Save(fullFilePath, ImageFormat.Png);
		}

		static Bitmap compileImage(Bitmap[] image)
		{
			//initialize and cache the final image
			Bitmap result = new Bitmap(maxResolution, maxResolution);
			BitmapData resultBmData = result.LockBits(new Rectangle(0, 0, maxResolution, maxResolution), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

			int bytesPerPixel = Bitmap.GetPixelFormatSize(image[0].PixelFormat) / 8;
			int widthInBytes = maxResolution * bytesPerPixel;

			BitmapData[] imageData = new BitmapData[4];

			for (int i = 0; i < 4; i++)
			{
				imageData[i] = image[i].LockBits(new Rectangle(0, 0, maxResolution, maxResolution), ImageLockMode.ReadOnly, image[i].PixelFormat);
				image[i].UnlockBits(imageData[i]);
			}

			//since we're using pointers we have to use unsafe.
			unsafe
			{
				//since we're using a variety of different textures, we need pointers to them all.
				byte* ptrFirstPixel = (byte*)resultBmData.Scan0;
				byte* ptrRedPixel	= (byte*)imageData[0].Scan0;	//Roughness
				byte* ptrGreenPixel = (byte*)imageData[1].Scan0;	//Metallicity
				byte* ptrBluePixel	= (byte*)imageData[2].Scan0;   //AO
				byte* ptrAlphaPixel = (byte*)imageData[3].Scan0;   //Emissive;

				Parallel.For(0, maxResolution, y =>
				 {
					 byte* currentRedLine	= ptrRedPixel + (y * imageData[0].Stride);
					 byte* currentGreenLine	= ptrGreenPixel + (y * imageData[1].Stride);
					 byte* currentBlueLine	= ptrBluePixel + (y * imageData[2].Stride);
					 byte* currentAlphaLine = ptrAlphaPixel + (y * imageData[3].Stride);
					 byte* currentLine		= ptrFirstPixel + (y * resultBmData.Stride);

					 for (int x= 0; x < widthInBytes; x = x + bytesPerPixel)
					 {
						 currentLine[x]		= currentBlueLine[x + 2];
						 currentLine[x + 1] = currentGreenLine[x + 2];
						 currentLine[x + 2] = currentRedLine[x + 2];
						 currentLine[x + 3] = currentAlphaLine[x + 2];
					 }
				 });

				result.UnlockBits(resultBmData);
			}
			return result;
		}

		static void initialize()
		{
			//Create a directory to put used files into
			if (!Directory.Exists("RMAOE"))
			{
				Directory.CreateDirectory("RMAOE");
			}

			foreach (ETextureType type in Enum.GetValues(typeof(ETextureType)))
			{
				string typeName = type.ToString();
				if (!Directory.Exists(typeName))
				{
					System.Console.WriteLine("Creating folder: " + typeName);
					Directory.CreateDirectory(typeName);
				}
			}

			//load settings
			if (File.Exists("settings.json"))
			{
				StreamReader sr = new StreamReader("settings.json");
				string jsonString = sr.ReadToEnd();
				JavaScriptSerializer ser = new JavaScriptSerializer();
				compileSettings = ser.Deserialize<Settings>(jsonString);
			}
			else
			{
				//if we don't have settings, create settings
				JavaScriptSerializer ser = new JavaScriptSerializer();
				string json = FormatOutput(ser.Serialize(compileSettings));
				File.WriteAllText("settings.json", json);
			}
		}

		static void CopyRight()
		{
			Console.WriteLine("             RMAOE Compiler (c) 2017 Anna Eerkes              ");
			Console.WriteLine("                    License (CC BY-SA 4.0)                    ");
			Console.WriteLine("==============================================================");
			Console.WriteLine("=== This merges a variety of black/white textures into one ===");
			Console.WriteLine("===    To be used with PBR engines like Unreal or Unity    ===");
			Console.WriteLine("===               Red Channel - Roughness (R)              ===");
			Console.WriteLine("===             Green Channel - Metallicity (M)            ===");
			Console.WriteLine("===              Blue Channel - Ambient Occlusion (AO)     ===");
			Console.WriteLine("===             Alpha Channel - INVERSE Emissive (E)       ===");
			Console.WriteLine("==============================================================");
			Console.WriteLine("");
		}

		//Pretty up the json to make it actually useable.
		//code from https://stackoverflow.com/a/23828858 
		public static string FormatOutput(string jsonString)
		{
			var stringBuilder = new StringBuilder();

			bool escaping = false;
			bool inQuotes = false;
			int indentation = 0;

			foreach (char character in jsonString)
			{
				if (escaping)
				{
					escaping = false;
					stringBuilder.Append(character);
				}
				else
				{
					if (character == '\\')
					{
						escaping = true;
						stringBuilder.Append(character);
					}
					else if (character == '\"')
					{
						inQuotes = !inQuotes;
						stringBuilder.Append(character);
					}
					else if (!inQuotes)
					{
						if (character == ',')
						{
							stringBuilder.Append(character);
							stringBuilder.Append("\r\n");
							stringBuilder.Append('\t', indentation);
						}
						else if (character == '[' || character == '{')
						{
							stringBuilder.Append(character);
							stringBuilder.Append("\r\n");
							stringBuilder.Append('\t', ++indentation);
						}
						else if (character == ']' || character == '}')
						{
							stringBuilder.Append("\r\n");
							stringBuilder.Append(' ', --indentation);
							stringBuilder.Append(character);
						}
						else if (character == ':')
						{
							stringBuilder.Append(character);
							stringBuilder.Append(' ');
						}
						else
						{
							stringBuilder.Append(character);
						}
					}
					else
					{
						stringBuilder.Append(character);
					}
				}
			}

			return stringBuilder.ToString();
		}
	}

	public class Settings
	{
		private int _resolution = 1024;
		public int resolution
		{
			get
			{
				return _resolution;
			}
			set
			{
				if (value <= 0 || value > 4096 || (value & (value - 1)) != 0)
				{
					//just log some of the exceptions
					if (value <= 0)
					{
						Console.WriteLine("Resolution needs to be bigger than 0.");
					}
					else if (value > 8192)
					{
						Console.WriteLine("Requested resolution is too high; this was written in 2017, 8k textures were pretty much the highest we could go!");
					}
					else if ((value & (value - 1)) != 0)
					{
						Console.WriteLine("Need to use a power of 2-resolution. This is much better for game engines.");
					}
				}
				else
				{
					_resolution = value;
					Console.WriteLine(string.Format("Using requested resolution: {0}", value));
				}
			}
		}
		public bool invertAlpha = true;
		private int _roughnessDefaultValue = 255;
		public int roughnessDefaultValue
		{
			get
			{
				return _roughnessDefaultValue;
			}
			set
			{
				if (value < 256 && value >= 0)
				{
					_roughnessDefaultValue = value;
				}
			}
		}
		private int _metallicityDefaultValue = 255;
		public int metallicityDefaultValue
		{
			get
			{
				return _metallicityDefaultValue;
			}
			set
			{
				if (value< 256 && value >= 0)
				{
					_metallicityDefaultValue = value;
				}
			}
		}
		private int _ambientOcclusionDefaultValue = 255;
		public int ambientOcclusionDefaultValue
		{
			get
			{
				return _ambientOcclusionDefaultValue;
			}
			set
			{
				if (value < 256 && value >= 0)
				{
					_ambientOcclusionDefaultValue = value;
				}
			}
		}
		private int _emissiveDefaultValue = 0;
		public int emissiveDefaultValue
		{
			get
			{
				return _emissiveDefaultValue;
			}
			set
			{
				if (value < 256 && value >= 0)
				{
					_emissiveDefaultValue = value;
				}
			}
		}
	}
}
