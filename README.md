# In a nutshell
FEI's EPU still can't write out K2 movies as compressed TIFFs, or even just MRC stacks. This tool can be run in the background during acquisition, and will combine incoming individual frames into stacks, optionally divide them by the already applied gain reference to go back to nicely compressible integers, and write them out as TIFFs with LZW compression.


## You will need
- Windows PC with [.NET Framework 4.6](https://www.microsoft.com/en-us/download/details.aspx?id=48130) installed.


## Usage

- Download everything from the bin directory and put it into a local folder.
- Run stacker.exe
- Specify the number of frames per movie, e. g. 40.
- Enter the path to the folder containing the individual frames, e. g. Z:\EPU\micrographs
- Optionally, enter the path to a gain reference saved as MRC, e. g. Z:\EPU\gain.mrc. This needs to be the same reference EPU is applying to the individual frames.
- The tool can automatically check if dividing by the provided gain reference results in the original integer values. Turn this check on or off by entering (y)es or (n)o.
- Under rare circumstances, the gain reference needs to be transposed or flipped. The next three options allow you to specify that.
- That's it! The tool will now run until you manually close it. While active, it will monitor the folder for new files that match EPU's naming pattern, process and combine them, and write out a compressed TIFF file for each movie.
- IMPORTANT #1: The original MRC files will be deleted once the corresponding TIFF file has been saved successfully.
- IMPORTANT #2: Remember that the gain reference is not pre-applied to your compressed movies anymore.


## Authorship

Stacker is being developed by Dimitry Tegunov ([tegunov@gmail.com](mailto:tegunov@gmail.com)), currently in Patrick Cramer's lab at the Max Planck Institute for Biophysical Chemistry in Göttingen, Germany.