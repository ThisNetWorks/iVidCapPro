// iVidCapPro Copyright (c) 2012-2014 James Allen and eccentric Orbits entertainment (eOe)
//
/*
Permission is hereby granted, free of charge, to any person or organization obtaining a copy of 
the software and accompanying documentation covered by this license (the "Software") to use
and prepare derivative works of the Software, for commercial or other purposes, excepting that the Software
may not be repackaged for sale as a Unity asset.

The copyright notices in the Software and this entire statement, including the above license grant, 
this restriction and the following disclaimer, must be included in all copies of the Software, 
in whole or in part.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO 
THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT SHALL 
THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER 
IN CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
IN THE SOFTWARE.
*/

/*--------------------------------------------------------------------------------------
	-- iVidCapPro_SampleManager --
	
	This script provides an example of how an app can use iVidCapPro to perform
	video recording.  This example uses UnityGUI for the sole reason it is available
	with all Unity installations.  Your iOS app is likely to use a different GUI system,
	but the process for using iVidCapPro will be similar.
	
	This example exercises many different features of iVidCapPro, some of which
	would likely be mutually exclusive in any given application.  For this reason the script
	is much more complicated than is ordinarily needed.  Still, much can be learned by seeing
	how	the various interfaces to iVidCapPro are used here.
	
	This sample shows how to:
	- record video from the main camera in the scene (i.e. the camera
	  	that is rendering the screen view)
	- how to record from a dedicated secondary camera
	- how to record using both cameras simultaneously
	- how to record from a custom rendertexture
	- how to record from a different audio listener, depending on which camera is used
	- how to record using the Unlocked, Locked and Throttled modes
	- how to alter the video quality and gamma settings for the plugin
	- how to use the session complete and error delegates
	
	You can make a quick trial of iVidCapPro in your project by adding this script to a 
	game object in your scene and setting its properties.  At a minimum you only need
	specify these properties:
	
	- vrController - set to the object that has the iVidCapPro component.
	- mainCamera   - set to a camera object that has the iVidCapProVideo component.
	- gameSkin     - set to the iVidCapPro_SampleGUISkin included with the package.
	
	If you want to record scene audio, set:
	
	- mainAudioListener - set to an AudioListener object that has the iVidCapProAudio component.
	
	To try out recording from a secondary camera, set:
	
	- secondaryCamera - set to a Camera object that has the iVidCapProVideo component.
	
	To try out recording from a rendertexture, set:
	
	- customRenderTexture - set to a rendertexture of your choice.
	
	
---------------------------------------------------------------------------------------- */ 

/* ---------------------------------------------------------------------
   Change History:

   - 03 Dec 16 - Switch from saving video to Documents folder to saving it
			     to the Photos album.
   - 28 Sep 14 - Fix problem with GetDocumentsPath for iOS 8. Replace use
                 of hardcoded path relative to Application.dataPath with
                 just Application.persistentDataPath.
   - 15 Apr 13 - Only configure the gamma setting if the value is not at default.
   - 13 Apr 13 - Add display of the average fps for the recording session.
   - 31 Mar 13 - Additions to the introductory commentary.
   - 25 Mar 13 - Changes to improve code readability.  Add enums for various sources.
   - 24 Mar 13 - Fix for audio not being recorded from scene with second camera.
   				 Ensure that vrController.saveAudio property gets set to the 
   				 iVidCapProAudio that is in use, otherwise result is that scene audio
   				 file is created but has no samples. This causes a crash in the plugin.
   - 12 Feb 13 - Add support to the UI for two app supplied audio files
                 instead of one.
   - 09 Feb 13 - Add capture type and framerate to the UI.
   - 02 Feb 13 - Add gamma correction specification to the UI.
   - 07 Jan 13 - Add audio source specification to the UI.
   - 29 Dec 12 - Add status code check after BeginRecordingSession() call.
   - 15 Dec 12 - Add showDebug as a public property. Put dynamic font scaling 
   		under #if conditional.
   - 09 Dec 12 - Add support for configuring video quality settings.
   		Add Show/Hide UI button to get settings out of the way when
   		not in use. 
   --------------------------------------------------------------------- */

