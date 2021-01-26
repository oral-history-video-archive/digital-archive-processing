# digital-archive-processing

A set of tools for ingesting, processing, and publishing content to the digital video archive.

## Processing Overview

Videos are segmented by hand into smaller story segments typically of 3 minutes length by means of a specially formatted text document. The segmentation file contains the meta information about each logical story segment such as:

* start and end times within the parent video
* story title
* video transcript

The `DocToXmlCleanup` tool is used to validate and convert the segmentation files to 
XML which is then imported in to the processing database via `Import_XML.cmd`

Imported data is then processed by invoking the `RunProcessing` command.

## Build

1. Clone repository
1. Open `digital-archive-processing.sln` in Visual Studio 2019 or newer
1. Right click `digital-archive-processing` solution in *Solution Explorer* | click *Build Solution*

Visual Studio should download and install all necessary dependency packages upon first build.

## Installation

### Database

The processing tools require a SQL Server 2016 or SQL Server 2016 Express database or newer.
Use the InformediaCORE.Schema project to initialize the database schema.

### External Dependencies

The processing tools have a number of external dependencies not included in the repository. These tools should be downloaded from their respective websites and installed on the system where you will be running the processing tools. The `InformediaCORE.config` file must be updated to point to the final installed location for these files. A typical installation nests all dependencies under `C:\DigitalArchive\tools`

Detailed instructions for installing each dependency is broken out in to the following files:

* [Installing FFmepg](Docs/installing_ffmpeg.md)
* [Installing PowerShell](Docs/installing_powershell.md)
* [Building Gentle Forced Aligner](Docs/building_gentle_on_windows.md)
* [Installing spaCy NLP](Docs/installing_spacy_nlp.md)
* [Installing Stanford NER](Docs/installing_stanford_ner.md)
