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

public class OpenCvKalmanFilter3DTest : MonoBehaviour
{
	public float expectedMeasurementNoise = 50f;
	public float expectedProcessNoise = 0.001f;
	public float fakeNoise = 0.5f;

	OpenCvKalmanFilter3D _kalman3D;

	LineRenderer _actualLine;
	LineRenderer _estimatedLine;

	Vector3[] _actualPoints;
	Vector3[] _estimatedPoints;

	const int linePointCapacity = 300;


	void Start()
	{
		// Visuaization
		_actualLine = new GameObject().AddComponent<LineRenderer>();
		_estimatedLine = new GameObject().AddComponent<LineRenderer>();
		Material material = new Material( Shader.Find("Unlit/Color") );
		_actualLine.material = material;
		_estimatedLine.material = material;
		_actualLine.material.color = Color.green;
		_estimatedLine.material.color = Color.red;
		_actualLine.startWidth = 0.01f;
		_actualLine.endWidth = 0.01f;
		_estimatedLine.startWidth = 0.01f;
		_estimatedLine.endWidth = 0.01f;
		_actualLine.positionCount = linePointCapacity;
		_estimatedLine.positionCount = linePointCapacity;
		_actualPoints = new Vector3[linePointCapacity];
		_estimatedPoints = new Vector3[linePointCapacity];
	}
	

	void Update()
	{
		//Return if no input.
		if( !Input.GetMouseButton( 0 ) ) return;

		// Get input.
		Ray ray = Camera.main.ScreenPointToRay( Input.mousePosition );
		Vector3 actualPosition = ray.origin + ray.direction * 10;
		if( fakeNoise > 0 ) actualPosition += Random.insideUnitSphere * fakeNoise;

		// Reset when input starts.
		if( Input.GetMouseButtonDown( 0 ) ) _kalman3D = new OpenCvKalmanFilter3D( actualPosition, true );

		// Execute kalman filter.
		_kalman3D.expectedMeasurementNoise = expectedMeasurementNoise;
		_kalman3D.expectedProcessNoise = expectedProcessNoise;
		Vector3 estimatedPosition = _kalman3D.Update( actualPosition );

		// Accumulate history.
		if( Input.GetMouseButtonDown( 0 ) ) {
			for( int i = 0; i < linePointCapacity; i++ ) {
				_actualPoints[ i ] = actualPosition;
				_estimatedPoints[ i ] = estimatedPosition;
			}
		} else {
			for( int i = linePointCapacity - 1; i > 0; i-- ) {
				_actualPoints[i] = _actualPoints[i-1];
				_estimatedPoints[i] = _estimatedPoints[i-1];
			}
			_actualPoints[0] = actualPosition;
			_estimatedPoints[0] = estimatedPosition;
		}
		_actualLine.SetPositions( _actualPoints );
		_estimatedLine.SetPositions( _estimatedPoints );
	}
}