using UnityEngine;
using System.Collections;
using System.IO;

public class iVidCapPro_SampleManager : MonoBehaviour {
	
	// Ref to the video recorder controller object (iVidCapPro).
	public iVidCapPro vrController;
	
	// Ref to the video editor object (iVidCapProEdit).
	public iVidCapProEdit veController;
	
	// The scene's main camera and audio listener.  This camera is assumed to 
	// be rendering the scene to the screen as well as being used to record
	// video on demand.  It does not have to be "Main Camera".  Setting 
	// mainListener is optional.  If you don't want to record sound you can 
	// leave it blank.
	public Camera mainCamera;
	public AudioListener mainListener;
	
	// A secondary camera and audio listener dedicated solely to video recording.
	// Both are optional.  If not specified, the "Secondary" button will have no effect.
	public Camera secondaryCamera;
	public AudioListener secondaryListener;
	
	public RenderTexture customRenderTexture;
	
	// The GUI skin that we will use for this GUI. 
	// Set this to the included iVidCapPro_SampleGUISkin.
	public GUISkin gameSkin;
	
	// Whether or not to show debug messages while recording.
	public bool showDebug = false;
	
	// Ref to the current video capture object (iVidCapProVideo).
	private iVidCapProVideo vrCap;
	
	// Ref to the current audio capture object (iVidCapProAudio).
	private iVidCapProAudio arCap;
	
	// Is recording in progress?
	private bool isRecording = false;
	
	// The dimensions of the video to capture, in pixels.
	private int vidWidth;
	private int vidHeight;
	
	// How we should handle the final video--keep or discard.
	private iVidCapPro.VideoDisposition videoAction;
	
	// Whether or not to display the GUI message label.
	private bool displayMessage = false;
	
	// The message to be displayed in the GUI message label.
	private string infoMessage = "";
	
	// The dimensions of our GUI elements.
	private float areaWidth;
	private float areaHeight;
	private Rect guiRect = new Rect(1, 1, 1, 1);
	private Rect guiScrollRect;
	private Vector2 guiLocVect;
	private Vector2 guiScaleVect;
	
	// Video recording sizes.  Here we're offering a selection of sizes
	// that are multiples of the full screen size.  An alternative approach
	// is to offer explicit sizes (e.g. 640x480) or to simply record at
	// a single fixed size.  The main consideration is that, for a non-dedicated
	// camera, the frame size should have the same aspect ratio as the camera.
	// Otherwise the image will be distorted.  If you are using a dedicated
	// camera, iVidCapPro will automatically adjust the camera's aspect ratio
	// to be equal to that of the requested frame size.
	private enum VideoSize {
		Full = 0,
		Half = 1,
		FiveEights = 2
	}
	private VideoSize videoSizeState = VideoSize.Full;
	private string[] videoSizeStrings = {"Full", "1/2", "5/8"};
	
	// Set factors for computing the recorded frame size based on the user's
	// selection.
	private int[] videoSizeMultiplier = {1, 1, 5};
	private int[] videoSizeDivider =    {1, 2, 8};
	//private int[] videoWidths  = {480, 640, 1280}; 
	//private int[] videoHeights = {360, 480, 720};
	
	// Video source buttons.
	// 0 => Main Camera   
	// 1 => Secondary camera
	// 2 => Both Cameras
	// 3 => Custom RenderTexture
	private enum VideoSource {
		Main_Camera = 0,
		Secondary_Camera = 1,
		Both_Cameras = 2,
		Custom_RenderTexture = 3
	}
	private VideoSource videoSourceState = VideoSource.Main_Camera;
	private string[] videoSourceStrings = {"Main", "2nd", "Both", "RT"};
	
