# spaCy Natural Language Processing (NLP)

spaCy is a Natural Language Processing (NLP) toolkit used to detect named entities within the story transcripts. Output from spaCy NLP and Standford NER are combined for improved accuracy.


## Install Visual Studio Redistributable

Download and install the Universal C Runtime:
https://support.microsoft.com/en-us/help/2977003/the-latest-supported-visual-c-downloads

## Install Python

Download the 64-bit installer for Python 3.8.1 for Windows here:
https://www.python.org/downloads/release/python-381/

* Run Python installer `python-3.8.1-amd64.exe`
* Screen: Install Python 3.8.1 (64-bit)
  * Uncheck: Install launcher for all users
  * Uncheck: Add Python 3.8 to PATH
  * Choose: Customize installation
* Screen: Optional Features  
  * Uncheck: All features except pip
* Screen: Advanced Options
  * Uncheck: All
  * Set install path to `C:\DigitalArchive\tools\spaCy`
* Screen: Setup was successful
  * Click: Close

The final folder structure should look like:

```bat
C:\DigitalArchive\tools\spaCy
├───__pycache__
├───DLLs
├───include
├───Lib
├───libs
├───Scripts
└───Tools
```

## Install spaCy

Refer to [spaCy's installation guide](https://spacy.io/usage) for further details.

Open command prompt and type:

```bat
cd C:\DigitalArchive\tools\spaCy
Scripts\pip install -U spacy
python -m spacy download en_core_web_sm
```

**NOTE:** Ignore warnings output during spaCy install.

## Copy Script Files

* Copy `example.py` to `C:\DigitalArchive\tools\spaCy`
* Copy `spacy.py` to `C:\DigitalArchive\tools\spaCy`

## Test

Open command prompt and type:

```bat
cd C:\DigitalArchive\tools\spaCy
python.exe example.py
```

Should produce output like:

```
Noun phrases: ['Sebastian Thrun', 'self-driving cars', 'Google', 'few people', 'the company', 'him', 'I', 'you', 'very senior CEOs', 'major American car companies', 'my hand', 'I', 'Thrun', 'an interview', 'Recode']
Verbs: ['start', 'work', 'drive', 'take', 'can', 'tell', 'would', 'shake', 'turn', 'talk', 'say']
Sebastian 5 14 NORP
Google 61 67 ORG
2007 71 75 DATE
American 173 181 NORP
Recode 299 305 ORG
earlier this week 306 323 DATE
```