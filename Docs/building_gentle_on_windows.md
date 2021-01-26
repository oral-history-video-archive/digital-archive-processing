# Instructions for Building Gentle on Windows 10 via Windows Subsystem for Linux (WSL)

Gentle's make files seem to suffer from both logical errors (attempts to run a step before the dependency has been installed) and from assumptions about the build environment which may not be true for your system.  I was able to reverse engineer the intent of the make file and produce this recipe for building Gentle on WSL. This has been tested and refined over multiple iterations across several machines however your mileage may vary.

These instructions are based on the following:

* Windows 10 1709 or newer
* Windows Subsystem for Linux (WSL1)
* Ubuntu 18.04 LTS
* lowerquality/gentle commit ce6873d8d Jan 7, 2019

## Prepare Windows

From https://docs.microsoft.com/en-us/windows/wsl/install-win10

* Install Windows Subsystem for Linux
* Install Ubuntu 18.04 LTS from Windows Store

## Prepare Ubuntu

* Start Ubuntu, initial setup will begin.
* Set the root userid and password as desired, but don't forget them!

## Get Gentle

```bash
git clone https://github.com/lowerquality/gentle.git
cd ./gentle
```

## Install Gentle

From `~/gentle/install.sh`

```bash
git submodule init
git submodule update
sudo bash ./install_deps.sh
sudo bash ./install_models.sh
```

**NOTE:**

> This is where break I from gentle's build process. Gentle's build script attempts to run install_kaldi.sh which appears to replicate kaldis' build scripts but it always fails. Instead I follow kaldi's instructions, with some modifiations detailed below.

## Install/Build Kaldi

From `~/gentle/ext/kaldi/INSTALL`

Kaldi tells us to:

1. Go to `tools/` and follow INSTALL instructions there.
2. Go to `src/`   and follow INSTALL instructions there.

So based on that, here's what we're going to do...

### Build kaldi/tools

From `~/gentle/ext/kaldi/tools/INSTALL`

```bash
cd ext/kaldi/tools
sudo bash extras/check_dependencies.sh
```

* This tool outputs some instructions, follow them.
* For example, I had to `$sudo apt-get install python2.7`
* Run it again and follow instructions until it says: `extras/check_dependencies.sh: all OK.`

```bash
$make
```

* wait a long time...
* Ends with the following, this is OK:

```
  Warning: IRSTLM is not installed by default anymore. If you need IRSTLM
  Warning: use the script extras/install_irstlm.sh
  All done OK.
```

### Build kaldi/src

From `~/gentle/ext/kaldi/src/INSTALL` plus `~/gentle/ext/install_kaldi.sh`

```bash
cd ~/gentle/ext/kaldi/src
./configure --static --static-math=yes --static-fst=yes --use-cuda=no
```

This command will result in an error like:

```
** Failed to configure ATLAS libraries ***
** ERROR **
** Configure cannot proceed automatically.
** If you know that you have ATLAS installed somewhere on your machine, you
** may be able to proceed by replacing [somewhere] in kaldi.mk with a directory.
```    
   
To fix this...

```bash
cp kaldi.mk /mnt/c/Users/yourusername/Desktop/
```

Edit `kaldi.mk` from your desktop using notepad and replace ATLASLIBS with:

```bash
ATLASLIBS = /usr/lib/x86_64-linux-gnu/libatlas.so.3 /usr/lib/x86_64-linux-gnu/libf77blas.so.3 /usr/lib/x86_64-linux-gnu/libcblas.so.3 /usr/lib/x86_64-linux-gnu/liblapack_atlas.so.3 -Wl,-rpath=/usr/lib/x86_64-linux-gnu
```

Resume with build...

```bash
cp /mnt/c/Users/yourusername/Desktop/kaldi.mk kaldi.mk
make depend -j 8
make -j 8
```

### Build Gentle (finally)

From `~/gentle/install.sh`

```bash
cd ~/gentle/ext
make depend
make
```

There will be a lot of output with warnings, it's probably ok. When done there should be a `k3` and `m3` file in the `ext` directory.

## Setup Python Application

From `~/gentle/install_deps.sh`

```bash
cd ~/gentle
sudo python3 setup.py develop
```

## Test Gentle

At the WSL Bash command prompt, type:

```bash
cd ~/gentle
python3 ~/gentle/align.py ~/gentle/examples/data/lucier.mp3 ~/gentle/examples/data/lucier.txt
```

The output should look like (truncated to the first two `words` for brevity):

```bash
INFO:root:converting audio to 8K sampled wav
INFO:root:starting alignment
INFO:root:1/6
INFO:root:2/6
INFO:root:3/6
INFO:root:4/6
INFO:root:5/6
INFO:root:6/6
INFO:root:21 unaligned words (of 105)
INFO:root:after 2nd pass: 4 unaligned words (of 105)
{
  "transcript": "I am sitting in a room different from the one you are in now. I am recording the sound of my speaking voice and I am going to play it back into the room again and again until the resonant frequencies of the room reinforce themselves so that any semblance of my speech, with perhaps the exception of rhythm, is destroyed. What you will hear, then, are the natural resonant frequencies of the room articulated by speech. I regard this activity not so much as a demonstration of a physical fact, but more as a way to smooth out any irregularities my speech might have.",
  "words": [
    {
      "alignedWord": "i",
      "case": "success",
      "end": 6.9799999999999995,
      "endOffset": 1,
      "phones": [
        {
          "duration": 0.21,
          "phone": "ay_S"
        }
      ],
      "start": 6.77,
      "startOffset": 0,
      "word": "I"
    },
    {
      "alignedWord": "am",
      "case": "success",
      "end": 7.180000000000001,
      "endOffset": 4,
      "phones": [
        {
          "duration": 0.08,
          "phone": "ae_B"
        },
        {
          "duration": 0.12,
          "phone": "m_E"
        }
      ],
      ...
```

Congratulations! You have successfully built and installed Gentle on your system.

## Known Issues

The audio files produced by Gentle are written to `/tmp` which will fill up over time. 

Refer to the following WSL GitHub issues:

* [/tmp never cleared #1278](https://github.com/microsoft/WSL/issues/1278)
* [/tmp not being cleared on boot #3406](https://github.com/microsoft/WSL/issues/3406)
* [Can't run cron jobs #344](https://github.com/microsoft/WSL/issues/344)

Also see blog post: [Background Task Support in WSL](https://devblogs.microsoft.com/commandline/background-task-support-in-wsl/)

### Tested Solution:

Use Windows Task Scheduler to run:

```bat
wsl find /tmp/* -mtime +1 -exec rm {} \;
```