	// Recording capture type.  Here we can specify how video frames
	// will be captured.  Choices are:
	// Unlocked  - real-time capture; typical choice for recording
	//			   game play footage.
	// Locked    - fixed rate capture with Unity's time clock locked
	//             to a specified rendering rate; good choice for when
	//             you want to render a video with an unchanging framerate
	//             from your app.
	// Throttled - fixed rate capture without locking Unity's rendering
	//             rate; good for creating "slideshow" videos.
	private iVidCapPro.CaptureFramerateLock captureType = iVidCapPro.CaptureFramerateLock.Unlocked;
	private string[] captureTypeStrings = {"Unlocked", "Locked", "Throttled"};
	
	// The framerate at which video will be captured.  Only relevant with capture
	// types Locked or Throttled.
	private float captureFramerate = 30.0f;
	private float Min_captureFramerate = 0.1f;
	private float Max_captureFramerate = 60.0f;
	
	// The source of audio for the recording.
	// 0 => None
	// 1 => Capture from scene
	// 2 => User file 1
	// 3 => User file 2
	// 4 => User file 1 + User file 2
	// 5 => Scene + User file 1 + User file 2
	private enum AudioSource {
		None = 0,
		Scene = 1,
		User_File_1 = 2,
		User_File_2 = 3,
		User_File_1_2 = 4,
		All = 5
	}
	private AudioSource audioSourceState = AudioSource.Scene;
	private string[] audioSourceStrings = {"None", "Scene", "1", "2", "1+2", "All"};
	
	// Video quality/compression settings.
 	private int bitsPerSecond = 5000000;
	private int Min_bitsPerSecond = 100000;
	private int Max_bitsPerSecond = 9999999;
	private int keyframeInterval = 30;
	private int Min_keyframeInterval = 1;
	private int Max_keyframeInterval = 99;
	
	// Gamma correction setting.
	private float gammaCorrection = 1.0f;
	private float Min_gammaCorrection = 0.0f;
	private float Max_gammaCorrection = 3.0f;
	
	// Vars for computing and displaying rendering fps.
	public bool showFPS = true;
	public float fpsUpdateInterval = 0.5f; 	// frequency of fps computation and update
	public float currentFPS;               	// current calculated framerate
	
	// Working variables to track the FPS.
	private float fpsAccum = 0.0f; 			// time accumulated over the interval
	private int fpsFrames = 0; 				// Frames drawn over the interval
	private float fpsTimer = 0.0f;  		// Time remaining in current interval
	private string fpsText;
	
	// Hide the settings UI when not in use so player can see scene.
	private bool uiIsHidden = true;
	private string uiButtonText = "Show UI";

	private int fontSize;
	
	// Use this for initialization
	void Awake () {		
		
		// Create a rectangle and vector2 to use for GUI sizing.
		guiLocVect = new Vector2(1, 1);
		//guiScaleVect = new Vector2(1, 1);
		//guiScrollRect = new Rect(1, 1, 1, 1);
		
		arCap = null;
		vrCap = null;
		
#if (UNITY_4_0 || UNITY_4_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_4 || UNITY_4_5 || UNITY_4_6 || UNITY_5_0)
		// NOTE:  This only works in Unity4 or beyond!  Otherwise you'll need
		// to set the Font asset size to values like those below, using the editor,
		// depending on the device you're targeting.
		string deviceModel = SystemInfo.deviceModel;
		if (showDebug) {
			print("device model = " + deviceModel);
		}
		if (deviceModel.Contains("iPod5") || deviceModel.Contains ("iPhone5")) {
			fontSize = 28;
		} else if (deviceModel.Contains("iPod4") || deviceModel.Contains ("iPhone4")) {
			fontSize = 24; 
		} else if (deviceModel.Contains("MacBookPro")) {
			fontSize = 20;
		} else if (deviceModel.Contains("iPad1") || deviceModel.Contains ("iPad2")) {
			fontSize = 22;
		} if (deviceModel.Contains("iPad3") || deviceModel.Contains ("iPad4")) {
			fontSize = 28;
		}
#endif
		
		// For testing purposes...
		//Application.targetFrameRate = 120;
		
		//foreach (string micName in Microphone.devices) {
		//	print ("Mic device = " + micName);
		//}
		
	}
	
