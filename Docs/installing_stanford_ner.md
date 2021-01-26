# Stanford Named Entity Recognizer (NER)

The Stanford Named Entity Recognizer (NER) is used to detect named entities within the story transcripts. Output from Standford NER and spaCy NLP are combined for improved accuracy.

## Install Java

* Download a zip archive containing a pre-built Java 8 binary from https://adoptopenjdk.net/
  * `OpenJDK8U-jre_x64_windows_hotspot_8u242b08.zip` at the time of this writing.
* Extract `OpenJDK8U-jre_x64_windows_hotspot_8u242b08.zip` to `C:\DigitalArchive\tools\java`
* The final folder structure should look like:

```bat
C:\DigitalArchive\tools\java
├───bin
└───lib
```

## Install Stanford NER

* Download a zip archive containing the Stanford NER from https://nlp.stanford.edu/software/CRF-NER.shtml. 
  * At the time of this writing it was `stanford-ner-2018-10-16.zip`
* Extract `stanford-ner-2018-10-16.zip` to `C:\DigitalArchive\tools\stanford-ner`
* The final folder structure should look like:

```bat
C:\DigitalArchive\tools\stanford-ner
├───classifiers
└───lib
```

## Test

Open a command prompt and type:

```bat
cd C:\DigitalArchive\tools\stanford-ner
..\java\bin\java.exe -mx1000m -cp stanford-ner.jar;lib/* edu.stanford.nlp.ie.crf.CRFClassifier -loadClassifier classifiers\english.all.3class.distsim.crf.ser.gz -textFile sample.txt 
```

Should produce output like:

```
FClassifier -loadClassifier classifiers\english.all.3class.distsim.crf.ser.gz -textFile sample.txt
Invoked on Tue Feb 25 14:40:45 EST 2020 with arguments: -loadClassifier classifiers\english.all.3class.distsim.crf.ser.gz -textFile sample.txt
loadClassifier=classifiers\english.all.3class.distsim.crf.ser.gz
textFile=sample.txt
Loading classifier from classifiers\english.all.3class.distsim.crf.ser.gz ... done [1.2 sec].
The/O fate/O of/O Lehman/ORGANIZATION Brothers/ORGANIZATION ,/O the/O beleaguered/O investment/O bank/O ,/O hung/O in/O the/O balance/O on/O Sunday/O as/O Federal/ORGANIZATION Reserve/ORGANIZATION officials/O and/O the/O leaders/O of/O major/O financial/O institutions/O continued/O to/O gather/O in/O emergency/O meetings/O trying/O to/O complete/O a/O plan/O to/O rescue/O the/O stricken/O bank/O ./O
Several/O possible/O plans/O emerged/O from/O the/O talks/O ,/O held/O at/O the/O Federal/ORGANIZATION Reserve/ORGANIZATION Bank/ORGANIZATION of/ORGANIZATION New/ORGANIZATION York/ORGANIZATION and/O led/O by/O Timothy/PERSON R./PERSON Geithner/PERSON ,/O the/O president/O of/O the/O New/ORGANIZATION York/ORGANIZATION Fed/ORGANIZATION ,/O and/O Treasury/ORGANIZATION Secretary/O Henry/PERSON M./PERSON Paulson/PERSON Jr./PERSON ./O
CRFClassifier tagged 85 words in 2 documents at 1075.95 words per second.
```