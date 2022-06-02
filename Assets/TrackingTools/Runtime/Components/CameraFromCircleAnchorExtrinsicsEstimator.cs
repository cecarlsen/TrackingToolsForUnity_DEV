/*
	Copyright © Carl Emil Carlsen 2018-2020
	http://cec.dk
*/

using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Experimental.Rendering;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.UnityUtils;

namespace TrackingTools
{
	public class CameraFromCircleAnchorExtrinsicsEstimator : MonoBehaviour
	{
		[SerializeField] bool _interactable = false;
		
		[Header("Input")]
		[SerializeField] Transform _anchorTransform = null;
		[SerializeField] Texture _cameraSourceTexture = null;
		[SerializeField] bool _flipSourceTextureVertically = true;
		[SerializeField] string _intrinsicsFileName = "DefaultCamera";
		[SerializeField] string _circleAnchorPointsFileName = "DefaultAnchorPoints";

		[Header("Output")]
		[SerializeField] Camera _targetCamera = null;
		
		[Header("UI")]
		[SerializeField] Canvas _canvas = null;
		[SerializeField] KeyCode _interactibaleHotKey = KeyCode.Alpha1;
		[SerializeField] KeyCode _resetHotKey = KeyCode.Backspace;
		[SerializeField] Font _font = null;
		[SerializeField] int _fontSize = 12;
		[SerializeField,Range(0f,1f)] float _alpha = 0.7f;
		[SerializeField,Range(1f,10f)] float _brightness = 1f;

		[SerializeField,Tooltip("Optional")] RectTransform _containerUI;


		ExtrinsicsCalibrator _calibrator;
		Intrinsics _intrinsics;
		
		bool _dirtyTexture = true;
		bool _dirtyPoints = true;
		
		AspectRatioFitter _aspectFitterUI;
		RawImage _rawImageUI;
		RectTransform _rawImageRect;
		CanvasGroup _wrapperGroup;
		RectTransform[] _userPointRects;
		Image[] _userPointImages;
		
		int _focusedPointIndex = -1;
		bool _isPointActive;

		Texture2D _tempTransferTexture; // For conversion from RenderTexture input.
		Color32[] _tempTransferColors;
		Point[] _anchorPointsImage;
		Point3[] _anchorPointsWorld;
		MatOfPoint2f _anchorPointsImageMat;
		MatOfPoint3f _anchorPointsWorldMat;
		Mat _cameraMatrix;
		MatOfDouble _distCoeffs;
		Mat _camTexMat;
		Mat _camTexGrayMat;
		Mat _camTexGrayUndistortMat;
		Texture2D _undistortedCameraTexture;

		Material _uiMaterial;

		
		const int pointCount = 5;
		
		static readonly Vector2[] defaultPointPositions = new Vector2[]{
			new Vector2( 0.5f, 0.5f ),
			new Vector2( 0.5f, 1.0f ),
			new Vector2( 1.0f, 0.5f ),
			new Vector2( 0.5f, 0.0f ),
			new Vector2( 0.0f, 0.5f ),
		};
		
		static readonly Color pointIdleColor = Color.cyan;
		static readonly Color pointHoverColor = Color.magenta;
		static readonly Color pointActiveColor = Color.white;

		static readonly string logPrepend = "<b>[" + nameof( CameraFromCircleAnchorExtrinsicsEstimator ) + "]</b> ";


		public Texture cameraSourceTexture {
			get { return _cameraSourceTexture; }
			set {
				_cameraSourceTexture = value;
				_dirtyTexture = true;
			}
		}
		
		public bool interactable {
			get { return _interactable; }
			set {
				_interactable = value;
				if( _containerUI && Application.isPlaying ) _containerUI.gameObject.SetActive( _interactable );
			}
		}

		public float alpha {
			get { return _brightness; }
			set { _alpha = Mathf.Clamp01( value ); }
		}

		public float brightness {
			get { return _brightness; }
			set {
				_brightness = value;
				if( _uiMaterial ) _uiMaterial.SetFloat( "_Brightness", _brightness );
			}
		}


