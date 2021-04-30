/*
	Copyright © Carl Emil Carlsen 2018-2020
	http://cec.dk
*/


using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.UtilsModule;

public class DepthSensorPeopleTrackerOpenCV : MonoBehaviour
{
    [SerializeField] float _test= 1;
    [SerializeField] Camera _sensor;
    [SerializeField] int _width = 848;
    [SerializeField] int _height = 480;
    [SerializeField] TextureEvent _debugTextureEvent = null;

    byte[] _texData;
    Mat _depthRawMat;
    Mat _depthMat;
    Mat _depthPointsMat;
    Mat _sensorMatrix;
    //Mat _outputMat;

    Texture2D _unityOutputTexture;

    Mesh _mesh;
    Vector3[] _vertices;


    public byte[] textureData {
        set { _texData = value; }
        get {
            return _texData;
        }
    }


    [System.Serializable] public class TextureEvent : UnityEvent<Texture> { }


	void Start()
	{

	}


	void Update()
	{
        if( _texData == null ) return;

        // Ensure work resources.
        if( _depthMat == null ){
            // RealSense image pixels are single channel unsigned 16-bit integers.
            _depthRawMat = new Mat( _height, _width, CvType.CV_16UC1 ); // 16 bit unsigned one channel.
            _depthMat = new Mat( _height, _width, CvType.CV_16SC1 );
            _depthPointsMat = new Mat( _height, _width, CvType.CV_16S ); // Default is CV_32FC3, alernaitves are CV_16S, CV_32S or CV_32F.
            _sensorMatrix = new Mat( 4, 4, CvType.CV_64F );
            
            //_outputMat = new Mat( _height, _width, CvType.CV_16UC1 );
            _unityOutputTexture = new Texture2D( _width, _height, TextureFormat.R16, false, true );
        }

        // Read raw data into raw mat.
        MatUtils.copyToMat( _texData, _depthRawMat );

        // Convert to signed 16bit. Calib3d.reprojectImageTo3D only takes either CV_8UC1, CV_16SC1, CV_32S or CV_32F disparity image
        _depthRawMat.convertTo( _depthMat, _depthMat.type() );

        // Reproject depth mat into into a 3-channel mat of points.
        //Matrix4x4 m = _sensor.projectionMatrix.inverse * _sensor.worldToCameraMatrix;
        Matrix4x4 m = new Matrix4x4(
            new Vector4( 1, 0, 0, 0 ),
            new Vector4( 0, 1, 0, 0 ),
            new Vector4( 0, 0, _test, 0 ),
            new Vector4( 0, 0, 0, 1 )
        );
        m = m.inverse;
        //Debug.Log( m );
        _sensorMatrix.put( 0, 0, 
            m.m00, m.m01, m.m02, m.m03, 
            m.m10, m.m11, m.m12, m.m13, 
            m.m20, m.m21, m.m22, m.m23, 
            m.m30, m.m31, m.m32, m.m33
            //m.m00, m.m10, m.m20, m.m30, 
            //m.m01, m.m11, m.m21, m.m31, 
            //m.m02, m.m12, m.m22, m.m32, 
            //m.m03, m.m13, m.m23, m.m33
            //1, 0, 0, 0, 
            //0, 1, 0, 0, 
            //0, 0, 1, 0, 
            //0, 0, 0, 1
        );
        bool handleMissingValues = false;
        Calib3d.reprojectImageTo3D( _depthMat, _depthPointsMat, _sensorMatrix, handleMissingValues, _depthPointsMat.type() );

        // Copy z (blue) channel from _depthPointsMat to single channnel _depthRawMat.
        MatOfInt fromTo = new MatOfInt( 2, 0 );
        Core.mixChannels( new List<Mat>(new Mat[]{ _depthPointsMat } ), new List<Mat>(new Mat[]{ _depthMat } ), fromTo );

        // Normalize to CvType.CV_8U.
        //Core.normalize (imgDisparity16S, imgDisparity8U, 0, 255, Core.NORM_MINMAX, CvType.CV_8U);

        // Read back to Unity.
        Utils.fastMatToTexture2D( _depthMat, _unityOutputTexture );

        // Output.
        _debugTextureEvent.Invoke( _unityOutputTexture );
	}



    static Material lineMaterial;
    static void CreateLineMaterial()
    {
        if (!lineMaterial)
        {
            // Unity has a built-in shader that is useful for drawing
            // simple colored things.
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            // Turn on alpha blending
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            // Turn backface culling off
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            // Turn off depth writes
            lineMaterial.SetInt("_ZWrite", 0);
        }
    }



    void OnRenderObject()
    {
        CreateLineMaterial();

        int i;
        if( _mesh == null ){
            _mesh = new Mesh();
            _vertices = new Vector3[ _depthPointsMat.height() * _depthPointsMat.width() ];
            _mesh.vertices = _vertices;
            int[] indices = new int[_vertices.Length];
            for( i = 0; i < _vertices.Length; i++ ) indices[i] = i;
            _mesh.SetIndices( indices, MeshTopology.Points, 0 );
        }

        short[] data = new short[3];
        i = 0;
        for( int y = 0; y < _depthPointsMat.height(); y += 2 ) {
            for( int x = 0; x < _depthPointsMat.width(); x +=2 ) {
                _depthPointsMat.get( y, x, data );
                _vertices[i++].Set( data[0], data[1], data[2] );
            }
        }
        _mesh.vertices = _vertices;

        // Apply the line material
        lineMaterial.SetPass(0);

        Matrix4x4 m = Matrix4x4.Scale( Vector3.one );
        Graphics.DrawMeshNow( _mesh, m );

        /*
        GL.PushMatrix();
        //GL.Begin( GL. );
        
        float[] data = new float[3];
        for( int y = 0; y < _depthPointsMat.height(); y++ ) {
            for( int x = 0; x < _depthPointsMat.width(); x++ ) {
                _depthPointsMat.get( y, x, data );
                GL.Vertex3( data[0], data[1], data[2] );
            }
        }
        GL.End();
        GL.PopMatrix();
        */
    }
}