/*
using System;
using UnityEngine;
using UnityEngine.Events;
using Intel.RealSense;
using CEC.Synths;

public class RealSensePointCloudBlobTracker : MonoBehaviour
{
    [SerializeField] Transform _sensorTransform;
    [SerializeField] Bounds _bounds = new Bounds( new Vector3( 0, 0.5f, 0 ), Vector3.one );
    [SerializeField] float _smoothDuration = 0.1f;
    [SerializeField] Vector3 _blobOffset;
    [SerializeField] Float4BufferEvent _outPositions;
    [SerializeField] Vector3Event _outBlobCenter;

    PointCloud _pointCloud = new PointCloud();
    Points.Vertex[] _vertices;

    Float4Buffer _positionBuffer;
    Vector4[] _positionBufferData;

    Vector3 _blobCenter;
    Vector3 _blobCenterVel;


    [Serializable] public class Vector3Event : UnityEvent<Vector3>{}


    
    void Start()
    {
        RealSensor.Instance.onNewSampleSet += OnFrames; 
    }
    

    void OnFrames( FrameSet frames )
    {

        using( Points points = _pointCloud.Calculate( frames.DepthFrame ) )
        {
            if( points == null ) return;

            if( _vertices == null || _vertices.Length != points.Count ) _vertices = new Points.Vertex[points.Count];
            
            points.CopyTo( _vertices );
        }
    }



    void LateUpdate()
    {
        if( _vertices == null ) return;


        if( !_positionBuffer || _positionBuffer.capacity != _vertices.Length ){
            if( _positionBuffer ) Destroy( _positionBuffer );
            _positionBuffer = Float4Buffer.Create( "PointCloud", _vertices.Length );
            _positionBufferData = new Vector4[_vertices.Length];
        }

        Vector3 newBlobCenter = Vector3.zero;
        int validPointCount = 0;
        Matrix4x4 sensorMatrix = _sensorTransform.localToWorldMatrix * Matrix4x4.Rotate( Quaternion.Euler(0,0,180) );
        for( int i = 0; i < _vertices.Length; i++ ){
            Points.Vertex vert = _vertices[i];

            Vector3 point = sensorMatrix.MultiplyPoint3x4( new Vector3( vert.x, vert.y, vert.z ) );
            if( _bounds.Contains( point ) ){
                newBlobCenter += point;
                validPointCount++;
            }

            _positionBufferData[i].Set( point.x, point.y, point.z, 1 );
        }
        if( validPointCount > 1 ) newBlobCenter /= (float) validPointCount;
        _blobCenter = Vector3.SmoothDamp( _blobCenter, newBlobCenter, ref _blobCenterVel, _smoothDuration, float.MaxValue, Time.deltaTime );

        //Vector3 fixedBlobCenter = new Vector3( -_blobCenter.z, _blobCenter.y, -_blobCenter.x );
        Vector3 fixedBlobCenter = new Vector3( _blobCenter.x, _blobCenter.y, -_blobCenter.z );

        _positionBuffer.SetData( _positionBufferData );

        if( _positionBuffer ) _outPositions.Invoke( _positionBuffer );
        _outBlobCenter.Invoke( fixedBlobCenter + _blobOffset );
    }


    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube( _bounds.center, _bounds.size );
    }
}
*/