	public void Start() {
		
		// For handling messages from the video edit plugin.
		veController.RegisterNotificationDelegate(EditNotificationHandler);
	}
	
	/*-----------------------------------------------------------------------------------------
		-- OnGUI --
	----------------------------------------------------------------------------------------- */ 
	public void OnGUI () {

		// Assign the GUI skin we'll be using.
		GUI.skin = gameSkin;

		// Set the font size for the widgets we use.
		//gameSkin.label.fontSize = fontSize;
		//gameSkin.button.fontSize = fontSize;
		//gameSkin.box.fontSize = fontSize;
		//gameSkin.textArea.fontSize = fontSize;
		
		// Set the size of the area to use for rendering the main GUI.
		// We do it here to handle the case when the device is rotated, causing screen width to change.
		areaWidth = Screen.width - 32;
		areaHeight = Screen.height - 32;
		guiRect.Set(10, 10, areaWidth, areaHeight);
		guiLocVect.Set(guiRect.x, guiRect.y);
		
		GUILayout.BeginArea(guiRect);
			
			GUILayout.BeginVertical();
				GUILayout.Space(5);
				GUILayout.BeginHorizontal();
					if (GUILayout.Button(uiButtonText)) {
						uiIsHidden = !uiIsHidden;
						if (uiIsHidden)
							uiButtonText = "Show UI";
						else
							uiButtonText = "Hide UI";
					}
		
					// Record start/stop buttons.
					if (GUILayout.Button("Start Rec")) {
						StartVideoRecording();
					}
					if (GUILayout.Button("Stop Rec")) {
						StopVideoRecording();
					}
				GUILayout.EndHorizontal();
		
				// Label to display frames per second.
				if (showFPS) {
					GUILayout.Space(10);
					GUILayout.BeginHorizontal();
						GUILayout.Box("FPS: " + fpsText);
						GUILayout.Space(10);
						GUILayout.Box("Session FPS: " + vrController.GetSessionAverageFPS().ToString("F2"));
						GUILayout.Space(10);
						GUILayout.Box("Drops: " + vrController.GetNumberDroppedFrames());
					GUILayout.EndHorizontal();
				}
		
				if (!uiIsHidden) {
					// "Radio" buttons to select the recording size.
					GUILayout.BeginHorizontal();
						GUILayout.Box("Size:");
						videoSizeState = (VideoSize)GUILayout.Toolbar ((int)videoSizeState, videoSizeStrings);
					GUILayout.EndHorizontal();
			
					GUILayout.Space(10);
					// "Radio" buttons to select the recording video source.
					GUILayout.BeginHorizontal();
						GUILayout.Box("Video Source:");
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal();
						videoSourceState = (VideoSource)GUILayout.Toolbar ((int)videoSourceState, videoSourceStrings);
					GUILayout.EndHorizontal();
			
					// "Radio" buttons to select the capture type.
					GUILayout.Space(10);
					GUILayout.BeginHorizontal();
						GUILayout.Box("Type: ");
						captureType = (iVidCapPro.CaptureFramerateLock)GUILayout.Toolbar ((int)captureType, captureTypeStrings);
					GUILayout.EndHorizontal();
			
					if (captureType == iVidCapPro.CaptureFramerateLock.Locked ||
						captureType == iVidCapPro.CaptureFramerateLock.Throttled)
					{
						GUILayout.BeginHorizontal();
							GUILayout.Label("Rate: ");
							GUILayout.Label(captureFramerate.ToString("F2"), GUILayout.Width(150));
							captureFramerate = GUILayout.HorizontalSlider(captureFramerate, Min_captureFramerate, Max_captureFramerate);
						GUILayout.EndHorizontal();
					}
			
					GUILayout.Space(10);
					// "Radio" buttons to select the recording audio source.
					GUILayout.BeginHorizontal();
						GUILayout.Box("Audio Source:");
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal();
						audioSourceState = (AudioSource)GUILayout.Toolbar ((int)audioSourceState, audioSourceStrings);
					GUILayout.EndHorizontal();
			
					GUILayout.Space(20);
					GUILayout.BeginHorizontal();
						GUILayout.Label("bps:  ");
						GUILayout.Label(bitsPerSecond.ToString("D7"), GUILayout.Width(225));
						bitsPerSecond = (int)GUILayout.HorizontalSlider(bitsPerSecond, Min_bitsPerSecond,  Max_bitsPerSecond);
					GUILayout.EndHorizontal();
		
					GUILayout.Space(10);
					GUILayout.BeginHorizontal();
						GUILayout.Label("kfi:  ");
						GUILayout.Label(keyframeInterval.ToString("D2"), GUILayout.Width(250));
						keyframeInterval = (int)GUILayout.HorizontalSlider(keyframeInterval, Min_keyframeInterval, Max_keyframeInterval);
					GUILayout.EndHorizontal();
			
					GUILayout.Space(10);
					GUILayout.BeginHorizontal();
						GUILayout.Label("gamma:  ");
						GUILayout.Label(gammaCorrection.ToString("F1"), GUILayout.Width(152));
						gammaCorrection = (float)GUILayout.HorizontalSlider(gammaCorrection, Min_gammaCorrection, Max_gammaCorrection);
					GUILayout.EndHorizontal();
			
					// Record start/stop buttons.
					if (GUILayout.Button("Copy to Album")) {
						CopyToAlbum();
					}
			
				}
		
				// Display any message we have for the user.
				if (displayMessage) {
					GUILayout.Space(5);
					GUILayout.BeginHorizontal();
						GUILayout.Space(5);
						GUILayout.TextArea(infoMessage);
					GUILayout.EndHorizontal();
				}
		
			GUILayout.EndVertical();
		
		GUILayout.EndArea();
	}
	
