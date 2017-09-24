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

/*------------------------------------------------------------------------------*/
/**
	@file 		iVidCapProVideo.cs
	@brief 		Place this script on a camera from which video will be captured.
	
--------------------------------------------------------------------------------*/

using UnityEngine;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;


/* ---------------------------------------------------------------------
   Change History:

   - 28 Sep 14 - Fix problem with GetDocumentsPath for iOS 8. Replace use
                 of hardcoded path relative to Application.dataPath with
                 just Application.persistentDataPath. 
   - 17 Aug 14 - Remove setting of Rendertexture.active. Appears to be
                 unnecessary. We're using Blit to copy directly into the
                 rendertexture.
   - 13 Apr 14 - Added warning message in Awake to alert user if the
                 associated camera has a target rendertexture.
   - 17 Feb 13 - Created.
   --------------------------------------------------------------------- */

/// <summary>
/// This class provides the video frame capture capability of the iVidCapPro plugin.
/// 
/// Place it on a Unity Camera from which you want video to be recorded.  
/// The camera image effect/filter chain method OnRenderImage() is used to capture
/// frames from the camera, so be sure to place the iVidCapPro script component
/// after any other image effects/filter components on the camera.
/// 
/// You may use multiple components of this type. Place one on each camera from
/// which video should be captured. For example, if you want to capture footage
/// of a demo of your app, place one on the main camera and one on the GUI camera.
/// The resultant video will include the output from both cameras.
/// </summary>
public class iVidCapProVideo : MonoBehaviour {
	
	/* ------------------------------------------------------------------------
	   -- Member variables --
	   ------------------------------------------------------------------------ */
	
	/// <summary>
	/// Specifies whether or not the camera being used to capture video is dedicated 
	/// solely to video capture. When a dedicated camera is used,
	/// the camera's aspect ratio will automatically be set to the specified frame size.
	/// If a non-dedicated camera is specified it is assumed the camera will also be used
	/// to render to the screen, and so the camera's aspect ratio will not be adjusted.
	/// Use a dedicated camera to capture video at resolutions that have a different aspect
	/// ratio than the device screen.
	/// </summary>
	public bool isDedicated = false;

	// 17-Aug-2014 Part of unsuccessful experiment to prevent initial blank frames in video.
	// This will be set to true after the first frame has been copied to 
	// the target rendertexture. It is used to ensure that we don't start 
	// capturing frames for the plugin before the rendertexture has an image.
	//public bool isRendering = false;

	// The camera that resides on the same game object as this script.
	// It will be used for capturing video.
	private Camera videoCam;
	
	// A local reference to the target rendertexture.
	private RenderTexture rt = null;
	
	// The rectangle that defines the viewport to be captured to the rendertexture.
	private Rect captureRect;
	
	// Whether or not recording from this camera is currently in progress.
	private bool isRecording = false;

	public void Awake () {
	
		videoCam = GetComponent<Camera>();
		if (videoCam == null) {
			// This game object has no camera component.
			Debug.LogWarning("iVidCapProVideo: Game object " + this.gameObject.name + 
				" needs a camera component to capture video.");
		} else if (videoCam.GetComponent<Camera>().targetTexture != null) {
			Debug.LogWarning("iVidCapProVideo: Game object '" + this.gameObject.name + 
				"' has a camera component with a render texture target specified.  If you want to " + 
				"record a render texture, REMOVE the iVidCapProVideo component from this game object" +
				" and use the iVidCapPro SetCustomRenderTexture method instead. Failure to do so may " +
				"result in a large framerate penalty.");
		}
	}
	
	/// <summary>
	/// Set the capture viewport of the camera on the rendertexture.
	/// Ordinarily you don't need to call this, as it is set automatically
	/// at the start of each recording session.  If, however, you change
	/// the viewport of the camera during the recording session, you need
	/// to call this function each time the camera viewport is updated.
	/// </summary>
	public void SetCaptureViewport() {
		
		
		Rect cameraRect = videoCam.rect;
		
		captureRect.x = cameraRect.x * rt.width;
		captureRect.y = cameraRect.y * rt.height;
		captureRect.width = cameraRect.width * rt.width;
		captureRect.height = cameraRect.height * rt.height;
		
		if (isDedicated) {
			// Set the aspect ratio of the camera to match the render texture.
			videoCam.aspect = ((float)rt.width)/((float)rt.height);
		}	
	}
	
