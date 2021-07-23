/*
	The MIT License (MIT)

	Copyright (c) 2016, Carl Emil Carlsen

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in
	all copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
	THE SOFTWARE.
*/

using UnityEngine;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.VideoModule;

public class OpenCvKalmanFilter3D
{
	float _expectedMeasurementNoise = 50f;
	float _expectedProcessNoise = 0.001f;
	bool _dirtyExpectedMeasurementNoise = true;
	bool _dirtyExpectedProcessNoise = true;

	KalmanFilter _filter;
	Mat _measurementMat;

	Vector3 _predictedPosition;
	Vector3 _measuredPosition;
	Vector3 _estimatedPosition;

	float _gain;

	int dynamParamCount;				// posX, posY, posZ, velX, velY, velZ, accX, accY, accZ
	const int measureParamCount = 3;	// posX, posY, posZ
	static readonly int cvType = CvType.CV_32FC1;


	public float expectedMeasurementNoise {
		get { return _expectedMeasurementNoise; }
		set {
			_expectedMeasurementNoise = Mathf.Max( 0, value );
			_dirtyExpectedMeasurementNoise = true;
		}
	}

	public float expectedProcessNoise {
		get { return _expectedProcessNoise; }
		set {
			_expectedProcessNoise = Mathf.Max( 0, value );
			_dirtyExpectedProcessNoise = true;
		}
	}


	public Vector3 predictedPosition => _predictedPosition;
	public Vector3 measuredPosition => _measuredPosition;
	public Vector3 estimatedPosition => _estimatedPosition;
	public float gain => _gain;



	public OpenCvKalmanFilter3D( Vector3 initialPosition, bool useAccelleration )
	{
		// Create filter.
		dynamParamCount = useAccelleration ? 9 : 6;
		_filter = new KalmanFilter( dynamParamCount, measureParamCount, 0, cvType );
		_filter.set_measurementMatrix( Mat.eye( measureParamCount, dynamParamCount, cvType ) ); // (Required)

		// Init "Process Model" or "Transition Matrix" http://stackoverflow.com/questions/35340188/kalman-filter-3d-implementation
		Mat transitionMat = new Mat( dynamParamCount, dynamParamCount, cvType );
		float dt = Time.fixedDeltaTime;
		float v = dt; // first derivative - velocity
		if( useAccelleration ){
			float a = 0.5f * Mathf.Pow( dt, 2 ); // second derivative - acceleration
			transitionMat.put( 0, 0, new float[]{
				1, 0, 0, v, 0, 0, a, 0, 0,
				0, 1, 0,  0,v, 0, 0, a, 0,
				0, 0, 1,  0, 0,v, 0, 0, a, 
				0, 0, 0,  1, 0, 0,v, 0, 0, 
				0, 0, 0,  0, 1, 0, 0,v, 0,
				0, 0, 0,  0, 0, 1, 0, 0,v,
				0, 0, 0,  0, 0, 0, 1, 0, 0, 
				0, 0, 0,  0, 0, 0, 0, 1, 0,
				0, 0, 0,  0, 0, 0, 0, 0, 1
			});
		} else {
			transitionMat.put( 0, 0, new float[]{
				1, 0, 0, dt, 0, 0,
				0, 1, 0, 0, dt, 0,
				0, 0, 1, 0, 0, dt,
				0, 0, 0, 1, 0, 0,
				0, 0, 0, 0, 1, 0,
				0, 0, 0, 0, 0, 1
			});
		}

		_filter.set_transitionMatrix( transitionMat );

		// Init measument matrix (H).
		_measurementMat = Mat.eye( measureParamCount, 1, cvType );

		// Set initial state estimate.
		/*
		Mat statePreMat = KF.get_statePre();
		statePreMat.put( 0, 0, new float[] { (float) cursorPos.x, (float) cursorPos.y, 0, 0 } );
		Mat statePostMat = KF.get_statePost();
		statePostMat.put( 0, 0, new float[] { (float) cursorPos.x, (float) cursorPos.y, 0, 0 } );
		*/

		// Set initial state
		Mat statePre = new Mat( measureParamCount, 1, CvType.CV_32F, new Scalar( 0 ) );
		statePre.put( 0, 0, initialPosition.x );
		statePre.put( 1, 0, initialPosition.y );
		statePre.put( 2, 0, initialPosition.z );
		_filter.set_statePre( statePre );
	}


	public Vector3 Update( Vector3 position )
	{
		if( _dirtyExpectedMeasurementNoise ){
			Mat measurementNoiseCovMat = Mat.eye( measureParamCount, measureParamCount, cvType ); // identity
			measurementNoiseCovMat = measurementNoiseCovMat.mul( measurementNoiseCovMat, 1 + _expectedMeasurementNoise );
			_filter.set_measurementNoiseCov( measurementNoiseCovMat );
			_dirtyExpectedMeasurementNoise = false;
		}
		if( _dirtyExpectedProcessNoise ){
			Mat processNoiseCovMat = Mat.eye( dynamParamCount, dynamParamCount, cvType ); // identity
			processNoiseCovMat = processNoiseCovMat.mul( processNoiseCovMat, 1 + _expectedProcessNoise );
			_filter.set_processNoiseCov( processNoiseCovMat );
			_dirtyExpectedProcessNoise = false;
		}

		// Predict.
		// Predict can take an optionl control matrix (if we know the state of what is driving the values, for example a motor)
		using( Mat prediction = _filter.predict() ) _predictedPosition = Mat2Vector3( prediction );

		// Measure.
		_measuredPosition = position;
		_measurementMat.put( 0, 0, _measuredPosition.x );
		_measurementMat.put( 1, 0, _measuredPosition.y );
		_measurementMat.put( 2, 0, _measuredPosition.z );

		// Estimate (also called update and correct).
		using( Mat estimation = _filter.correct( _measurementMat ) ) _estimatedPosition = Mat2Vector3( estimation );

		// Update gain.
		Mat gainMat = _filter.get_gain();
		_gain = (float) gainMat.get(0,0)[0];

		return _estimatedPosition;
	}


	Vector3 Mat2Vector3( Mat mat )
	{
		return new Vector3(
			(float) mat.get(0,0)[0],
			(float) mat.get(1,0)[0],
			(float) mat.get(2,0)[0]
		);
	}
}