	// This function is called when the "Start" button is pressed.
	public void StartVideoRecording() {

		if (isRecording)
			return;
		
		// Select the cameras and audio listener we're going to record.
		// We can record video from multiple cameras, but there is always just a
		// single audio listener we capture.
		if (mainCamera != null && (videoSourceState == VideoSource.Main_Camera || 
				videoSourceState == VideoSource.Both_Cameras)) {
			// Record from the main camera and main listener.
			vrCap = (iVidCapProVideo)mainCamera.GetComponent<iVidCapProVideo>();
			if (mainListener != null) {
				arCap = (iVidCapProAudio)mainListener.GetComponent<iVidCapProAudio>();
			}
			// Enable video capture from this camera.
			vrCap.enabled = true;
		}
		
		if (secondaryCamera != null && (videoSourceState == VideoSource.Secondary_Camera ||
				videoSourceState == VideoSource.Both_Cameras)) {
			// Record from the secondary camera.  First, turn it on.
			secondaryCamera.enabled = true;
			vrCap = (iVidCapProVideo)secondaryCamera.GetComponent<iVidCapProVideo>();
			// Use the secondary audio listener if we're recording solely from the secondary camera.
			if (secondaryListener != null && videoSourceState == VideoSource.Secondary_Camera) {
				arCap = (iVidCapProAudio)secondaryListener.GetComponent<iVidCapProAudio>();
				if (mainListener != null) {
					// Now turn off the main audio listener.  We want to use the secondary listener.
					// We keep the main camera enabled because it is used to show the user's view.
					mainListener.enabled = false;
				}
				secondaryListener.enabled = true;
			}
			// Enable video capture from this camera.
			vrCap.enabled = true;
		}
		
		
		isRecording = true;
		
		vrController.SetDebug(showDebug);
		
		// Set the desired video dimensions.
		//vidWidth  = videoWidths[videoSizeState];
		//vidHeight = videoHeights[videoSizeState];
		vidWidth  = (Screen.width * videoSizeMultiplier[(int)videoSizeState])/videoSizeDivider[(int)videoSizeState];
		vidHeight = (Screen.height * videoSizeMultiplier[(int)videoSizeState])/videoSizeDivider[(int)videoSizeState];
		
		// Testing full HD recording...
		//vidWidth = 1920;
		//vidHeight = 1080;
		
		// Testing tiny recording...
		//vidWidth = 128;
		//vidHeight =128;
		
		// Testing unsupported frame size.
		//vidWidth = 2048;
		//vidHeight = 1536;

		// Temporary hack - Limit video size to max allowed size for iPad3/4.
		// Preserve aspect ratio.
		if (vidWidth == 2048) {
			vidWidth = 1440;
			vidHeight = 1080;
		} else if (vidHeight == 2048) {
			vidHeight = 1440;
			vidWidth = 1080;
		}
		
		// Do we want to record the video from a custom rendertexture instead
		// of a camera? 
		if (customRenderTexture != null && videoSourceState == VideoSource.Custom_RenderTexture) {
			// Set the rendertexture and override the UI specified frame size.
			vrController.SetCustomRenderTexture(customRenderTexture);
			vidWidth = customRenderTexture.width;
			vidHeight = customRenderTexture.height;
			if (mainListener != null) {
				arCap = (iVidCapProAudio)mainListener.GetComponent<iVidCapProAudio>();
			}
		} else {
			// Be sure to reset custom rendertexture when we're not using it.
			vrController.SetCustomRenderTexture(null);
		}
		
		// Enable audio capture.
		if (arCap != null) {
			vrController.saveAudio = arCap;
			arCap.enabled = true;
		}
		
		
		// Register a delegate in case an error occurs during the recording session.
		vrController.RegisterSessionErrorDelegate(HandleSessionError);
		
		// Register a delegate to be called when the video is complete.
		vrController.RegisterSessionCompleteDelegate(HandleSessionComplete);
		
		// Configure video quality settings. This method call is optional.
		// You would only use it if you want to set the video compression settings
		// to non-default values.
		vrController.ConfigureVideoSettings(bitsPerSecond, keyframeInterval);
		
		// Configure gamma setting. This method call is optional.
		// You would only use it if you want to tweak the gamma setting for the 
		// output video.  Because gamma adjustment is computationally expensive,
		// do not invoke this function unless you really need it.
		if (gammaCorrection <= 0.9f || gammaCorrection >= 1.1f) {
			vrController.ConfigureGammaSetting(gammaCorrection);
		} else {
			vrController.ConfigureGammaSetting(-1.0f);
		}
		
		// Has audio recording from the scene been requested from the GUI?
		iVidCapPro.CaptureAudio audioSetting = iVidCapPro.CaptureAudio.No_Audio;
		if (arCap != null && (audioSourceState == AudioSource.Scene || 
				audioSourceState == AudioSource.All)) {
			audioSetting = iVidCapPro.CaptureAudio.Audio;
			// audioSetting = iVidCapPro.CaptureAudio.Audio_Plus_Mic;  
		}
		
		// Tell video recorder to begin a recording session and start recording.
		iVidCapPro.SessionStatusCode status = vrController.BeginRecordingSession(
			"SampleVideo",                             // file name; only relevant if saving to Documents
			vidWidth, vidHeight,                       // frame size
			captureFramerate,                          // frame rate; NOT USED when captureType is Unlocked
			audioSetting,					           // do we want to record audio
			captureType                                // type of capture (see docs for details)
			);
		
		if (status == iVidCapPro.SessionStatusCode.OK) {
			// Display a message to tell the user recording is in progress.
			ShowMessage("Recording...", 9999.0f);
		} else {
			ShowMessage ("Recording session failed!  Reason: " + status, 15.0f);
		}
	}
	

