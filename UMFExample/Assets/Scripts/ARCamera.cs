using UnityEngine;
using System.Collections;
using System;
using System.Runtime.InteropServices;

//external structures
[StructLayout(LayoutKind.Sequential)]
internal struct DetectorResult
{
	public float positionX;
	public float positionY;
	public float positionZ;
	
	public float quatX;
	public float quatY;
	public float quatZ;
	public float quatW;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DetectorProperties
{
	public int textureWidth;
	public int textureHeight;
	public int bufferSize;
	public int chroma;
	IntPtr detector;
	IntPtr currentImage; //imageRGB
	IntPtr cacheResult;
}

public class ARCamera : MonoBehaviour
{
	public Material WebcamPlaneMaterial;
	public float webcamFovY = 30.0f;
	public TextAsset markerCSV;
	
	private Transform cameraTransform;
	
	#if UNITY_ANDROID
	AndroidJavaObject act;
	#else
	
	private GameObject texturePlane;
	private Mesh texturePlaneMesh;
	private WebCamTexture texture;
	private Color32[] textureData;
	
	//handles and pointers
	private GCHandle textureDataHandle;
	private IntPtr textureDataPointer = IntPtr.Zero;
	private byte[] _frameByte;
	private IntPtr _frameBytePointer = IntPtr.Zero;
	private GCHandle frameByteHandle;
	
	
	//detector stuff
	private DetectorProperties detectorProperties;
	private DetectorResult detectorResult;
	
	//external functions
	[DllImport("UMFDetector.dll", EntryPoint = "umf_set_frame")]
	private static extern int UMF_SetFrame(ref DetectorProperties detector, byte[] arr);
	
	[DllImport("UMFDetector.dll", EntryPoint = "umf_detect")]
	private static extern int UMF_Detect(ref DetectorProperties detector, float timeout);
	
	[DllImport("UMFDetector.dll", EntryPoint = "umf_get_result")]
	private static extern void UMF_GetResult(ref DetectorProperties detector, ref DetectorResult result);
	
	//main thread
	
	[DllImport("UMFDetector.dll", EntryPoint = "umf_create_detector")]
	private static extern int UMF_CreateDetector(int width, int height, float near, float far, float fov, ref DetectorProperties props);
	
	[DllImport("UMFDetector.dll", EntryPoint = "umf_free_detector")]
	private static extern void UMF_FreeDetector(ref DetectorProperties detector);
	
	[DllImport("UMFDetector.dll", EntryPoint = "umf_set_marker_str")]
	private static extern int UMF_SetMarkerStr(ref DetectorProperties props, [MarshalAs(UnmanagedType.LPStr)] string str);
	
	private void SetupTexture()
	{
		WebCamDevice[] devices = WebCamTexture.devices;
		texture = new WebCamTexture(devices[0].name, 1280, 720, 30);
		texture.Play();
		textureData = new Color32[texture.width*texture.height];
		
		textureDataHandle = GCHandle.Alloc(textureData, GCHandleType.Pinned);
		textureDataPointer = textureDataHandle.AddrOfPinnedObject();
		_frameByte = new byte[texture.width * texture.height * 4];
		frameByteHandle = GCHandle.Alloc(_frameByte, GCHandleType.Pinned);
		_frameBytePointer = frameByteHandle.AddrOfPinnedObject();
	}
	