		void Awake()
		{
			_calibrator = new ExtrinsicsCalibrator();

			// Create UI.
			if( !_containerUI ) {
				_containerUI = new GameObject( "CameraPoser" ).AddComponent<RectTransform>();
				_containerUI.transform.SetParent( _canvas.transform );
			}
			_wrapperGroup = _containerUI.GetComponent<CanvasGroup>();
			if( !_wrapperGroup ) _wrapperGroup = _containerUI.gameObject.AddComponent<CanvasGroup>();
			Image backgroundImage = new GameObject( "Background" ).AddComponent<Image>();
			backgroundImage.transform.SetParent( _containerUI.transform );
			_rawImageUI = new GameObject( "CameraImage" ).AddComponent<RawImage>();
			_rawImageUI.transform.SetParent( _containerUI.transform );
			_rawImageRect = _rawImageUI.GetComponent<RectTransform>();
			_uiMaterial = new Material( Shader.Find( "Hidden/SingleChannelTexture" ) );
			_rawImageUI.material = _uiMaterial;
			_aspectFitterUI = _rawImageUI.gameObject.AddComponent<AspectRatioFitter>();
			_aspectFitterUI.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
			backgroundImage.color = Color.black;
			ExpandRectTransform( _containerUI );
			ExpandRectTransform( backgroundImage.GetComponent<RectTransform>() );
			ExpandRectTransform( _rawImageRect );
			_userPointRects = new RectTransform[pointCount];
			_userPointImages = new Image[pointCount];
			for( int p = 0; p < pointCount; p++ ){
				GameObject pointObject = new GameObject( "Point" + p );
				pointObject.transform.SetParent( _rawImageRect );
				Image pointImage = pointObject.AddComponent<Image>();
				pointImage.color = Color.cyan;
				RectTransform pointRect = pointObject.GetComponent<RectTransform>();
				pointRect.sizeDelta = Vector2.one * 5;
				SetAnchoredPosition( pointRect, defaultPointPositions[p] );
				pointRect.anchoredPosition = Vector3.zero;
				Text pointLabel = new GameObject( "Label" ).AddComponent<Text>();
				pointLabel.text = p.ToString();
				pointLabel.transform.SetParent( pointRect );
				pointLabel.rectTransform.anchoredPosition = Vector2.zero;
				pointLabel.rectTransform.sizeDelta = new Vector2( _fontSize, _fontSize ) * 2;
				pointLabel.font = _font;
				pointLabel.fontSize = _fontSize;
				_userPointRects[p] = pointRect;
				_userPointImages[p] = pointImage;
			}

			// Hide.
			if( !_interactable ) _containerUI.transform.gameObject.SetActive( false );
			
			// Prepare OpenCV.
			_anchorPointsImage = new Point[pointCount];
			_anchorPointsWorld = new Point3[pointCount];
			_anchorPointsImageMat = new MatOfPoint2f();
			_anchorPointsWorldMat = new MatOfPoint3f();
			_anchorPointsImageMat.alloc( pointCount );
			_anchorPointsWorldMat.alloc( pointCount );
			for( int p = 0; p < pointCount; p++ ) {
				_anchorPointsImage[p] = new Point();
				_anchorPointsWorld[p] = new Point3();
			}

			// Load files.
			if( !Intrinsics.TryLoadFromFile( _intrinsicsFileName, out _intrinsics ) ) {
				enabled = false;
				Debug.LogError( logPrepend + "Missing instrinsics file: '" + _intrinsicsFileName + "'\n" );
				return;
			}
			LoadCircleAnchorPoints();
			
			// Update variables.
			OnValidate();
		}


		void OnDestroy()
		{
			if( _calibrator != null ) _calibrator.Release();
			if( _tempTransferTexture ) Destroy( _tempTransferTexture );
			if( _anchorPointsImageMat != null ) _anchorPointsImageMat.Dispose();
			if( _anchorPointsWorldMat != null ) _anchorPointsWorldMat.Dispose();
			if( _distCoeffs != null ) _distCoeffs.Dispose();
			if( _cameraMatrix != null ) _cameraMatrix.Dispose();
			if( _camTexMat != null ) _camTexMat.Dispose();
			if( _camTexGrayMat != null ) _camTexGrayMat.Dispose();
			if( _camTexGrayUndistortMat != null ) _camTexGrayUndistortMat.Dispose();
		}