	// This function is called when the "Stop" button is pressed.
	public void StopVideoRecording() {
		
		if (!isRecording) 
			return;
		
		isRecording = false;
		videoAction = iVidCapPro.VideoDisposition.Save_Video_To_Album;
		//videoAction = iVidCapPro.VideoDisposition.Save_Video_To_Documents;
		
		iVidCapPro.SessionStatusCode rc;
		int framesRecorded = 0;
		
		// Stop the video recording and end the session.
		// Assuming the recording didn't abort, we get the number of frames recorded
		// returned as the result.
		
		// Have we requested mixing additional audio files?
		if (audioSourceState > AudioSource.Scene) {
			string audioFilePath1 = null;
			string audioFilePath2 = null;
			// Get the device path to the audio files from StreamingAssets/Audio.
			string assetsDir = Path.Combine(Application.streamingAssetsPath, "Audio");
			//string assetsDir = Application.dataPath + "/Raw/Audio";
			if (audioSourceState == AudioSource.User_File_1 || audioSourceState == AudioSource.User_File_1_2 
					|| audioSourceState == AudioSource.All) {
				string audioFileName1 = "Seagulls.mp3";
				audioFilePath1 = Path.Combine(assetsDir, audioFileName1);
				if (showDebug) {
					Debug.Log ("iVidCapPro - audio file 1 to mix:" + audioFilePath1);
				}
			}
			if (audioSourceState == AudioSource.User_File_2 || audioSourceState == AudioSource.User_File_1_2 
					|| audioSourceState == AudioSource.All) {
				string audioFileName2 = "Arpeggiator.mp3";
				audioFilePath2 = Path.Combine(assetsDir, audioFileName2);
				if (showDebug) {
					Debug.Log ("iVidCapPro - audio file 2 to mix:" + audioFilePath2);
				}
			}
			
			// Use this call if you want to mix the video with your own audio file.
			// Make sure the files you pass in exist!
			rc = vrController.EndRecordingSessionWithAudioFiles(videoAction, audioFilePath1, audioFilePath2, out framesRecorded);
		} else {
			// We're not mixing in an additional audio file.
			// Either no audio or audio captured only from the scene.
			rc = vrController.EndRecordingSession(videoAction, out framesRecorded);
		}
		
		// Disable video capture and reactivate main audio listener.
		if (mainCamera != null && (videoSourceState == VideoSource.Main_Camera || 
				videoSourceState == VideoSource.Both_Cameras)) {
			// We were recording from the main camera and main listener.
			vrCap = (iVidCapProVideo)mainCamera.GetComponent<iVidCapProVideo>();
			vrCap.enabled = false;
		}
		if (secondaryCamera != null && (videoSourceState == VideoSource.Secondary_Camera ||
				videoSourceState == VideoSource.Both_Cameras))	{
			vrCap = (iVidCapProVideo)secondaryCamera.GetComponent<iVidCapProVideo>();
			vrCap.enabled = false;
			
			// Turn off secondary camera.
			secondaryCamera.enabled = false;
			
			// If we were recording solely from the secondary camera we were using the 
			// secondary listener.  Make sure the main listener is turned on the secondary
			// listener is off.
			if (mainListener != null) {
				mainListener.enabled = true;
			}
			if (secondaryListener != null) {
				secondaryListener.enabled = false;
			}
		}
		
		// Disable audio capture.	
		if (arCap != null) {
			arCap.enabled = false;
		}
		
		// Check to see if the video recording was successful.
		// If so, we display a message asking the user to wait while the video
		// is finalized.  This is not necessary since our app is still usable
		// during the final video processing and how this is handled in the actual
		// app would vary depending on the circumstances.  The requirement that must
		// be honored is that a new recording session may not be started until the 
		// previous session is fully complete.
		if (rc == iVidCapPro.SessionStatusCode.OK) {
			if (Application.platform != RuntimePlatform.OSXEditor) {
				ShowMessage("Please wait...", 9999.0f);
			} else {
				// Our session complete delegate will never be invoked in the editor.
				// So just wait a few seconds for the look of the thing.
				ShowMessage("Please wait...", 5.0f);
			}
		} 
	}
	
