/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Events;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;

namespace TrackingTools
{
	public class LevelsUsingOpenCvExample : MonoBehaviour
	{
		[SerializeField] Texture _inputTexture;
		[SerializeField, Range( 0f, 0.2f ) ] float _blackValue = 0.0f;
		[SerializeField, Range( 0.8f, 1.0f )] float _whiteValue = 1.0f;
		[SerializeField, Range(0.1f,8f) ] float _gammaValue = 1.0f;
		[SerializeField] UnityEvent<Texture> _outputTexture = null;

		Mat _camTexMat;
		Mat _camTexGrayMat;
		Texture2D _processedCameraTexture;

		Color32[] _tempTransferColors;
		Texture2D _tempTransferTexture;

		Mat _levelsLookup;
		byte[] _levelsLookupData;

		bool _dirtyTexture;
		bool _dirtyLevelsLookup;


		public Texture inputTexture {
			get { return _inputTexture; }
			set {
				_inputTexture = value;
				_dirtyTexture = true;
			}
		}

		public float blackValue {
			get { return _blackValue; }
			set {
				_blackValue = Mathf.Clamp01( value );
				_dirtyLevelsLookup = true;
			}
		}

		public float whiteValue {
			get { return _whiteValue; }
			set {
				_whiteValue = Mathf.Clamp( value, 0.1f, 8f );
				_dirtyLevelsLookup = true;
			}
		}
		public float gammaLookup {
			get { return _gammaValue; }
			set {
				_gammaValue = Mathf.Clamp( value, 0.1f, 8f );
				_dirtyLevelsLookup = true;
			}
		}


		void Awake()
		{
			_levelsLookup = new Mat( 1, 256, CvType.CV_8U );
			_levelsLookupData = new byte[ (int) ( _levelsLookup.total() * _levelsLookup.channels() ) ];

			UpdateLevelsLookup();
		}


		void OnDestroy()
		{
			if( _camTexMat != null ) _camTexMat.release();
			if( _camTexGrayMat != null ) _camTexGrayMat.release();
		}


		void OnValidate()
		{
			_dirtyTexture = true;
			_dirtyLevelsLookup = true;
		}


		void Update()
		{
			if( !AdaptResources() || !_dirtyTexture ) return;

			// Convert texture to mat (If the texture looks right in Unity, it needs to be flipped for OpenCV).
			TrackingToolsHelper.TextureToMat( _inputTexture, true, ref _camTexMat, ref _tempTransferColors, ref _tempTransferTexture );

			// Convert to grayscale if more than one channel, else copy (and convert bit rate if necessary).
			TrackingToolsHelper.ColorMatToLumanceMat( _camTexMat, _camTexGrayMat );

			// Correct levels.
			if( !Mathf.Approximately( _blackValue, 0 ) || !Mathf.Approximately( _whiteValue, 1 ) || !Mathf.Approximately( _gammaValue, 1f ) ) {
				if( _dirtyLevelsLookup ) UpdateLevelsLookup();
				Core.LUT( _camTexGrayMat, _levelsLookup, _camTexGrayMat );
			}

			Utils.fastMatToTexture2D( _camTexGrayMat, _processedCameraTexture ); // Flips the texture vertically by default

			_outputTexture.Invoke( _processedCameraTexture );

			_dirtyTexture = false;
		}


		bool AdaptResources()
		{
			if( !_inputTexture ) return false;

			int w = _inputTexture.width;
			int h = _inputTexture.height;
			if( _processedCameraTexture != null && _processedCameraTexture.width == w && _processedCameraTexture.height == h ) return true;

			_camTexGrayMat = new Mat( h, w, CvType.CV_8UC1 );

			_processedCameraTexture = new Texture2D( w, h, GraphicsFormat.R8_UNorm, 0, TextureCreationFlags.None );
			_processedCameraTexture.name = "ProcessedCameraTex";

			return true;
		}



		void UpdateLevelsLookup()
		{
			float scale = 1 / ( _whiteValue - _blackValue );
			for( int i = 0; i < _levelsLookup.cols(); i++ ) {
				float value = ( i / 255f - _blackValue ) * scale;
				_levelsLookupData[ i ] = (byte) ( Mathf.Clamp01( Mathf.Pow( value, _gammaValue ) ) * 255 );
			}
			_levelsLookup.put( 0, 0, _levelsLookupData );
			_dirtyLevelsLookup = false;
		}
	}
}