	private void CreateTexturePlane()
	{
		// Create plane game object
		texturePlane = new GameObject();
		texturePlane.name = "CameraFeedPlane";
		
		// Create plane mesh
		texturePlaneMesh = new Mesh();
		texturePlaneMesh.name = "CameraFeedPlaneMesh";
		
		float xmin = 0.0f;
		float ymin = 0.0f;
		float xmax = 0.0f;
		float ymax = 0.0f;
		
		// Get corner points of far clipping plane
		float z = GetComponent<Camera>().farClipPlane * 0.99f;
		Vector3 p0 = GetComponent<Camera>().ViewportToWorldPoint(new Vector3(0, 0, z));
		Vector3 p1 = GetComponent<Camera>().ViewportToWorldPoint(new Vector3(1, 0, z));
		Vector3 p2 = GetComponent<Camera>().ViewportToWorldPoint(new Vector3(1, 1, z));
		Vector3 p3 = GetComponent<Camera>().ViewportToWorldPoint(new Vector3(0, 1, z));
		
		
		// Set corner points to mesh
		texturePlaneMesh.vertices = new Vector3[] { p0, p1, p2, p3 };
		
		float textureAspect = (float)texture.width / (float)texture.height;
		float screenAspect = (float)Screen.width / (float)Screen.height;
		
		if (textureAspect == screenAspect)
		{
			texturePlaneMesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
		}
		else if (textureAspect < screenAspect)
		{
			//when the camera preview is higher than screen resolution
			// Vertical UV offset (works only when width > height)
			//float cameraAspect = (float)texture.width / (float)texture.height;
			//float verticalOffset = (camera.aspect - cameraAspect) / 2;
			float verticalOffset = (1.0f - textureAspect / screenAspect) / 2;
			
			// Set texturing coordinates
			texturePlaneMesh.uv = new Vector2[] { new Vector2(0, verticalOffset), new Vector2(1, verticalOffset), 
				new Vector2(1, 1 - verticalOffset), new Vector2(0, 1 - verticalOffset) };
			
		}
		else
		{
			//when the camera preview is wider than screen resolution
			float horizontalOffset = (1.0f - screenAspect / textureAspect) / 2;
			texturePlaneMesh.uv = new Vector2[] { new Vector2(horizontalOffset, 0), new Vector2(1 - horizontalOffset, 0),
				new Vector2(1 - horizontalOffset, 1), new Vector2(horizontalOffset, 1) };
		}
		
		
		texturePlaneMesh.triangles = new int[] { 0, 3, 2, 2, 1, 0 };
		
		// Add mesh filter
		texturePlane.AddComponent<MeshFilter>();
		
		// Set mesh to mesh filter
		texturePlane.GetComponent<MeshFilter>().mesh = texturePlaneMesh;
		
		// Add mesh renderer
		texturePlane.AddComponent<MeshRenderer>();
		
		// Create and set material with webcam texture
		WebcamPlaneMaterial.mainTexture = texture;
		texturePlane.GetComponent<Renderer>().material = WebcamPlaneMaterial;
		
		// Disable shadows
		texturePlane.GetComponent<Renderer>().receiveShadows = false;
		texturePlane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		
		// Add plane as child of this object
		texturePlane.transform.parent = cameraTransform;
	}
	
	private void InitDetector()
	{
		detectorProperties = new DetectorProperties();
		detectorProperties.chroma = 0;
		detectorResult = new DetectorResult();
		
		//we need different focal, then the camera gives us, since we are scaling...
		float fovy = this.webcamFovY; 
		float gameFovy = fovy;
		
		float textureAspect = (float)texture.width / (float)texture.height;
		float screenAspect = (float)Screen.width / (float)Screen.height;
		
		if (textureAspect < screenAspect)
		{
			//only if the texture is higher then we got
			
			float texHeight = 1.0f;
			float screenHeight = textureAspect / screenAspect;
			
			float focal_length = texHeight/ (2 * Mathf.Tan(fovy * Mathf.Deg2Rad * 0.5f) );
			Debug.Log("Focal length: " + focal_length);
			
			
			//ok now compute game fov
			gameFovy = 2* Mathf.Rad2Deg * Mathf.Atan2( screenHeight / 2, focal_length );
		}
		
		
		GetComponent<Camera>().fieldOfView = gameFovy;
		// Create unmanaged UMF detector
		Debug.LogError("Camera params: " + GetComponent<Camera>().near + " far: " + GetComponent<Camera>().far + " fov: " + GetComponent<Camera>().fieldOfView);
		int createResult = UMF_CreateDetector(texture.width, texture.height, GetComponent<Camera>().near, GetComponent<Camera>().far, fovy * Mathf.Deg2Rad, ref detectorProperties);
		Debug.Log("Detector allocated with result" + createResult);
        int changeMarkerResult = UMF_SetMarkerStr(ref detectorProperties, markerCSV.text);
        Debug.Log("Marker changed to with result: " + changeMarkerResult);
	}
	