	// This delegate function is called when the recording session has completed successfully
	// and the video file has been written.
	public void HandleSessionComplete() {
		
		ShowMessage("The video recording completed successfully and was saved\nin the Camera Roll/Photo Album.", 10.0f);
		
	}
	
	
	// This delegate function is called if an error occurs during the recording session.
	public void HandleSessionError(iVidCapPro.SessionStatusCode errorCode) {
		
		if (showDebug) {
			Debug.Log ("GUI_VideoManager - HandleSessionError called: errorCode=" + errorCode);
		}
		
		// This is the most likely user-induced error.  It occurs if they switch apps while the 
		// the video is recording.  
		if (errorCode == iVidCapPro.SessionStatusCode.Failed_FrameCapture) {
			if (showDebug) {
				print ("Recording session ended due to frame abort.");
			}
			ShowMessage("ERROR: The video recording failed.  The video was not saved.\nDid you switch to another app while recording?", 15.0f);
		} else if (errorCode == iVidCapPro.SessionStatusCode.Failed_Memory) {
			if (showDebug) {
				print ("Recording session ended due to memory abort.");
			}
			ShowMessage("ERROR: The video recording ran out of memory.  The video was not saved.  To remedy, try:\n" +
				"- recording at a smaller size\n- closing other apps\n- rebooting your device", 15.0f);
		} else if (errorCode == iVidCapPro.SessionStatusCode.Failed_AssetCouldNotBeLoaded) {
			if (showDebug) {
				print ("Recording session ended due to missing or defective asset.");
			}
			ShowMessage("ERROR: A necessary asset for creating the video was missing or defective. The video was not saved.", 15.0f);
		} else {
			if (showDebug) {
				print ("Recording session ended due to an unknown error.");
			}
			ShowMessage("ERROR: The video recording was terminated due to an unknown error.  The video was not saved.\n", 15.0f);
		}
		
		// The recording session has failed.  We will end the recording session at the end of this
		// frame, after this delegate function has had an opportunity to return.
		StartCoroutine(AbortVideoRecording());
	}
	
