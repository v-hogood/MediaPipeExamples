# MediaPipe Tasks Audio Classifier Android Demo

### Overview

This is an audio app that continuously classifies the sound of objects that is
recorded by the microphone, or in the audios imported from the device library,
with the option to use a
[Yamnet classification](https://storage.googleapis.com/download.tensorflow.org/models/tflite/task_library/audio_classification/android/lite-model_yamnet_classification_tflite_1.tflite).
The model files are downloaded by the project when you build and run the app.
You don't need to do any steps to download TFLite models into the project
explicitly unless you wish to use your own models. If you do use your own
models, place them into the app's *Assets* directory.

![Audio Classifier Demo](audioclassifier.png?raw=true "Audio Classifier Demo")

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

* Select the AudioClassification project.

* With your Android device connected to your computer and developer mode
  enabled, click on the black Run arrow in Visual Studio.

### Models used

Downloading, extraction, and placing the models into the *Assets* folder is
managed automatically by the **AudioClassification.csproj** file.
