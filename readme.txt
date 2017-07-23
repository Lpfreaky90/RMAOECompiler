				RMAOE Compiler (c) 2017 Anna Eerkes
                    License (CC BY-SA 4.0)                    

					
This compiler has been written in C# to compress multiple maps into one texture.

How to use:
	1. Place the executable in a folder;
	2. Run the executable;
	3. The executable will automatically generate folders for you.
	4. Place your textures in the appropriate folders.
	5. Re-run the executable, and it will generate the files for you in the RMAOE sub-folder.

Naming conventions:
	Give all your textures the same name, but add a post-fix for each:
		For the Roughness textures: add _R
		For the Metal textures: add _M;
		For the Ambient Occlusion textures: add _AO
		For the Emissive textures: add _E
	
How to change resolution:
	Create a shortcut, and add your favorite resolution to it.
	IE: RMAOECompiler.exe 128
	This can be done from the command line as well.
	
	The example will compile the textures down to 128x128.
	
	Notes:
		Resolution should be a power of 2.
		Resolutions should be > 0
		Resolutions should be 4096 max
		
		If any of these checks fails, it falls back to 1024.