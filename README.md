PNG to XNB
==========

Command line program to convert PNG files to [XNB format](http://xbox.create.msdn.com/en-US/sample/xnb_format).
Supports batch conversion.

Motivation
----------

When I created my Terraria HD texture pack, the new textures were around 800 MB in total. Not too much, but compared to the original 14 MB, it was a huge difference. So that's why I created this little converter program. I should have googled first, because there are similar applications like this for example: [XNB Builder](http://sourceforge.net/projects/xnbbuilder/) which support many different file formats, not just PNG images.

Of course when I used it only myself it wasn't very well polished. But than I thought it might be beneficial for some people on the internet who are looking for a minimalistic PNG to XNB converter.

Requirements
------------

The program requires .NET 3.0. It should work with newer .NET versions as well. Windows 7 comes with preinstalled .NET 3.5 so the
program should run without installing any additional software.

I used Xamarin Studio / Mono for development, which is an open source .NET implementation.

The program runs on Windows and on Mac / Linux by using `wine` or `mono`. I use it on a Mac, but also tested on Windows 7.

On Ubuntu Linux you need to install a package which contains the `System.Windows.Forms.dll` file, for example: `sudo apt-get install libmono-system-windows-forms4.0-cil`. You can find packages in the [package repository](http://packages.ubuntu.com/).

Examples
--------

1) Start a command prompt (cmd.exe)
2) Change to the folder where you have the png_to_xnb.exe file. Most likely this is in your Downloads folder:

    cd %USERPROFILE%\Downloads

3) Convert Item_1.png to XNB:

    png_to_xnb.exe Item_1.png Item_1.xnb

On Mac / Linux with `wine` (you probably need to install .NET in wine for example: `winetricks dotnet45`):

    wine /path/to/png_to_xnb.exe Item_1.png Item_1.xnb

On Mac / Linux with `mono` installed:

    mono /path/to/png_to_xnb.exe Item_1.png Item_1.xnb

xcompress32.dll
---------------

XNB files can be compressed or uncompressed. The compression algorithm used is LZXD, which was invented by Microsoft, see [official website here](https://msdn.microsoft.com/en-us/library/cc483133%28v=exchg.80%29.aspx).
I was unable to find any source code for LZXD compression. That's why I used the xcompress32.dll. This is most likely a proprietary dll so I don't want to include it in my project.

If you want to create compressed XNB files then you need to get this dll from somewhere. It is part of the [XNA Game Studio](https://en.wikipedia.org/wiki/Microsoft_XNA) which is discontinued. (I found the dll [here](https://github.com/cpich3g/rpftool/blob/master/RPFTool/xcompress32.dll?raw=true))
Once you downloaded the dll, move it next to the png_to_xnb.exe file, then my little program should find it.

Command line options
--------------------

    Usage: png_to_xnb.exe [-h|--help] [-c] [-u] [-hidef] png_file [xnb_file]

    The program reads the image 'png_file' and saves as an XNB file as 'xnb_file'.
    Start without any input parameters to launch a GUI.

    Options:
      -h      Prints this help.
      -c      Compress the XNB file. This is the default if xcompress32.dll is
              available. Note that the compression might take significant time, but
              of course the result XNB file will be much smaller.
      -u      Save uncompressed XNB file, even xcompress32.dll is available.
      -hidef  XNB's can be either 'reach' or 'hidef'. Default is 'reach', so use
              this -hidef option when necessary. I don't know what 'reach' or
              'hidef' means, but for example Terraria cannot load 'hidef' XNB files.

    png_file  This can either be a file or a directory. If this is a directory
              then it will convert all *.png files in the directory (not recursive).
    xnb_file  This can also be a file or a directory. If this is a directory then
              the filename will be name.xnb if the image file was name.png
              If this is omitted then it converts the png_file into the same folder.

License
-------

GPLv3, see LICENSE file
