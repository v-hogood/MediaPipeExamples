# MediaPipe Tasks Text Classification Android Demo

### Overview

This sample will accept text entered into a field and classify it as either
positive or negative with a provided confidence score. The supported
classification models include Word Vector and MobileBERT.

These instructions walk you through building and running the demo on an Android
device.

The model files are downloaded by the project when you build and run the app.
You don't need to do any steps to download TFLite models into the project
explicitly.

![Text Classification Demo](textclassification.gif?raw=true "Text Classification Demo")

## Build the demo using Visual Studio

### Prerequisites

* The **[Visual Studio](https://visualstudio.microsoft.com/vs/mac/)** IDE.
  This sample has been tested on Visual Studio 2022 for Mac.

* A physical Android device with a minimum OS version of SDK 24 (Android 7.0 -
  Nougat) with developer mode enabled. The process of enabling developer mode
  may vary by device. You may also use and Android emulator.

### Building

* Open Visual Studio. From the Welcome screen, select Open a local
  Visual Studio project, solution, or file.

* From the Open File or Project window that appears, navigate to and select
  the MediaPipeExamples/MediaPipeExamples.sln Android solution. Click Open.

* Select the TextClassification project.

* With your Android device connected to your computer and developer mode
  enabled, click on the black Run arrow in Visual Studio.

### Models used

Downloading, extraction, and placing the models into the *Assets* folder is
managed automatically by the **TextClassification.csproj** file.
