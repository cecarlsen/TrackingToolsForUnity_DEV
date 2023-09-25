# TrackingTools for Unity

A set of tools to make camera and video projector calibration slightly easier in Unity. This is useful for AR/XR and projection mapping setups. It contains two packages: *TrackingTools* and *TrackingTools.KinectAzure*.

![CalibrationDance](https://raw.githubusercontent.com/cecarlsen/TrackingToolsForUnity/master/ReadmeImages/ProjectorCameraCalibration.jpg)

#### Dependencies
- Unity 2023.1 (it may work with other versions, but no promises)
- [OpenCV for Unity](https://assetstore.unity.com/packages/tools/integration/opencv-for-unity-21088) (sold on the Asset Store). Tested with version 2.5.6.
- [Optional] [Azure Kinect Examples for Unity](https://assetstore.unity.com/packages/tools/integration/azure-kinect-examples-for-unity-149700) (sold on the Unity Asset Store). If you want to use *TrackingTools.KinectAzure*. Tested with version 1.19.
- [Optional] When working with the ProjectorFromCameraExtrinsicsEstimator it can be convenient to full-screen a game window from the editor. I use [Fullscreen Editor](https://assetstore.unity.com/packages/tools/utilities/fullscreen-editor-69534) for this.

## TrackingTools
MonoBehaviours:

#### WebCameraTextureProvider
Creates and forwards a Unity WebCamTexture.

#### CameraFromCheckerboardIntrinsicsEstimator  
Finds the intrinsics (internal properties) of a camera using a physical calibration board and stores it in a json file at *StreamingAssets/TrackingTools/Intrinsics/FILE_NAME.json*.

- Use a calibration board that corresponds to the resolution of your camera. Find boards at *TrackingTools/CalibrationBoards/*. See more about printing further down.
- Open and run the scene *TrackingTools/Examples/02 CameraIntrinsicsEstimator*. Prepare to take samples.
  - If your calibration board is large enough to cover the view, then only four samples should be required. Else, make sure that you gather samples from all areas of the view. But keep in mind, the less area the calibration target covers in the image, the more grows the uncertainty of the estimated camera parameters.
  - Sampling at multiple distances does not improve the result.
  - Avoid movement to reduce motion blur. At best, mount both camera and board, repositioning the board for each sample.
  - Avoid orthogonal shots. it makes the calibration more prone to noise. Instead, always to use a tilt of 15–30° between target normal and camera axis.

#### CameraIntrinsicsLoader
Loads intrinsics from json file located in the StreamingAssets folder and applies properties to a Unity camera (with the PhysicalCamera option enabled).

#### CameraIntrinsicsSaver
Saves intrinsics from a Unity camera to a json file located in the StreamingAssets.

#### CameraFromCheckerboardExtrinsicsEstimator
Find extrinsics (physical position and rotation) of a camera relative to calibration board and store it in a json file at *StreamingAssets/TrackingTools/Extrinsics/FILE_NAME.json*. Do use this, you first need to find the intrinsics of the camera (CameraIntrinsicsEstimator).

#### CameraFromCircleAnchorExtrinsicsEstimator
Find extrinsics (physical position and rotation) of a camera relative to specially designed marker and store it in a json file at *StreamingAssets/TrackingTools/Extrinsics/FILE_NAME.json*. The marker is consists of four points on a circle and one point in the middle. Using a piece of thread/string and tape, and a ruler, you can easily create this marker at varies scales on the go.

- Make sure the marker is in the camera view.
- Open and run the scene *TrackingTools/Examples/05 CameraFromCircleAnchorExtrinsicsEstimator*.
- Point and click the five marker points as precisely as possible.

#### ExtrinsicsLoader
Loads extrinsics from json file located in the StreamingAssets folder and applies properties (position and rotation) to a "anchor" transform. The transformation can be inversed, so you can switch between the marker and the camera/projector being fixed.

#### ProjectorFromCameraExtrinsicsEstimator
Find the extrinsics of a video projector (indirectly also finding the intrinsics) using a specially designed calibration board. [Twitter post](https://twitter.com/cecarlsen/status/1265567632591331328).

- Print one of the specially designed "ProjectorFromCameraExtrinsics" boards found in *TrackingTools/CalibrationBoards/*.
  - The PDF only represents the right half the calibration board. The left half is supposed to be 50% grey, for video projecting a dot pattern. It's recommended to use a coloured sheet of paper instead of printing grey to avoid gloss.
- Open and run the scene *TrackingTools/Examples/07 ProjectorFromCameraExtrinsicsEstimator* and get ready for the calibration dance.
  - Similarly to using CameraIntrinsicsEstimator, the calibration starts with capturing four samples. The same principles apply to this, except this time the camera needs to see both the printed chess pattern and the projected dot pattern.
  - Once the four samples are acquired, the dot patterns starts to follow the board as you move it around. This is your chance to improve the calibration in areas that are off.
  - It is absolutely crucial for precision that movement is minimised. Even more so for projector calibration because there is a frame delay during sampling. Always support the calibration board against something. For example a tripod with variable height.


## TrackingTools.KinectAzure
MonoBehaviours:

#### KinectAzureTexture2DProvider  
Creates and forwards a IR and/or colour RenderTextures from the Kinect Azure.


## Additional notes

#### Printing calibration boards
- Print and spray mount on foam or alu-sandwich (aluminum composite panels ACP) boards. Avoid glue bubbles. Keep absolutely flat.
- Use matte paper. Avoid gloss.
- If your camera sees infrared (IR) only, then print the board using [a laser printer instead of inkjet](https://answers.opencv.org/question/228413/printer-ink-not-black-in-ir/#229238) to gain higher contrast.
- Set up bright diffuse room lighting and make sure the board is lit evenly.

#### Designing your own caibration board
- Design for OpenCV [findChessboardCorersSB()](https://docs.opencv.org/master/d9/d0c/group__calib3d.html#gadc5bcb05cb21cf1e50963df26986d7c9). It should produce more accurate results than the older findChessboardCorners().
- Avoid rotational symmetry by keeping your rows and columns asymmetrical. If  you rotate the board 180 degrees, it should NOT look the same.
- Aim for a tile size of 50 pixels in the image domain. For a 1080p image: floor( 1080 / 50 ) = 21 vertical tiles. However findChessboardCorersSB needs space for rounded tiles, so 21 - 2 = 19 vertical tiles.
- Beware that findChessboardCorersSB() expects the upper-left corner to be white.

### Further reading
- [The Magic Behind Camera Calibration](https://medium.com/@hey_duda/the-magic-behind-camera-calibration-8596b7ddcd71) by Alexander Duda.

### Credits
This work could not have been achieved without much inspiration from examples posted by kind people online.

- [Elliot Woods](http://elliotwoods.info/) and [Kyle McDonald](https://kylemcdonald.net/) did a projector and camera calibration [workshop](http://artandcode.com/3d/workshops/4a-calibrating-projectors-and-cameras/) in 2011.
- Kyle McDonald published [ofxCv](https://github.com/kylemcdonald/ofxCv) computer vision tools for [openFrameworks](https://openframeworks.cc/) (MIT license). [Calibration.cpp](https://github.com/kylemcdonald/ofxCv/blob/master/libs/ofxCv/src/Calibration.cpp) has been an important sources of learning.
- [Cassinelli Alvaro](https://www.alvarocassinelli.com/) improved on the solution and posted [a video](https://www.youtube.com/watch?v=pCq7u2TvlxU) with a nice explanation in the description.


### Author
Carl Emil Carlsen | [cec.dk](http://cec.dk) | [github](https://github.com/cecarlsen)
