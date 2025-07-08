# MediaPipe Tasks Image Classification Android Demo

### Overview

This is a camera app that continuously classifies the objects (classes and
confidence) in the frames seen by your device's back camera, in an image
imported from the device gallery,  or in a video imported by the device gallery,
with the option to use a quantized [EfficientDet Lite 0](https://storage.googleapis.com/mediapipe-tasks/object_detector/efficientdet_lite0_uint8.tflite), or
[EfficientDet Lite2](https://storage.googleapis.com/mediapipe-tasks/object_detector/efficientdet_lite2_uint8.tflite) model.

The model files are downloaded by the project when you build and run the app.
You don't need to do any steps to download TFLite models into the project
explicitly unless you wish to use your own models. If you do use your own
models, place them into the app's *Assets* directory.

This application should be run on a physical Android device to take advantage
of the physical camera, though the gallery tab will enable you to use an
emulator for opening locally stored files.

![Image Classification Demo](imageclassifier.gif?raw=true "Image Classification Demo")

## Build the demo using Visual Studio

### Prerequisites

* The **[Visual Studio](https://visualstudio.microsoft.com/vs/mac/)** IDE.
  This sample has been tested on Visual Studio 2022 for Mac.

* A physical Android device with a minimum OS version of SDK 24 (Android 7.0 -
  Nougat) with developer mode enabled. The process of enabling developer mode
  may vary by device. You may also use an Android emulator with more limited
  functionality.

### Building

* Open Visual Studio. From the Welcome screen, select Open a local
  Visual Studio project, solution, or file.

* From the Open File or Project window that appears, navigate to and select
  the MediaPipeExamples/MediaPipeExamples.sln Android solution. Click Open.
  You may be asked if you trust the project. Select Trust.

* Select the ImageClassification project.

* With your Android device connected to your computer and developer mode
  enabled, click on the black Run arrow in Visual Studio.

### Models used

Downloading, extraction, and placing the models into the *Assets* folder is
managed automatically by the **ImageClassification.csproj** file.