	// End the recording session at the end of the current frame.
	private IEnumerator AbortVideoRecording() {
		
		yield return new WaitForEndOfFrame();
		
		StopVideoRecording();
		
	}
	
	private void CopyToAlbum() {
		
		veController.SetDebug(true);
		
		string fileName = Path.Combine(GetDocumentsPath(), "SampleVideo.mov");
		
		veController.CopyVideoFileToPhotoAlbum(fileName);
		
	}
	
	public void EditNotificationHandler(iVidCapProEdit.ActionCode actionCode, 
		iVidCapProEdit.StatusCode statusCode) {
		
		if (showDebug) {
			print ("EditNotificationHandler -  actionCode=" + actionCode.ToString() +
				" statusCode=" + statusCode.ToString());
		}

	}
		
	
	public void ShowMessage(string msg, float duration) {
		
		displayMessage = true;
		infoMessage = msg;
		
		// Display the message for "duration" seconds.
		Invoke("HideMessage", duration);
	}
	
	public void HideMessage() {
		
		displayMessage = false;
		
	}
	
	/* ------------------------------------------------------------------------
	   -- GetDocumentsPath --
	   
	   Return the path to the Documents directory for our application.
	   Note that it is platform dependent.  The Documents directory is
	   assumed to exist.
	   
	   ------------------------------------------------------------------------ */
	private string GetDocumentsPath() {
		
		string documentsPath = "";
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			documentsPath = Application.persistentDataPath;
		} else if (Application.platform == RuntimePlatform.OSXEditor) {
			documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
		}
		
		return documentsPath;
	}
	
	public void Update() {
		
		// Compute the frames per second.  
		fpsTimer -= Time.deltaTime;
	    fpsAccum += Time.timeScale/Time.deltaTime;
	    fpsFrames++;
	   
	    // Interval ended - update GUI text and start new interval
	    if( fpsTimer <= 0.0f )
	    {
	        // display two fractional digits (f2 format)
			currentFPS = fpsAccum/fpsFrames;
	        fpsText = currentFPS.ToString("f2");
			
	        fpsTimer = fpsUpdateInterval;
	        fpsAccum = 0.0f;
	        fpsFrames = 0;
	    }
	}
}
