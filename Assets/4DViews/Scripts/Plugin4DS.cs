#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID || UNITY_WSA
#define USE_NATIVE_LIB
#endif

using UnityEngine;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace unity4dv
{

    public enum OUT_RANGE_MODE
    {
        Loop = 0,
        Reverse = 1,
        Stop = 2,
        Hide = 3
    }

    public enum SOURCE_TYPE
    {
        Files = 0,
        Network = 1
    }





    interface Plugin4DSInterface
    {
        void Initialize();
        void Uninitialize();

        void Play(bool on);
        void GotoFrame(int frame);
    }


    public class Plugin4DS : MonoBehaviour, Plugin4DSInterface
    {
#region Properties
        //-----------------------------//
        //-  PROPERTIES               -//
        //-----------------------------//
        public int CurrentFrame { get { return GetCurrentFrame(); } set { GotoFrame((int)value); } }
        public float Framerate { get { return GetFrameRate(); } }
        public int SequenceNbOfFrames { get { return GetSequenceNbFrames(); } }
        public int ActiveNbOfFrames { get { return GetActiveNbFrames(); } }
        public int FirstActiveFrame { get { return (int)_activeRangeMin; } set { _activeRangeMin = (float)value; } }
        public int LastActiveFrame { get { return (int)_activeRangeMax; } set { _activeRangeMax = (float)value; } }
        public TextureFormat TextureEncoding { get { return GetTextureFormat(); } }

        public bool AutoPlay { get { return _autoPlay; } set { _autoPlay = value; } }
        public bool IsPlaying { get { return _isPlaying; } set { _isPlaying = value; } }
        public bool IsInitialized { get { return _isInitialized; } }

        public string SequenceName { get { return _sequenceName; } set { _sequenceName = value; } }
        public string SequenceDataPath { get { return _mainDataPath; } set { _mainDataPath = value; } }
        public string ServerAddress { get { return _serverAddress; } set { _serverAddress = value; } }
        public int ServerPort { get { return _serverPort; } set { _serverPort = value; } }

        public OUT_RANGE_MODE OutOfRangeMode { get { return _outRangeMode; } set { SetOutRangeMode(value); } }

        public bool ComputeNormals { get { return _computeNormals; } set { _computeNormals = value; } }
        public int PreviewFrame { get { return _previewFrame; } set { _previewFrame = value; } }

#endregion

#region Events
        //-----------------------------//
        //-  EVENTS                   -//
        //-----------------------------//
        public delegate void EventFDV();
        public event EventFDV onNewModel;
        public event EventFDV onModelNotFound;
        public event EventFDV onOutOfRange;
        #endregion

#region classMembers
        //-----------------------------//
        //- Class members declaration -//
        //-----------------------------//

        //Data source
        public SOURCE_TYPE _sourceType = SOURCE_TYPE.Files;

        //Path containing the 4DR data (edited in the unity editor panel)
        [SerializeField]
        private string _sequenceName;
        [SerializeField]
        private string _mainDataPath;
        public bool _dataInStreamingAssets = false;

        //Network configuration
        [SerializeField]
        private string _serverAddress = "127.0.0.1";
        [SerializeField]
        private int _serverPort = 4444;

        //Playback
        [SerializeField]
        private bool _autoPlay = true;
        [SerializeField]
        private OUT_RANGE_MODE _outRangeMode = OUT_RANGE_MODE.Loop;

        //Field used for animation timeline
        [SerializeField]
        private float _animationFrame = -1.0f;

        //Normals computing
        [SerializeField]
        private bool _computeNormals = false;

        //Buffer & Cache
        public bool _bufferMode = true;
        public int _bufferSize = 10;
        public int _cachingMode = 0;

        //Active Range
        [SerializeField]
        private float _activeRangeMin = 0;
        [SerializeField]
        private float _activeRangeMax = -1;

        //Infos
        public bool _debugInfo = false;
        private float _decodingFPS = 0f;
        private int _lastDecodingId = 0;
        private System.DateTime _lastDecodingTime;
        private float _updatingFPS = 0f;
        private int _lastUpdatingId = 0;
        private System.DateTime _lastUpdatingTime;
        private int _playCurrentFrame = 0;
        private System.DateTime _playDate;

        //4D source
        private DataSource4DS _dataSource = null;
        [SerializeField]
        private int _lastModelId = -1;

        //Mesh and texture objects
        private Mesh[] _meshes = null;
        private Texture2D[] _textures = null;
        private MeshFilter _meshComponent;
        private Renderer _rendererComponent;

        //Receiving geometry and texture buffers
        private Vector3[] _newVertices;
        private Vector2[] _newUVs;
        private int[] _newTriangles;
        private byte[] _newTextureData;
        private Vector3[] _newNormals = null;
        private GCHandle _newVerticesHandle;
        private GCHandle _newUVsHandle;
        private GCHandle _newTrianglesHandle;
        private GCHandle _newTextureDataHandle;
        private GCHandle _newNormalsHandle;

        //Mesh and texture multi-buffering (optimization)
        private int _nbGeometryBuffers = 2;
        private int _currentGeometryBuffer;
        private int _nbTextureBuffers = 2;
        private int _currentTextureBuffer;

        //time a latest update
        //private float           _prevUpdateTime=0.0f;
        private bool _newMeshAvailable = false;
        private bool _isSequenceTriggerON = false;
        private float _triggerRate = 0.3f;

        //pointer to the mesh Collider, if present (=> will update it at each frames for collisions)
        private MeshCollider _meshCollider;

        //Has the plugin been initialized
        [SerializeField]
        private bool _isInitialized = false;
        [SerializeField]
        private bool _isPlaying = false;
#if UNITY_EDITOR
        private bool _lastEditorMode = false;
#endif
        [SerializeField]
        private int _previewFrame = 0;
        public System.DateTime last_preview_time = System.DateTime.Now;
        private int _pausedFrame;
        [SerializeField]
        private int _nbFrames = 0;

        private int _nbVertices;
        private int _nbTriangles;

        private const int MAX_SHORT = 65535;

        private bool _preview = false;
        #endregion

#region methods
        //-----------------------------//
        //- Class methods implement.  -//
        //-----------------------------//


        void Awake()
        {
            if ((_sourceType == SOURCE_TYPE.Files && _sequenceName != "") ||
                (_sourceType == SOURCE_TYPE.Network && _serverAddress != ""))
                Initialize();
            //Hide preview mesh
            if (_meshComponent != null)
                _meshComponent.mesh = null;
#if UNITY_EDITOR
#if UNITY_2017_3_OR_NEWER
            EditorApplication.playModeStateChanged -= HandleOnPlayModeChanged;
#else
            EditorApplication.playmodeStateChanged -= HandleOnPlayModeChanged;
#endif
#endif
        }


        public void Initialize()
        {
            //Initialize already called successfully
            if (_isInitialized == true)
                return;

            if (_dataSource == null)
            {
                if (_sourceType == SOURCE_TYPE.Network)
                {
                    //Creates data source from server ip
                    _dataSource = DataSource4DS.CreateNetworkSource(_serverAddress, _serverPort);
                }
                else
                {
                    //Creates data source from the given path (directory or sequence.xml)
                    _dataSource = DataSource4DS.CreateDataSource(_sequenceName, _dataInStreamingAssets, _mainDataPath, (int)_activeRangeMin, (int)_activeRangeMax, _outRangeMode);
                }
                if (_dataSource == null)
                {
                    if (onModelNotFound != null)
                        onModelNotFound();
                    return;
                }
            }

            _lastModelId = -1;

            _meshComponent = GetComponent<MeshFilter>();
            _rendererComponent = GetComponent<Renderer>();
            _meshCollider = GetComponent<MeshCollider>();

            Bridge4DS.SetComputeNormals(_dataSource.FDVUUID, _computeNormals);

            //Allocates geometry buffers
            AllocateGeometryBuffers(ref _newVertices, ref _newUVs, ref _newNormals, ref _newTriangles, _dataSource.MaxVertices, _dataSource.MaxTriangles);

            //Allocates texture pixel buffer
            int pixelBufferSize = _dataSource.TextureSize * _dataSource.TextureSize / 2;    //default is 4 bpp
            if (_dataSource.TextureFormat == TextureFormat.PVRTC_RGB2 )  //pvrtc2 is 2bpp
                pixelBufferSize /= 2;
            if (_dataSource.TextureFormat == TextureFormat.ASTC_RGBA_8x8)
            {
                int blockSize = 8;
                int xblocks = (_dataSource.TextureSize + blockSize - 1) / blockSize;
                pixelBufferSize = xblocks * xblocks * 16;
            }
            _newTextureData = new byte[pixelBufferSize];

            //Gets pinned memory handle
#if USE_NATIVE_LIB
            _newVerticesHandle = GCHandle.Alloc(_newVertices, GCHandleType.Pinned);
            _newUVsHandle = GCHandle.Alloc(_newUVs, GCHandleType.Pinned);
            _newTrianglesHandle = GCHandle.Alloc(_newTriangles, GCHandleType.Pinned);
            _newTextureDataHandle = GCHandle.Alloc(_newTextureData, GCHandleType.Pinned);
            if (_computeNormals)
            {
                _newNormalsHandle = GCHandle.Alloc(_newNormals, GCHandleType.Pinned);
            }

#endif

            //Allocates objects buffers for double buffering
            _meshes = new Mesh[_nbGeometryBuffers];
            _textures = new Texture2D[_nbTextureBuffers];

            for (int i = 0; i < _nbGeometryBuffers; i++)
            {
                //Mesh
                Mesh mesh = new Mesh();
                mesh.MarkDynamic(); //Optimize mesh for frequent updates. Call this before assigning vertices. 
                mesh.vertices = _newVertices;
                mesh.uv = _newUVs;
                mesh.triangles = _newTriangles;
                if (_computeNormals)
                    mesh.normals = _newNormals;

                Bounds newBounds = mesh.bounds;
                newBounds.extents = new Vector3(10, 10, 10);
                mesh.bounds = newBounds;
                _meshes[i] = mesh;
            }


            for (int i = 0; i < _nbTextureBuffers; i++)
            {
                //Texture
                Texture2D texture = new Texture2D(_dataSource.TextureSize, _dataSource.TextureSize, _dataSource.TextureFormat, false);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Point;
                texture.Apply(); //upload to GPU
                _textures[i] = texture;
            }

            Bridge4DS.SetBuffering(_dataSource.FDVUUID, _bufferMode, _bufferSize);

            //Bridge4DS.SetCachingMode(_dataSource.FDVUUID, _cachingMode);
            _currentGeometryBuffer = _currentTextureBuffer = 0;

            if (_autoPlay)
                Play(true);

            _isInitialized = true;
        }


        public void Uninitialize()
        {
            if (_dataSource == null)
                return;

            StopCoroutine("SequenceTrigger");

            //Releases sequence
            Bridge4DS.DestroySequence(_dataSource.FDVUUID);
            _dataSource = null;

            //Releases memory
            _newVerticesHandle.Free();
            _newUVsHandle.Free();
            _newTrianglesHandle.Free();
            _newTextureDataHandle.Free();
            if (_computeNormals)
                _newNormalsHandle.Free();

            if (!_preview)
            {
                for (int i = 0; i < _nbGeometryBuffers; i++)
                    Destroy(_meshes[i]);
                _meshes = null;
                for (int i = 0; i < _nbTextureBuffers; i++)
                    Destroy(_textures[i]);
                _textures = null;
            }

            _newVertices = null;
            _newUVs = null;
            _newTriangles = null;
            _newNormals = null;
            _newTextureData = null;

            _isSequenceTriggerON = false;
            _isInitialized = false;

#if UNITY_EDITOR
#if UNITY_2017_3_OR_NEWER
            EditorApplication.playModeStateChanged -= HandleOnPlayModeChanged;
#else
            EditorApplication.playmodeStateChanged -= HandleOnPlayModeChanged;
#endif
#endif
        }


        void OnDestroy()
        {
            Uninitialize();
        }



        void Start()
        {
            if (_isInitialized == false &&
                ((_sourceType == SOURCE_TYPE.Files && _sequenceName != "") ||
                    (_sourceType == SOURCE_TYPE.Network && _serverAddress != "")))  //recall initialize if it was not succsefull yet (webGL)
                Initialize();

            if (_dataSource == null)
                return;

            //launch sequence play
            if (_autoPlay)
            {
                Play(true);
            }

        }



        //Called every frame
        //Get the geometry from the plugin and update the unity gameobject mesh and texture
        void Update()
        {
            if (_isInitialized == false &&
                ((_sourceType == SOURCE_TYPE.Files && _sequenceName != "") ||
                    (_sourceType == SOURCE_TYPE.Network && _serverAddress != "")))  //recall initialize if it was not succsefull yet (webGL)
                Initialize();

            if (_dataSource == null)
                return;
            //everything is in UpdateMesh(), which called by the SequenceTrigger coroutine

            if (_animationFrame >= 0)
                CurrentFrame = (int)_animationFrame;

            if (_newMeshAvailable)
            {
                //Get current object buffers (double buffering)
                Mesh mesh = _meshes[_currentGeometryBuffer];
                Texture2D texture = _textures[_currentTextureBuffer];

                //Optimize mesh for frequent updates. Call this before assigning vertices.
                //Seems to be useless :(
                mesh.MarkDynamic();

                //Update geometry
                mesh.vertices = _newVertices;
                mesh.uv = _newUVs;
                if (_nbTriangles == 0)  //case empty mesh
                    mesh.triangles = null;
                else
                    mesh.triangles = _newTriangles;
                if (_computeNormals)
                    mesh.normals = _newNormals;
                else
                    mesh.normals = null;
                mesh.UploadMeshData(false); //Good optimization ! nbGeometryBuffers must be = 1

                //Update texture
                texture.LoadRawTextureData(_newTextureData);
                texture.Apply();

                //Assign current mesh buffers and texture
                _meshComponent.sharedMesh = mesh;
#if UNITY_EDITOR
                var tempMaterial = new Material(_rendererComponent.sharedMaterial);
                tempMaterial.mainTexture = texture;
                _rendererComponent.sharedMaterial = tempMaterial;
#else
                _rendererComponent.material.mainTexture = texture;
#endif

                //Switch buffers
                _currentGeometryBuffer = (_currentGeometryBuffer + 1) % _nbGeometryBuffers;
                _currentTextureBuffer = (_currentTextureBuffer + 1) % _nbTextureBuffers;

                //Send event
                if (onNewModel != null)
                    onNewModel();

                _newMeshAvailable = false;

                if (_meshCollider && _meshCollider.enabled)
                    _meshCollider.sharedMesh = mesh;
                //_updateCollider = !_updateCollider;

                if (_debugInfo)
                {
                    double timeInMSeconds = System.DateTime.Now.Subtract(_lastUpdatingTime).TotalMilliseconds;
                    _lastUpdatingId++;
                    if (timeInMSeconds > 500f)
                    {
                        _updatingFPS = (float)((float)(_lastUpdatingId) / timeInMSeconds * 1000f);
                        _lastUpdatingTime = System.DateTime.Now;
                        _lastUpdatingId = 0;
                    }
                }
            }
        }


        private void UpdateMesh()
        {
            if (_dataSource == null)
                return;

            //Get the new model
#if USE_NATIVE_LIB
            System.IntPtr normalAddr = System.IntPtr.Zero;
            if (_computeNormals)
            {
                if (_newNormals == null)
                {
                    _newNormals = new Vector3[_dataSource.MaxVertices];
                    _newNormalsHandle = GCHandle.Alloc(_newNormals, GCHandleType.Pinned);
                }
                normalAddr = _newNormalsHandle.AddrOfPinnedObject();
            }

            int modelId = Bridge4DS.UpdateModel(_dataSource.FDVUUID,
                                                      _newVerticesHandle.AddrOfPinnedObject(),
                                                      _newUVsHandle.AddrOfPinnedObject(),
                                                      _newTrianglesHandle.AddrOfPinnedObject(),
                                                      _newTextureDataHandle.AddrOfPinnedObject(),
                                                      normalAddr,
                                                      _lastModelId,
                                                      ref _nbVertices,
                                                      ref _nbTriangles);

#endif

            //look for end of range event
            if (Bridge4DS.OutOfRangeEvent(_dataSource.FDVUUID))
            {   //Send event
                if (onOutOfRange != null)
                {
                    onOutOfRange();
                }

                if (_outRangeMode == OUT_RANGE_MODE.Stop)
                    Play(false);

                if (_outRangeMode == OUT_RANGE_MODE.Hide)
                {
                    Play(false);
                    _meshComponent.sharedMesh.Clear();
                }
            }

            //Check if there is model
            if (!_newMeshAvailable)
                _newMeshAvailable = (modelId != -1 && modelId != _lastModelId);

            if (modelId == -1) modelId = _lastModelId;
            else _lastModelId = modelId;

            if (_debugInfo)
            {
                double timeInMSeconds = System.DateTime.Now.Subtract(_lastDecodingTime).TotalMilliseconds;
                if (_lastDecodingId == 0 || timeInMSeconds > 500f)
                {
                    _decodingFPS = (float)((double)(Mathf.Abs((float)(modelId - _lastDecodingId))) / timeInMSeconds) * 1000f;
                    _lastDecodingTime = System.DateTime.Now;
                    _lastDecodingId = modelId;
                }
            }


        }


        //manage the UpdateMesh() call to have it triggered by the sequence framerate
        private IEnumerator SequenceTrigger()
        {
            float duration = (_triggerRate / _dataSource.FrameRate);

            //infinite loop to keep executing this coroutine
            while (true)
            {
                UpdateMesh();
                yield return new WaitForSeconds(duration);
            }
        }

        //set the sequence on pause if the application looses the focus
        void OnApplicationPause(bool pauseStatus)
        {
            PlayOnFocus(!pauseStatus);
        }

        void OnEnable()
        {
            PlayOnFocus(true);
        }

        void OnDisable()
        {
            PlayOnFocus(false);
        }

        void PlayOnFocus(bool on)
        {

            if (on && _isPlaying)
            {
                if (_isSequenceTriggerON == false)
                {
                    Bridge4DS.Play(_dataSource.FDVUUID, on);
                    StartCoroutine("SequenceTrigger");
                    _isSequenceTriggerON = true;
                }
            }
            else
            {
                if (_isSequenceTriggerON == true)
                {
                    Bridge4DS.Play(_dataSource.FDVUUID, on);
                    StopCoroutine("SequenceTrigger");
                    _isSequenceTriggerON = false;
                }
            }
        }

#if UNITY_EDITOR
#if UNITY_2017_3_OR_NEWER
        void HandleOnPlayModeChanged(PlayModeStateChange state)
#else
        void HandleOnPlayModeChanged()
#endif
        {
            if (EditorApplication.isPaused && _lastEditorMode)
            {
                _pausedFrame++;
                bool isCurrentlyPlaying = _isPlaying;
                GotoFrame(_pausedFrame % GetSequenceNbFrames()); //GotoFrame pauses automatically the playback
                _isPlaying = isCurrentlyPlaying;                 //so we need to restore the playback mode
                Debug.Log("CURRENT FRAME " + CurrentFrame);
            }
            else
            {
                _pausedFrame = CurrentFrame;
                OnApplicationPause(EditorApplication.isPaused);
                _lastEditorMode = EditorApplication.isPaused;
            }
        }
#endif


        //Public functions
        public void Play(bool on)
        {
            if (on)
            {
                if (_isSequenceTriggerON == false)
                {
                    Bridge4DS.Play(_dataSource.FDVUUID, on);
                    StartCoroutine("SequenceTrigger");
                    _isSequenceTriggerON = true;
                    _playCurrentFrame = CurrentFrame;
                    _playDate = System.DateTime.Now;
                }
            }
            else
            {
                if (_isSequenceTriggerON == true)
                {
                    Bridge4DS.Play(_dataSource.FDVUUID, on);
                    StopCoroutine("SequenceTrigger");
                    _isSequenceTriggerON = false;
                }
            }
            _isPlaying = on;
        }


        public void GotoFrame(int frame)
        {
            Bridge4DS.GotoFrame(_dataSource.FDVUUID, frame);
            _isPlaying = false;
            UpdateMesh();
        }


        private int GetSequenceNbFrames()
        {
            if (_dataSource != null)
                return Bridge4DS.GetSequenceNbFrames(_dataSource.FDVUUID);
            else
                return _nbFrames;
        }

        private int GetActiveNbFrames()
        {
            return (int)_activeRangeMax - (int)_activeRangeMin + 1;
        }

        private int GetCurrentFrame()
        {
            if (_lastModelId < 0)
                return 0;
            else
                return _lastModelId;
        }

        private float GetFrameRate()
        {
            return (_dataSource == null) ? 0.0f : _dataSource.FrameRate;
        }

        private void SetOutRangeMode(OUT_RANGE_MODE mode)
        {
            if (_dataSource != null)
                Bridge4DS.ChangeOutRangeMode(_dataSource.FDVUUID, mode);
            _outRangeMode = mode;
        }


        private TextureFormat GetTextureFormat()
        {
            return _dataSource.TextureFormat;
        }


        void OnGUI()
        {
            if (_debugInfo)
            {
                double delay = System.DateTime.Now.Subtract(_playDate).TotalMilliseconds - ((float)(CurrentFrame - _playCurrentFrame) * 1000 / GetFrameRate());
                string decoding = _decodingFPS.ToString("00.00") + " fps";
                string updating = _updatingFPS.ToString("00.00") + " fps";
                delay /= 1000;
                if (!_isPlaying)
                {
                    delay = 0f;
                    decoding = "paused";
                    updating = "paused";
                }
                int top = 20;
                GUIStyle title = new GUIStyle();
                title.normal.textColor = Color.white;
                title.fontStyle = FontStyle.Bold;
                GUI.Button(new Rect(Screen.width - 210, top - 10, 200, 330), "");
                GUI.Label(new Rect(Screen.width - 200, top, 190, 20), "Sequence ", title);
                GUI.Label(new Rect(Screen.width - 200, top += 15, 190, 20), "Length: " + ((float)GetSequenceNbFrames() / GetFrameRate()).ToString("00.00") + " sec");
                GUI.Label(new Rect(Screen.width - 200, top += 15, 190, 20), "Nb Frames: " + GetSequenceNbFrames() + " frames");
                GUI.Label(new Rect(Screen.width - 200, top += 15, 190, 20), "Frame rate: " + GetFrameRate().ToString("00.00") + " fps");
                GUI.Label(new Rect(Screen.width - 200, top += 15, 190, 20), "Max vertices: " + _dataSource.MaxVertices);
                GUI.Label(new Rect(Screen.width - 200, top += 15, 190, 20), "Max triangles: " + _dataSource.MaxTriangles);
                GUI.Label(new Rect(Screen.width - 200, top += 15, 190, 20), "Texture format: " + _dataSource.TextureFormat);
                GUI.Label(new Rect(Screen.width - 200, top += 15, 190, 20), "Texture size: " + _dataSource.TextureSize + "x" + _dataSource.TextureSize + "px");
                GUI.Label(new Rect(Screen.width - 200, top += 25, 190, 20), "Current Mesh", title);
                GUI.Label(new Rect(Screen.width - 200, top += 15, 190, 20), "Nb vertices: " + _nbVertices);
                GUI.Label(new Rect(Screen.width - 200, top += 15, 190, 20), "Nb triangles: " + _nbTriangles);
                GUI.Label(new Rect(Screen.width - 200, top += 25, 190, 20), "Playback", title);
                GUI.Label(new Rect(Screen.width - 200, top += 15, 190, 20), "Time: " + ((float)(CurrentFrame) / GetFrameRate()).ToString("00.00") + " sec");
                GUI.Label(new Rect(Screen.width - 200, top += 15, 190, 20), "Decoding rate: " + decoding);
                GUI.Label(new Rect(Screen.width - 200, top += 15, 190, 20), "Decoding delay: " + delay.ToString("00.00") + " sec");
                GUI.Label(new Rect(Screen.width - 200, top += 15, 190, 20), "Updating rate: " + updating);
            }
        }


        public void Preview()
        {
            _preview = true;

            if (_sourceType == SOURCE_TYPE.Network)
                return;

            //save params values
            bool autoPlayTMP = _autoPlay;
            int nbGeometryTMP = _nbGeometryBuffers;
            int nbTextureTMP = _nbTextureBuffers;
            bool bufferModeTMP = _bufferMode;
            bool debugInfoTMP = _debugInfo;

            //set params values for preview
            _autoPlay = false;
            _nbGeometryBuffers = 1;
            _nbTextureBuffers = 1;
            _bufferMode = false;
            _debugInfo = false;

            _isInitialized = false;

            //destroy previous preview mesh
            if (_meshes != null)
            {
                for (int i = 0; i < _meshes.Length; i++)
                    DestroyImmediate(_meshes[i]);
                _meshes = null;
                for (int i = 0; i < _textures.Length; i++)
                    DestroyImmediate(_textures[i]);
                _textures = null;
            }

            //get the sequence
            Initialize();
            if (_isInitialized)
            {
                _nbFrames = GetSequenceNbFrames();

                //set mesh to the preview frame
                GotoFrame(_previewFrame);
                Update();

                //Assign current texture to new material to have it saved
                var tempMaterial = new Material(_rendererComponent.sharedMaterial);
                tempMaterial.mainTexture = _rendererComponent.sharedMaterial.mainTexture;
                _rendererComponent.sharedMaterial = tempMaterial;

                Uninitialize();
            }

            //restore params values
            _autoPlay = autoPlayTMP;
            _nbGeometryBuffers = nbGeometryTMP;
            _nbTextureBuffers = nbTextureTMP;
            _bufferMode = bufferModeTMP;
            _debugInfo = debugInfoTMP;

            _preview = false;
        }


        public void ConvertPreviewTexture()
        {
            System.DateTime current_time = System.DateTime.Now;
            if (_rendererComponent != null && _rendererComponent.sharedMaterial.mainTexture != null)
            {
                if (((System.TimeSpan)(current_time - last_preview_time)).TotalMilliseconds < 1000
                    || ((Texture2D)_rendererComponent.sharedMaterial.mainTexture).format == TextureFormat.RGBA32)
                    return;

                last_preview_time = current_time;

                if (_rendererComponent != null)
                {
                    Texture2D tex = (Texture2D)_rendererComponent.sharedMaterial.mainTexture;
                    if (tex && tex.format != TextureFormat.RGBA32)
                    {
                        Color32[] pix = tex.GetPixels32();
                        Texture2D textureRGBA = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                        textureRGBA.wrapMode = TextureWrapMode.Clamp;
                        textureRGBA.SetPixels32(pix);
                        textureRGBA.Apply();

                        _rendererComponent.sharedMaterial.mainTexture = textureRGBA;
                    }
                }
            }
        }


        private void AllocateGeometryBuffers(ref Vector3[] verts, ref Vector2[] uvs, ref Vector3[] norms, ref int[] tris, int nbMaxVerts, int nbMaxTris)
        {
            int size1 = nbMaxVerts;
            if (size1 > MAX_SHORT)
            {
                size1 = MAX_SHORT;
            }

            verts = new Vector3[size1];
            uvs = new Vector2[size1];
            tris = new int[nbMaxTris * 3];
            norms = null;
            if (_computeNormals)
                norms = new Vector3[size1];
        }
    }

    #endregion

}