		void OnDisable()
		{
			
		}


		void Update()
		{
			if( Input.GetKeyDown( _interactibaleHotKey ) ){
				if( !interactable && _cameraSourceTexture == null ) {
					Debug.LogWarning( logPrepend + "Missing camera texture.\n" );
					return;
				}
				interactable = !interactable;
			}

			if( !_cameraSourceTexture ) return;

			// Adapt resources.
			AdaptResources();

			if( _dirtyTexture )
			{
				// Update mat texture ( If the texture looks correct in Unity, then it needs to be flipped for OpenCV ).
				TrackingToolsHelper.TextureToMat( _cameraSourceTexture, !_flipSourceTextureVertically, ref _camTexMat, ref _tempTransferColors, ref _tempTransferTexture );

				// Convert to black & white.
				TrackingToolsHelper.ColorMatToLumanceMat( _camTexMat, _camTexGrayMat );

				// Undistort.
				Calib3d.undistort( _camTexGrayMat, _camTexGrayUndistortMat, _cameraMatrix, _distCoeffs );

				// Back to Unity.
				Utils.fastMatToTexture2D( _camTexGrayUndistortMat, _undistortedCameraTexture ); // Will flip back to Unity orientation by default.
				_dirtyTexture = false;
			}

			if( _interactable ){
				UpdateInteraction();

				// UI.
				_wrapperGroup.alpha = _alpha;
			}

			if( _dirtyPoints )
			{
				UpdateCameraTransform();
				_dirtyPoints = false;
			}
		}


		void OnValidate()
		{
			interactable = _interactable;
			cameraSourceTexture = _cameraSourceTexture;
			alpha = _alpha;
			brightness = _brightness;

			_fontSize = Mathf.Max( 0, _fontSize );
		}


		void AdaptResources()
		{
			int w = _cameraSourceTexture.width;
			int h = _cameraSourceTexture.height;
			if( _undistortedCameraTexture != null && _undistortedCameraTexture.width == w && _undistortedCameraTexture.height == h ) return;

			_camTexMat = TrackingToolsHelper.GetCompatibleMat( _cameraSourceTexture );
			_camTexGrayMat = new Mat( h, w, CvType.CV_8UC1 );
			_camTexGrayUndistortMat = new Mat( h, w, CvType.CV_8UC1 );

			_undistortedCameraTexture = new Texture2D( w, h, GraphicsFormat.R8_UNorm, 0, TextureCreationFlags.None );
			_undistortedCameraTexture.name = "UndistortedCameraTex";

			// Apply intrinsics.
			_intrinsics.ApplyToToOpenCV( ref _cameraMatrix, ref _distCoeffs );
			_intrinsics.ApplyToUnityCamera( _targetCamera );

			// Update UI.
			_rawImageUI.texture = _undistortedCameraTexture;
			_aspectFitterUI.aspectRatio = w / (float) h;
		}


