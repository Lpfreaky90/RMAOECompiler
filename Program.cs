using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

//Copyright (c) 2017 Anna Eerkes All rights reserved;
//License (CC BY-SA 4.0)

namespace RMAOECompiler
{
	class Program
	{
		public static int maxResolution = 1024;

		static void Main(string[] args)
		{
			Console.WriteLine("             RMAOE Compiler (c) 2017 Anna Eerkes              ");
			Console.WriteLine("                    License (CC BY-SA 4.0)                    ");
			Console.WriteLine("==============================================================");
			Console.WriteLine("=== This merges a variety of black/whitet extures into one ===");
			Console.WriteLine("===    To be used with PBR engines like Unreal or Unity    ===");
			Console.WriteLine("===               Red Channel - Roughness (R)              ===");
			Console.WriteLine("===             Green Channel - Metallicity (M)            ===");
			Console.WriteLine("===              Blue Channel - Ambient Occlusion (AO)     ===");
			Console.WriteLine("===             Alpha Channel - INVERSE Emissive (E)       ===");
			Console.WriteLine("==============================================================");
			Console.WriteLine("");

			//we set a default resolution; can be overwritten with a commandprompt
			if (args.Length != 0)
			{
				int num = 0;
				Int32.TryParse(args[0], out num);
				if (num > 0 && (num & (num - 1)) == 0 && num < 5000)
				{
					maxResolution = num;
					Console.WriteLine(string.Format("Using requested resolution: {0}", num));
				}
				else
				{
					//just log some of the exceptions
					if (num > 4096)
					{
						Console.WriteLine("Requested resolution is too high; this was written in 2017, 4k textures were pretty much the highest we could go!");
					}

					if ((num & (num - 1)) != 0)
					{
						Console.WriteLine("Need to use a power of 2-resolution. This is much better for game engines.");
					}

					if (num < 0)
					{
						Console.WriteLine("Resolution needs to be bigger than 0.");
					}
				}
			}

			if (!Directory.Exists("RMAOE"))
			{
				Directory.CreateDirectory("RMAOE");
			}

			List <string> allFiles = new List<string>();

			foreach (ETextureType textureType in Enum.GetValues(typeof(ETextureType)))
			{
				allFiles = Index(textureType, allFiles);
			}

			foreach (string fileName in allFiles)
			{
				CreateTexture(fileName);
			}

			Console.WriteLine("");
			Console.WriteLine("==============================================================");
			Console.WriteLine("                    Press ENTER to exit...                    ");
			Console.ReadLine();
		}

		public enum EFileExtension
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

		static string CleanName(string dirtyName)
		{
			//clean up names; like _M _R etc.
			string cleanName = dirtyName.Replace(".png", "");
			cleanName = cleanName.Replace("\\", "");
			foreach (EFileExtension fe in Enum.GetValues(typeof(EFileExtension)))
			{
				cleanName = cleanName.Replace(fe.ToString(), "");
			}
			return cleanName;
		}

		static List<string> Index(ETextureType type, List<string> allFiles)
		{
			string typeName = type.ToString();

			if (!Directory.Exists(typeName))
			{
				System.Console.WriteLine("Creating folder: " + typeName);
				Directory.CreateDirectory(typeName);
			}

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

		static void CreateTexture(string fileName)
		{
			Console.WriteLine(string.Format("Compiling texture: {0}",fileName));

			string realFileName = null;
			Bitmap[] image = new Bitmap[4];
			Bitmap endResult = new Bitmap(maxResolution, maxResolution);
			for (int i = 0; i < Enum.GetNames(typeof(ETextureType)).Length; i++)
			{
				realFileName = string.Format("{0}/{1}{2}.png", Enum.GetNames(typeof(ETextureType))[i], fileName, Enum.GetNames(typeof(EFileExtension))[i]);
				if (File.Exists(realFileName))
				{
					Bitmap thisBitmap = (Bitmap)Image.FromFile(realFileName);
					if (thisBitmap.Height != maxResolution || thisBitmap.Width != maxResolution)
					{
						//Here we resize the images;
						Bitmap resizedBitMap = new Bitmap(thisBitmap, maxResolution, maxResolution);
						thisBitmap = resizedBitMap;
					}
					image[i] = thisBitmap;
				}
				else 
				{
					image[i] = null;
				}
			}

			for (int x=0; x < maxResolution; x++)
			{
				for (int y=0; y < maxResolution; y++)
				{
					Color pixelColour = Color.FromArgb(0, 0, 0, 0);
					//COLOUR IS ARGB FORMAT, SO ALPHA COMES FIRST...
					for (int i = 0; i < 3; i++)
					{
						if (image[i] != null)
						{
							if (i == 0)
							{
								//set r-value of the pixel
								pixelColour = Color.FromArgb(endResult.GetPixel(x, y).A, image[0].GetPixel(x, y).R, endResult.GetPixel(x, y).G, endResult.GetPixel(x, y).B);
							}
							else if (i == 1)
							{
								//set g-value of the pixel
								pixelColour = Color.FromArgb(endResult.GetPixel(x, y).A, endResult.GetPixel(x, y).R, image[1].GetPixel(x, y).R, endResult.GetPixel(x, y).B);
							}
							else if (i == 2)
							{
								//set b-value of the pixel
								pixelColour = Color.FromArgb(endResult.GetPixel(x, y).A, endResult.GetPixel(x, y).R, endResult.GetPixel(x, y).G, image[2].GetPixel(x, y).R);
							}
							else if (i == 3)
							{
								//set a-value of the pixel
								pixelColour = Color.FromArgb(image[3].GetPixel(x, y).R, endResult.GetPixel(x, y).R, endResult.GetPixel(x, y).G, endResult.GetPixel(x, y).B);
							}
							endResult.SetPixel(x, y, pixelColour);
						}
					}
					//we do a flip for the alpha channel; this makes them much easier to see in explorer etc;
					//do another small flip of this in your shader/material;
					pixelColour = Color.FromArgb(255 - pixelColour.A, pixelColour.R, pixelColour.G, pixelColour.B);
					endResult.SetPixel(x, y, pixelColour);
				}
			}
			//we should be done now; let's try and save this thing.
			string fullFilePath = string.Format("RMAOE/{0}_RMAOE.png", fileName);
			if (File.Exists(fullFilePath))
			{
				File.Delete(fullFilePath);
			}
			endResult.Save(fullFilePath, ImageFormat.Png);
		}
	}
}
