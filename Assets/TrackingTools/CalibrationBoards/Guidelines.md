Pattern guidelines
=================


Design
------

- Design for findChessboardCorersSB(). It should produce more accurate results than the older findChessboardCorners().
https://docs.opencv.org/master/d9/d0c/group__calib3d.html#gadc5bcb05cb21cf1e50963df26986d7c9
- Avoid rotational symmetry by keeping your rows and columbs asymmetrical. If you you rotate the board 180 degrees, it should NOT loook the same (#1).
- Aim for a tile size of 50 pixels in the image domain (#2). For a 1080p image: floor( 1080 / 50 ) = 21 vertical tiles. However findChessboardCorersSB needs space for rounded tiles, so 21 - 2 = 19 vertical tiles.
- Beware that findChessboardCorersSB expects the upper-left corner to be white.


Production
-----

**Printing**  
- For infrared cameras use a laser printer. In infrared light laser prints have great darks while inkjet prints appear faded (#3).

**Board**
- Spray mount onto aluminum composite panels (ACP). "aluminium sandwichplader". PVC composite panels can shrink or expand slightly depending on temperature.
https://e-plast.dk/shop/plastplader/alu-sandwichplader.aspx
- Use an online service to make sure you avoid bubbles (#2).


Calibration dance
-----------------

**Camera intrinsics**  
- If your calibration board is large enough to cover the view, then only four samples should be required (#2).
- Else, make sure that you gather samples from all areas of the view. "But keep in mind, the less area the calibration target covers in the image, the more grows the uncertainty of the estimated camera parameters." (#2).
- Sampling at multiple distances does not improve the result (#2).
- Avoid movement to reduce motion blur. At best, mount both camera and board (#2).
- Avoid orthogonal shots. "This is an unstable configuration, and noise can have a considerable effect on the calibration result. It is better always to use a tilt of 15–30° between target normal and camera axis" (#2).
- Set up a diffuse illuminmation of the board. "The sensor is not linear at both ends of its measurement range. Therefore, it is better to keep the sensor value between 128 and 200 for bright image regions. Try to use diffuse illumination by pointing spotlights away from the target."

**Projector (intrinsics and) extrinsics from camera**  
- It is absolutely crucial for precision that movement is minimised. Even more so for projector calibration because there is a frame delay during sampling. Always support the calibration board against something. For example a tripod with variable height.



References
----------
#1 https://answers.opencv.org/question/96561/calibration-with-findcirclesgrid-trouble-with-pattern-widthheight
#2 https://medium.com/@hey_duda/the-magic-behind-camera-calibration-8596b7ddcd71
#3 https://answers.opencv.org/question/228413/printer-ink-not-black-in-ir/#229238
