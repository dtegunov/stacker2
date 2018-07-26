# In a nutshell
Although FEI's EPU can write out whole gain-uncorrected movies now, the compression options are still limited to bit-packed 2 or 4 bit/px MRCs. This tool can be run in the background during acquisition, and will read newly acquired movies, save them as LZW-compressed TIFFs at the specified location or just move them, and (optionally) delete the originals.


## You will need
- Windows PC (**Gatan's K2 computer will work!**) with [.NET Framework 4.6](https://www.microsoft.com/en-us/download/details.aspx?id=48130) installed.


## Usage

- Download everything from the bin directory and put it into a local folder.
- Run stacker2.exe.
- Enter the path to the folder containing the individual frames, e. g. Z:\EPU\micrographs.
- Specify whether the tool should search all sub-folders recursively for new movies.
- Specify whether the movies should be compressed at all, or just moved to the specified location.
- In case you just want to move files without compressing them, specify the input file extension (for compression, the input is always *.mrc)
- Specify the number of frames per movie, e. g. 40. This ensures only files with the correct number of frames are processed.
- Specify whether the original files should be deleted, or moved to a subfolder named 'original'.
- Specify whether the gain reference files saved by newer EPU versions for each movie should be deleted automatically.
- That's it! The tool will now run until you manually close it. While active, it will monitor the folder for new movies, and save them as compressed TIFF files.

- **IMPORTANT #1**: If specified, the original files will be deleted once the corresponding output file has been saved successfully.
- **IMPORTANT #2**: Compressing data acquired in integrating mode (e. g. Falcon II/III) won't provide significant gains and most likely isn't worth the overhead.
- **IMPORTANT #3**: Reading bit-packed MRCs (i. e. 2 or 4 bit/px) isn't supported!


## Authorship

Stacker2 is being developed by Dimitry Tegunov ([tegunov@gmail.com](mailto:tegunov@gmail.com)), currently in Patrick Cramer's lab at the Max Planck Institute for Biophysical Chemistry in Göttingen, Germany.