		void UpdateInteraction()
		{
			if( Input.GetKeyDown( _resetHotKey ) ){
				Reset();
				_dirtyPoints = true;
			}
			
			bool changed = false;

			// Get anchored mouse position withint image rect.
			Vector2 mousePos;
			RectTransformUtility.ScreenPointToLocalPointInRectangle( _rawImageRect, Input.mousePosition, _canvas.worldCamera, out mousePos );
			mousePos = LocalPixelPositionToAnchoredPosition( mousePos, _rawImageRect );
			
			// Deselect.
			if( Input.GetMouseButtonUp( 0 ) && _isPointActive && _focusedPointIndex != -1){
				SetAnchoredPosition( _userPointRects[_focusedPointIndex], mousePos );
				changed = true;
				_userPointImages[_focusedPointIndex].color = pointHoverColor;
				_isPointActive = false;
			} else {
				if( _isPointActive ){
					// Update position.
					SetAnchoredPosition( _userPointRects[_focusedPointIndex], mousePos );
					changed = true;
				} else {
					// Find nearest point.
					float sqrDistMin = float.MaxValue;
					int nearestPointIndex = -1;
					for( int p = 0; p < pointCount; p++ ){
						Vector2 towardsPoint = _userPointRects[p].anchorMin - mousePos;
						float sqrDist = Vector2.Dot( towardsPoint, towardsPoint );
						if( sqrDist < sqrDistMin ) {
							nearestPointIndex = p;
							sqrDistMin = sqrDist;
						}
					}
					if( _focusedPointIndex != -1 ) _userPointImages[_focusedPointIndex].color = pointIdleColor;
					if( Input.GetMouseButtonDown( 0 ) ) {
						// Select.
						_userPointImages[nearestPointIndex].color = pointActiveColor;
						_isPointActive = true;
					} else {
						// Hover.
						_userPointImages[nearestPointIndex].color = pointHoverColor;
					}
					_focusedPointIndex = nearestPointIndex;
				}
			}
		
			if( changed ) _dirtyPoints = true;
		}
	
	
		void UpdateCameraTransform()
		{
			// Update points.
			for( int p = 0; p < pointCount; p++ ){
				Vector2 posImage = _userPointRects[p].anchorMin;
				posImage.y = 1f - posImage.y; // OpenCv pixels are flipped vertically.
				posImage.Scale( new Vector2( _cameraSourceTexture.width, _cameraSourceTexture.height ) );
				_anchorPointsImage[p].set( new double[]{ posImage.x, posImage.y } );
				Vector3 posWorld = _anchorTransform.TransformPoint( defaultPointPositions[p] - Vector2.one * 0.5f );
				_anchorPointsWorld[p].set( new double[]{ posWorld.x, posWorld.y, posWorld.z } );
			}
			_anchorPointsImageMat.fromArray( _anchorPointsImage );
			_anchorPointsWorldMat.fromArray( _anchorPointsWorld ); 
			
			_calibrator.UpdateExtrinsics( _anchorPointsWorldMat,_anchorPointsImageMat, _intrinsics );
			
			_calibrator.extrinsics.ApplyToTransform( _targetCamera.transform );

			// Save.
			SaveCircleAnchorPoints();
		}
	

		public void Reset()
		{
			for( int p = 0; p < pointCount; p++ ){
				SetAnchoredPosition( _userPointRects[p], defaultPointPositions[p] );
			}
		}


		void LoadCircleAnchorPoints()
		{
			string filePath = TrackingToolsConstants.circleAnchorsDirectoryPath + "/" + _circleAnchorPointsFileName + ".json";
			if( !File.Exists( filePath ) ) return;

			string json = File.ReadAllText( filePath );
			CircleAnchorData data = JsonUtility.FromJson<CircleAnchorData>( json );
			for( int p = 0; p < pointCount; p++ ) SetAnchoredPosition( _userPointRects[ p ], data.points[ p ] );
			_dirtyPoints = true;
		}


		void SaveCircleAnchorPoints()
		{
			if( !Directory.Exists( TrackingToolsConstants.circleAnchorsDirectoryPath ) ) Directory.CreateDirectory( TrackingToolsConstants.circleAnchorsDirectoryPath );
			string filePath = TrackingToolsConstants.circleAnchorsDirectoryPath + "/" + _circleAnchorPointsFileName + ".json";

			CircleAnchorData data = new CircleAnchorData();
			for( int p = 0; p < pointCount; p++ ) data.points[p] = _userPointRects[ p ].anchorMin;

			File.WriteAllText( filePath, JsonUtility.ToJson( data ) );

			//Debug.Log( logPrepend + "Saved anchor points to file.\n" + filePath );
		}


		static void ExpandRectTransform( RectTransform rectTransform )
		{
			rectTransform.anchorMax = Vector2.one;
			rectTransform.anchorMin = Vector2.zero;
			rectTransform.anchoredPosition = Vector2.zero;
			rectTransform.sizeDelta = Vector2.zero;
		}


		static void SetAnchoredPosition( RectTransform rectTransform, Vector2 pos )
		{
			rectTransform.anchorMin = pos;
			rectTransform.anchorMax = pos;
			rectTransform.anchoredPosition = Vector3.zero;
		}


		static Vector2 LocalPixelPositionToAnchoredPosition( Vector2 pos, RectTransform rectTransform )
		{
			UnityEngine.Rect rect = rectTransform.rect;
			return new Vector2( ( pos.x - rect.x ) / rect.width, ( pos.y - rect.y ) / rect.height );
		}
	}
}