	/* ------------------------------------------------------------------------
	   -- SetRenderTexture --
	   
	   This function is called by the controller to set the rendertexture that
	   will be used for video capture. 
	   ------------------------------------------------------------------------ */
	public void SetRenderTexture(RenderTexture rt) {
		this.rt = rt;
	}
	
	/* ------------------------------------------------------------------------
	   -- SetIsRecording --
	   
	   This function is called by the controller to set whether or not recording
	   is currently in progress.
	   ------------------------------------------------------------------------ */
	public void SetIsRecording(bool isRecording) {
		this.isRecording = isRecording;
	}

	// 17-Aug-2014 Part of unsuccessful experiment to prevent initial blank frames in video.
	/* ------------------------------------------------------------------------
	   -- InitSession --
	   
	   This function is called by the controller to initialize the recording
	   state at the start of a recording session.
	   ------------------------------------------------------------------------ */
	//public void InitSession() {
	//	this.isRendering = false;
	//}
	
	/* ------------------------------------------------------------------------
	   -- OnRenderImage --
	   
	   This function is called at the end of rendering for the camera to 
	   which this script is attached.  Here we blit the camera output into
	   the render texture that is serving as a source of frames for the 
	   video we're recording in the plugin. 
	   
	   Note that we have to blit the source to the destination so that the 
	   camera render is passed along to the next stage (possibly an image
	   effect we don't want included in the video or to the screen itself).
	   	   		
	   ------------------------------------------------------------------------ */
	private void OnRenderImage (RenderTexture source,  RenderTexture destination) {	
		
		//print ("OnRenderImage called...");
		if (rt != null && isRecording) {

			// Suppress "Tiled GPU perf." warning messages.
			// We need to render to our rendertexture multiple times without
			// discarding its contents in the case of multiple cameras. The
			// rendertexture contents WILL be discarded at the end of each frame.
			rt.MarkRestoreExpected();

			// 17-Aug-2014 This appears to be unnecessary.
			// 08-Nov-2014 Nope - It's necessary after all. It's required for the case when
			// there are multiple recording cameras. 
			RenderTexture.active = rt;
			
			// We want to honor the size and location on the screen of the camera rendering
			// rectangle.  These GL routines allow us to restrict the rendering viewport to
			// be that of the camera when we do the blit.
			GL.PushMatrix();
    		GL.LoadPixelMatrix();
			GL.Viewport(captureRect);
			
			Graphics.Blit (source, rt);
			
			// Restore the modelview and projection matrices.
			GL.PopMatrix();

			// 17-Aug-2014 This appears to be unnecessary.
			// 08-Nov-2014 Nope - It's necessary after all. See above.
			RenderTexture.active = null;

			// 17-Aug-2014 Experiment to prevent initial blank frames in video.
			// Goal was to insure all cameras have rendered to rt prior to first
			// call to plugin to capture frames.  Does not appear to be the source
			// of the problem, however, since technique did NOT help.
			//isRendering = true;
			
			//RenderTextureToPNG(rt, "frame2_capture.png");
			
		}
		
		// If the camera is dedicated to video recording we don't need to pass the 
		// image any further along the rendering chain.
		if (!isDedicated) {

			// This Blit will cause "Tiled GPU perf." warning messages when 
			// multiple non-dedicated cameras are used for recording. It isn't
			// possible to use MarkRestoreExpected() as above, because the 
			// destination rendertexture here is null, as the dest is the screen.
			// Unity bug?

			// Pass the image to the next stage of rendering.
			Graphics.Blit (source, destination);
		}
	}
	
	/* ------------------------------------------------------------------------
	   -- RenderTextureToPNG --
	   
	   Capture a rendertexture to a PNG file.
	   ------------------------------------------------------------------------ */
	private void RenderTextureToPNG(RenderTexture rtex, string fileName) {
		string filePath = GetDocumentsPath() + "/" + fileName;
		byte[] imageBytes; 
		
		Texture2D captureTex = new Texture2D(rtex.width, rtex.height, TextureFormat.RGB24, false);
		RenderTexture.active = rtex;
        captureTex.ReadPixels(new Rect(0, 0, rtex.width, rtex.height), 0, 0);
        RenderTexture.active = null;
        imageBytes = captureTex.EncodeToPNG();
        System.IO.File.WriteAllBytes(filePath, imageBytes);
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
}