	private void ReleaseDetector()
	{
		UMF_FreeDetector(ref detectorProperties);
		_frameByte = null;
		Debug.Log("Detector released");
	}
	
	#endif
	
	private float getWebcamFovY()
	{
		#if UNITY_ANDROID
		
		return 30;
		#else
		return webcamFovY;
		#endif
	}
	
	
	void Awake()
	{
		this.webcamFovY = this.getWebcamFovY();
		#if UNITY_ANDROID
		this.act = new AndroidJavaObject("com.quadrati.umfdetector.UnityActivity");
		this.act.Call("onCreate");
		#endif
	}
	
	// Use this for initialization
	void Start()
	{
		
		cameraTransform = transform;
		#if UNITY_ANDROID
		this.act.Call("onResume", Screen.width, Screen.height, false);
		GetComponent<Camera>().fieldOfView = this.act.Call<float>("getRendererFovy");

		if(markerCSV) {
			AndroidJavaObject csv = new AndroidJavaObject("java.lang.String", markerCSV.text);
			int markerResult = this.act.Call<int>("loadCSV", csv);
			
			Debug.Log("Loading custom marker" + markerResult);
		}
		#else
		
		SetupTexture();
		//init detector
		InitDetector();
		
		CreateTexturePlane();
		#endif
	}
	
	#if UNITY_ANDROID
	#else
	private int numRecordings = 100; // number of recordings to make
	private float timeRecorded = 0;  // total accumulated time
	private int recordingNum = 0;    // current recording number
	#endif
	
	// Update is called once per frame
	void Update()
	{
		#if UNITY_ANDROID
		float[] posrot = this.act.Call<float[]>("getPositionRotation");
		cameraTransform.position = new Vector3(posrot[0], posrot[1], posrot[2]);
		cameraTransform.rotation = new Quaternion(posrot[3], posrot[4], posrot[5], posrot[6]);
		Vector3 euler = cameraTransform.rotation.eulerAngles;
		
		#else 
		textureData = texture.GetPixels32(textureData);
		Marshal.Copy(textureDataPointer, _frameByte, 0, _frameByte.Length);
		
		float startTime = Time.realtimeSinceStartup;
		
		int result = UMF_SetFrame(ref detectorProperties, _frameByte);
		
		int success = UMF_Detect(ref detectorProperties, 100.0f);
		
		float endTime = Time.realtimeSinceStartup;
		
		if (success > 0)
		{
			UMF_GetResult(ref detectorProperties, ref detectorResult);
			cameraTransform.position = new Vector3(detectorResult.positionX, detectorResult.positionY, detectorResult.positionZ);
			cameraTransform.rotation = new Quaternion(detectorResult.quatX, detectorResult.quatY, detectorResult.quatZ, detectorResult.quatW);
			//Debug.Log("Position: " + detectorResult.positionX + "; " + detectorResult.positionY + "; " + detectorResult.positionZ);
		}
		
		float timeElapsed = (endTime-startTime);
		recordingNum++;
		timeRecorded += timeElapsed;
		if (recordingNum == numRecordings) {
			
			// calculate and display the average time
			float averageTime = timeRecorded/numRecordings;
			Debug.Log("Avg Time: "+averageTime+" seconds");
			
			// and finally, reset & repeat:
			recordingNum = 0;
			timeRecorded = 0;
		}
		#endif
	}
	
	void OnPostRender()
	{
		#if UNITY_ANDROID
		this.act.Call("onRenderUpdate");
		GL.InvalidateState();
		#endif
	}
	
	void OnDestory()
	{
		#if UNITY_ANDROID
		this.act.Call("onPause");
		this.act = null;
		#else
		ReleaseDetector();
		#endif
	}
}