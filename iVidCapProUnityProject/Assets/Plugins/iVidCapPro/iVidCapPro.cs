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

using UnityEngine;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;


/* ---------------------------------------------------------------------
   Change History:

   - 03 Dec 16 - Version 1.7 updates. Mainly in native plugin.
   - 15 Feb 15 - Version 1.6 Update for Unity 4.6.2. Same fix as for 
                 Unity 5.0 Metal backend compatibility.
   - 15 Nov 14 - Version 1.5. Updates for Unity 5.0 compatibility.
				 Addition of iVidCapProEdit.
   - 28 Sep 14 - Fix problem with GetDocumentsPath for iOS 8. Replace use
                 of hardcoded path relative to Application.dataPath with
                 just Application.persistentDataPath.
   - 17 Aug 14 - Add public variable syncWaitTime. This addresses the
                 issue where the first few frames of the video were
                 blank. This technique is a bit of a hack, but is the
                 only thing I've found so far that works.
   - 10 Aug 13 - Changes for Unity 4.2.  Add rt.Create() after rt is
   				 created to ensure that the rendertexture is created
   				 in the GPU and has a valid texture id.  Also add 
   				 rt.DiscardContents() call after we've sent the rt
   				 to the native plugin.  This gets rid of the warning
   				 message about a potential performance degradation.
   - 17 Apr 13 - Add method to retrieve the number of dropped frames for
   				 the session.
   - 13 Apr 13 - Add average fps computation as an aid to performance 
   				 testing.
   - 30 Mar 13 - Fix for maximum framesize check. Must allow for portrait
   				 orientation.
   - 29 Mar 13 - Update documentation for editorial corrections.
   - 25 Mar 13 - Update documentation.
   - 22 Mar 13 - Change all usages of plg_ to ivcp_ for improved 
   				 uniqueness and association with iVidCapPro.
   - 18 Mar 13 - Make documentation updates for the post process build
                 script.
   - 17 Mar 13 - Fix frames sent tracking. It was not being incremented.
   - 24 Feb 13 - Add support for capturing audio from the microphone.
   - 17 Feb 13 - This object is now a controller and no longer performs
   				 OnRenderImage capture.  That task is now handled by
   				 iVidCapProVideo.  This division enables multiple cameras
   				 to be captured simultaneously. Also, support was added
   				 for capturing from a client supplied rendertexture. 
   - 12 Feb 13 - Add support for specifying a second audio file to 
   				 EndRecordingSessionWithAudioFiles().  Also, make the
   				 minimum frame size 128x128.  Sizes smaller than this
   				 cause crashes.
   - 09 Feb 13 - Add support for capture type of Throttled.
                 Change lower limit on frame size check to 128x128.
   - 02 Feb 13 - Add support for configuring gamma correction.
   - 29 Dec 12 - Add frame size check.
   - 15 Dec 12 - Ensure that all debug is only displayed when requested.
   - 09 Dec 12 - Add support for configuring video quality settings.
   --------------------------------------------------------------------- */


/* ---------------------------------------------------------------------*/
/**
	@file 		iVidCapPro.cs
	@author 	eccentric Orbits entertainment
	@version 	1.7
	@brief 		Place this script on a game object. It controls the video recording process.	
	
	\mainpage
	\tableofcontents
	@details
	\section overview Overview
	
	iVidCapPro is an iOS plugin that enables you to capture video/audio
	from your Unity iOS application and save it to the device photo album or to a
	file.  It's especially useful for recording video trailers, demoes and in-app 
	footage for your Unity-based game or app.  You can record in real time while the 
	app is in-use, or you can record at a specified fixed frame rate.  The latter is 
	particularly useful when you want to render a high quality video that does not
	require real-time interaction (e.g. recording a preconfigured animation sequence).  
	
	The plugin works by using one or more Unity cameras as a source of rendered
	frames.  These frames are passed to the iOS native portion of the plugin,
	which encodes the frames and streams them into a video file.  The video is stored 
	in the Documents directory of your app until it is complete, and is then either moved 
	into the photo album of the device or left in place, as requested by the app.
	
	Audio from the app may also be recorded in conjunction with video. A Unity AudioListener 
	object serves as a source of audio samples that are processed by the plugin.  During
	the recording process, audio is handled in a separate thread and is recorded to a
	temporary audio file.  When the recording session is complete, the audio file is mixed
	with the video file to produce the final video.  This additional step to mix the audio and
	video is completely transparent to the client app.  Note that performing the mixing step
	will require a small amount of extra time as compared to recording only video.  While the mixing 
	is in progress, however, the app may continue to be used as usual, except that another recording
	session cannot be started until the video is complete.
	
	\section features Features
	
	Yes, iVidCapPro can record your app in real time, but it can also do a whole lot more.
	Video capture in iVidCapPro is centered around the Unity camera.  This means that it is
	not limited to simply creating a video of what's on the device screen.
	Here are some of its features:
	
	- Record at any resolution.
	  	+ Record your app in 720p or even 1080p.  Need 512x512 for
	      a special project?  You got it.
	- Record at fixed framerate.
		+ If you don't need real time rendering, but want a really 
	      high quality recording, you can pick your target framerate.  30fps? 60fps?  Your choice.
	- Record from multiple cameras in sequence.
		+ Switch between cameras during the recording
	      session to show the action from different vantage points.
	- Record from rendertexture.
		+ If you have your own secret recipe for cooking up a video
	      source, iVidCapPro gets out of your way.  Just point it at a rendertexture and it'll make
	      a video out of it.  No cameras required.
	- Record scene audio.
		+ Choose an audio listener and capture it.  The audio capture component
	      has its own gain control, so you can get the video volume just right without disturbing
	      your in-app mix.
	- Record a voice over
		+ Maybe you're making a demo of your app or game and want to add commentary.  Just tell 
		  iVidCapPro to record from the mic in addition to the scene.  Use the device's built-in
		  mic or a headset mic.
	- Supply prerecorded audio.
		+ Have prerecorded audio that you want to add to the video?
	      iVidCapPro lets you choose up to two audio files to mix with the finished video.  
	- Adjust video quality settings.
		+ Configure the bitrate and keyframe interval to get the
	      balance you need between video file size and quality.
	- Adjust video gamma.
		+ Need to tweak the video brightness to get the right look?  It's here.
	- Slide show mode.
		+  Throttled framerate capture lets you turn your app into a slideshow.
	- Full destination control
		+  Have the finished video copied to the device Photo Album.  Or store it in your 
		   app's Documents folder.  Control of the video is in your hands and those of your
		   customer.  No servers.  No accounts.  Use of the videos from your app can be totally
		   unlimited or fully restricted.  You decide.
	- H.264 video encoding.
	
	\section installation Installing iVidCapPro in Your Project
	
	Requirements:  Unity Pro 4.0 and Unity iOS Pro 4.0 or later, iOS SDK 5.0 or later.
	
	iVidCapPro is contained in a Unity package file called iVidCapPro.unitypackage.  This file
	contains everything you need to make use of the plugin.  To install the plugin in your app,
	import the package as usual, for example by using the menu selections Assets->Import Package->Custom Package.
	
	The package utilizes the Unity built-in support for automated plugin integration.  
	This means the files that are used by Xcode will be located in the Assets/Plugins/iOS folder in the 
	project browser.  These files must remain in this directory for the project to build properly.
	
	iVidCapPro comes with a post build processing script that handles making the required changes to the
	Xcode project.  This script will appear in the Editor/iVidCapPro folder in the Project tab
	(i.e. Assets/Editor/iVidCapPro), and is named <strong>iVidCapPro_PostBuild.cs</strong>. This script
	will be invoked automatically after each build.  Look \ref postbuild "here" for more details on 
	post build processing.
	
	\section usage Using iVidCapPro
	
	The iVidCapPro plugin is primarily accessed by its scripting interface, however there
	is a small amount of setup that must be performed in the editor.  Follow these steps
	to setup the video and audio capture devices in the scene.  If you are not capturing
	audio from the scene you may skip steps (4) and (5).
		
	1. Add an iVidCapPro component to a game object of your choice. 
	   I like to create an empty game object called "Video Recorder" and place it there,
	   but it doesn't matter where you place it as long as the game object is enabled.
	2. Add an iVidCapProVideo component to the camera you will be using to record video.
	   Make sure that it is located after all image effect components on the camera that you
	   wish to be included in the video.  If you are recording from multiple cameras
	   (for example, the main camera and the GUI camera), add iVidCapProVideo to each.
	3. Select the iVidCapPro object from step (1) and set the "VideoCameras" property.  Add one
	   entry to the list for each capture camera you setup in step (2).  Set each entry to
	   reference a unique capture camera. 
	4. If you're recording audio from the scene, add an iVidCapProAudio component to the 
	   game object that contains the AudioListener object.  This may be on the same camera to
	   which you added an iVidCapProVideo component, or it may be some other object.
	5. Return to the iVidCapPro component, select the target button for the "Save Audio" property
	   and choose the game object which has the iVidCapProAudio component.
	  
	
	The basic outline for scripting the use of iVidCapPro follows:
	
	- Perform the editor setup detailed above.
	- Call the BeginRecordingSession method when you are ready for recording to begin.
	- Call the EndRecordingSession method when you want the recording to stop.
	
	Here is a sample invocation of both of these methods.  A detailed description of the method 
	parameters can be found in the class description for iVidCapPro.
	
	@code
	// "vr" is a reference to the iVidCapPro object.
	vr.BeginRecordingSession(
				"Spiralocity",                             // name of the video
				vidWidth, vidHeight,                       // video width & height in pixels
				30,                                        // frames per second when frame rate Locked/Throttled
				iVidCapPro.CaptureAudio.No_Audio,          // whether or not to record audio
				iVidCapPro.CaptureFramerateLock.Unlocked); // capture type: Unlocked, Locked, Throttled
	.
	// Things are happening here that you want to be recorded.
	.
	vr.EndRecordingSession(
				iVidCapPro.VideoDisposition.Save_Video_To_Album,  // where to put the finished video 
				out framesRecorded);                        // # of video frames recorded
	@endcode
	
	The above code snippet depicts the most basic invocation scenario for the plugin.  
	Probably, however, your app will have a UI in which you will want to reflect the
	completion status of the video.  Because finalization of the video happens in a 
	separate thread, you cannot assume that the video is completely written to file
	when EndRecordingSession returns.  To know when the video is complete, register 
	a delegate method using RegisterSessionCompleteDelegate.  The delegate you register
	will be invoked when the video is complete and all iVidCapPro recording session resources
	have been released.
	
	Likewise, if an error should occur during the recording, you can be notified by means
	of the SessionErrorDelegate.  Register for this by using the RegisterSessionErrorDelegate.
	
	With these additions, our sample code now looks like:
	
	@code
	// "vr" is a reference to the iVidCapPro object.
		
	// Register a delegate to be called when the video is complete.
	vr.RegisterSessionCompleteDelegate(HandleSessionComplete);
	
	// Register a delegate in case an error occurs during the recording session.
	vr.RegisterSessionErrorDelegate(HandleSessionError);
	
	vr.BeginRecordingSession(
				"Spiralocity",                             // name of the video
				vidWidth, vidHeight,                       // video width & height in pixels
				30,                                        // frames per second when frame rate Locked/Throttled
				iVidCapPro.CaptureAudio.No_Audio,          // whether or not to record audio
				iVidCapPro.CaptureFramerateLock.Unlocked); // capture type: Unlocked, Locked, Throttled
	.
	.
	// Things are happening here that you want to be recorded.
	.
	.
	vr.EndRecordingSession(
				iVidCapPro.VideoDisposition.Save_To_Album,  // where to put the finished video 
				out framesRecorded);                        // # of video frames recorded
	.
	.
	.			
	// This delegate function is called when the recording session has completed successfully
	// and the video file has been written.
	public void HandleSessionComplete() {
		// Do UI stuff when video is complete.
	}
	
	// This delegate function is called if an error occurs during the recording session.
	public void HandleSessionError(iVidCapPro.SessionStatusCode errorCode) {
		// Do stuff when an error occurred.
	}
	@endcode
	
	That is the essential outline for using iVidCapPro in your app.  A detailed example can be
	found in the file iVidCapPro_SampleManager.cs included in the iVidCapPro package.  The 
	Sample Manager script demonstrates the use of many of the features of iVidCapPro, such as
	recording from multiple cameras, recording from a custom rendertexture, capturing a slideshow 
	video using Throttled mode, and mixing a pre-recorded audio file with the video.
	
	\section editor	iVidCapPro and the Unity Editor
				
	Note that creation of a video only takes place when the app is run on the device.  
	The iVidCapPro plugin script will, however, check to see if it is running in the editor and 
	avoid calling the Xcode plugin interfaces if so.  This allows you to perform a certain amount 
	of debug of program flow and UI controls related to video recording while in the editor.  
	Since video is not being recorded, however, the session complete and error delegates will
	not be called when running in the editor.

	\section resolution Recording device independent resolution video

	Usually we want the recorded video to simply capture the screen of the running app and 
	have the same resolution as the device on which it was recorded.  Sometimes, however,
	we may wish our app to always record at a particular fixed size.

	Some care is needed if you wish to record video from your app whose quality is not dependent
	on the screen resolution of the device.  Consider, for example, a case in which you would
	like to always record video at 1920x1080, even when running on devices that have a lower
	screen resolution.  

	If you simply hook up the main camera to iVidCapPro and set the recording framesize to
	1920x1080, a video will be produced, but it may not have the quality you expect.  If the 
	device screen is, for example, 1334x750 (iPhone 6), then each frame will be scaled up
	from 1334x750 to 1920x1080.  Some quality loss will result.

	In order to get the recording to actually be performed at 1920x1080, create a separate
	camera for recording and assign it a target rendertexture of 1920x1080.  Now feed that
	rendertexture directly into iVidCapPro using the SetCustomRenderTexture method.

	When recording a rendertexture directly there is no need to attach the iVidCapProVideo
	component to the camera's game object. In fact, you may experience very large framerate
	losses if you do.


	\section limitations Known Limitations
	
	iVidCapPro cannot capture GUI elements that are created via the old UnityGUI. These 
	elements are apparently rendered following all calls to the Camera's OnRenderImage()
	method.  Other popular GUI packages that use sprite-based techniques (e.g. EZ GUI and NGUI)
	can be captured without issue.  iVidCapPro works with the "new" Unity GUI system (uGUI)
	as longs you specify the Canvas to use a Render Mode of either "Screen Space - Camera"
	or "World Space".

	Multi-camera support is currently working properly only when none of the recording cameras 
	are set to depth-only or clear.  It appears that in these cases the Unity rendering engine
	reloads the full screen texture into the input texture to OnRenderImage, causing a massive 
	framerate impact.  Also, it appears the camera depth is not honored in the calling order to
	OnRenderImage. These shortcomings will be addressed in a future version of iVidCapPro.

	
	\section versions Version History
	
	Version 1.0 
	- Initial release.
	
	Version 1.1
	- Compatibility updates for Unity 4.2.
	
	Version 1.2
	- Compatibility updates for Unity 4.5.
	
	Version 1.3 
	- Added back performance improvements that were inadvertenly lost from iVidCapProLib.
	- Added syncWaitTime property to iVidCapPro. This addresses an issue with the
	  first few frames of a video appearing as blank/black. By default, a 1/10 second
	  delay now occurs between the time BeginRecordingSession is called and frames start
	  to be recorded. 
	- Cleaned-up numerous compiler warning messages in the iVidCapProLib Xcode project.
	
	Version 1.4
	- Compatibility updates for iOS 8. Replace the use of Application.dataPath with
	  Application.persistentDataPath. 

	Version 1.5
	- Compatibility updates for Unity 5.0.
	- New component added: iVidCapProEdit. This component will be used to add
	  video editing capability in the future.  Currently a single function is 
	  available: CopyVideoFileToPhotoAlbum. This allows you to copy an 
	  existing video file to the device photo album. This can be useful if 
	  you need to record a video then do some post-processing on it before
	  placing it in the photo album. Use it in conjunvction with the 
	  Save_Video_To_Documents option on EndRecordingSession.

	Version 1.6
	- Compatibility updates for Unity 4.6.2. Unity moved iOS Metal backend
	  support into 4.6.2.

	Version 1.7
    - Compatibility updates for Unity 5.5, iOS 10, and Xcode 8.
    - .unitypackage file created using Unity 5-4-0f3.
    - Create iVidCapPro lib with Bitcode enabled.
    - Fix to native code to ensure that unmixed video has completed writing
      before we try to mix it with audio.  This should alleviate some app 
      crashes in which the video track was missing when the video asset 
      was loaded.
    - Update native code to perform more error checking when loading assets.
      Plugin should now abort in these cases instead of crashing the app.
      IMPORTANT NOTE: When calling EndRecordingSessionWithAudioFiles, the specified 
      audio files must exist or the plugin will abort. Previously, non-existent
      files were simply ignored.
    - The SampleManager was updated to copy the finished video to the Photos album.
      Previously it was leaving the video in the app Documents directory (an oversight
      from when I was testing the "copy to Photos album" feature).
    - Added new section "Recording device independent resolution video" to the 
      documentation.
    - NOTE: When using iOS10, you will need to add the "Privacy - Photo Library Usage Description"
      key to the info.plist (Info) in Xcode.  Unity has not yet exposed this setting in
      Player settings.


   ------------------------------------------------------------------------ */

	/*! \page postbuild Post Build Processing
	
	iVidCapPro includes a post build processor that automatically modifies the Unity Xcode project
	to work with the plugin.  In general you should simply build your app as usual and no intervention
	on your part should be needed.
	
	If for any reason the iVidCapPro_PostBuild script does not work,
	you can manually make the necessary change to the Xcode project. Do this as follows:
	
	Open the generated Unity-iPhone.xcodeproj project file in Xcode.  Edit the AppController.mm/UnityAppController.mm
	file and add the following code at global file scope (e.g. just prior to the line containing "// \-\-\- AppController") 
	or at the end of the file:
	
	<strong>For Unity versions prior to 4.1 add this:</strong>
	
	@code
	// Added for use by iVidCapPro.
	extern "C" EAGLContext* ivcp_UnityGetContext()
	{
    	return _context;
	}
	@endcode
	
	<strong>For Unity version 4.1 -> 4.3, add this:</strong>
	
	@code
	// Added for use by iVidCapPro.
	extern "C" EAGLContext* ivcp_UnityGetContext()
    {
   	    return _mainDisplay->surface.context;
    }
	@endcode

	<strong>For Unity version 4.5 -> 4.6.2, add this:</strong>
	
	@code
	// Added for use by iVidCapPro.
	extern "C" EAGLContext* ivcp_UnityGetContext()
    {
		DisplayConnection* display = GetAppController().mainDisplay;
		return display->surface.context;
    }
	@endcode




	<strong>For Unity version 4.6.2 -> ?, add this:</strong>

	@code
	// Added for use by iVidCapPro.
	extern "C" EAGLContext* ivcp_UnityGetContext()
    {
		return UnityGetMainScreenContextGLES();
    }
	@endcode

	*/
	

/// <summary>
/// This class provides all the major features of the iVidCapPro plugin.
/// 
/// Place it on an enabled game object in your scene. Set its "Video Cameras"
/// property to reference the cameras in the scene that will be used for 
/// recording and have the iVidCapProVideo component on them. 
/// </summary>
public class iVidCapPro : MonoBehaviour {
	
	/* ------------------------------------------------------------------------
	   -- Class defined types --
	   ------------------------------------------------------------------------ */
	
	/// <summary>
	/// Specifies whether or not audio should be recorded in addition to video.
	/// Audio recording takes place in the audio thread and is performed independently
	/// of video recording.  This means that video and audio are recorded to separate files.
	/// When the recording session is ended by the application, the plugin must then mix the 
	/// audio and video files into a single resultant video file.  This will require a brief
	/// period of time.
	///
	/// Another recording session must not be started until previous video mix down is 
	/// complete.  In order to know when the creation of the fully mixed video is complete,
	/// register for a SessionCompleteDelegate to be invoked when the mixed video is 
	/// completed.
	/// </summary>
	public enum CaptureAudio {
		/// <summary>
		/// Audio will not be recorded.
		/// </summary>
		No_Audio = 0,
		
		/// <summary>
		/// Audio from the scene will be recorded in addition to video.
		/// </summary>
		Audio = 1,
		
		/// <summary>
		/// Audio from the scene and microphone will be recorded in addition to video.
		/// </summary>
		Audio_Plus_Mic = 2
	}
	
	/// <summary>
	/// Specifies whether or not time in Unity is locked to the video framerate. 
	/// </summary>
	public enum CaptureFramerateLock {
		
		/// <summary>
		/// Unity time is free running.  Use this option when you need to record video 
		/// in real time, e.g. when capturing a demo or game play session. 
		/// 
		/// If the rendering framerate exceeds the ability of the video encoder to process
		/// frames, then frames will be dropped from the video.  To prevent this, use the
		/// Throttled mode and reduce the target framerate to a level at which frame drops
		/// do not occur.  See GetNumberDroppedFrames() for more details.
		/// </summary>
		Unlocked = 0,
		
		/// <summary>
		/// Unity time advancement is locked to the recording framerate.  This ensures that the
		/// video will always be smooth, but may negatively affect framerate in the app.  This is a good
		/// choice when you're app needs to perform non-real time rendering of a video.
		/// Video frames will not be dropped in Locked mode.
		///
	    /// Note that audio recorded from the scene will not maintain synchronization with the video
	    /// when recorded in this mode.  This is because locking the framerate in Unity affects 
	    /// rendering but does not apply to audio play back.  As long as your audio does not need to
	    /// be precisely synchronized with the video (e.g. an ambient music track) this is not an issue.
	    /// If you need synchronized video and audio, use the Unlocked capture type.  Alternatively,
	    /// you can provide as many as two pre-recorded audio files to be mixed with the video.  Do this
	    /// by using the EndRecordingSessionWithAudioFiles() method.
		/// </summary>
		Locked = 1,
		
		/// <summary>
		/// Unity time is free running.  Recording of frames will occur at the interval specified by
		/// the framerate parameter to BeginRecordingSession.  A typical use for this setting would be 
		/// to specify it in conjunction with a small value for framerate in order to achieve a 
		/// slideshow effect. For example, setting framerate to 0.25 in conjunction
		/// with Throttled will yield a video which displays a new frame every 4 seconds.
		/// 
		/// Throttled mode may also be used to prevent dropped video frames.  Set the target
		/// video framerate to a value (e.g. 30 fps) lower than the rendering framerate.
		/// </summary>
		Throttled = 2
	}
	
	/// <summary>
	/// Specifies what to do with the video when ending the recording session.
	/// </summary>
	public enum VideoDisposition {
		/// <summary>
		/// The video is placed in the Photo Album/Camera Roll
	    ///	on the device.  The name specified for the video when BeginRecordingSession()
	    /// is called is used only as a temporary name while the video is being 
	    /// recorded.
		/// </summary>
		Save_Video_To_Album = 0,
		
		/// <summary>
		/// The video is placed in the application's "Documents" 
		/// folder.  This is the location that is used by iTunes file sharing.  The file
		/// will be saved with the name specified when BeginRecordingSession() was called.
		/// </summary>
		Save_Video_To_Documents = 1,
		
		/// <summary>
		/// This video is deleted.  A typical use for this would be to
	    ///	respond appropriately to a "Cancel Recording" action by the user.
		/// </summary>
	    Discard_Video = 2
	}
	
	/// <summary>
	// Indicates the current status of the recording session. 
	/// </summary>	
	public enum SessionStatusCode {
		
		/// <summary>
		/// The recording session has encountered no errors.
		/// </summary>
		OK = 0,
		
		/// <summary>
		/// The plugin failed when writing a video frame
		///	to the output stream.  This is usually caused by switching apps while
		/// the recording is in progress.
		/// </summary>
	    Failed_FrameCapture = -1,
		
		/// <summary>
		/// A memory warning was received during the recording session.
		/// This requires a change to Application.mm to add a call to ivcp_Abort.  
		/// Use of ivcp_Abort is optional and you may instead elect to just let 
		/// iOS handle low memory conditions.  The trade-off is that iOS may also kill
		/// your app if enough memory cannot be recovered by killing backgrounded
		/// apps.
		/// </summary>
    	Failed_Memory = -2,
		
		/// <summary>
		/// A failure occurred while attempting to copy the video the Camera Roll/Photo Album
		/// on the device.  A possible reason for this is that storage on the device is full.
		/// </summary>
    	Failed_CopyToAlbum = -3,
		
		/// <summary>
		/// When the video was checked for compatibility with the Camera Roll/Photo Album it was
		/// discovered to be incompatible.
		/// </summary>
    	Failed_VideoIncompatible = -4,
		
		/// <summary>
		/// A failure occurred during the mixing of the audio and video tracks into the finished
		/// video.
		/// </summary>
    	Failed_SessionExport = -5,
		
		/// <summary>
		/// A failure occurred for reasons unknown.
		/// </summary>
		Failed_Unknown = -6,
		
		/// <summary>
		/// A plugin method was called without first calling BeginRecordingSession().
		/// </summary>
		Failed_SessionNotInitialized = -7,
		
		/// <summary>
		/// No camera was found to perform video recording and no custom rendertexture was
		/// specified.  You must specify one or the other before calling BeginRecordingSession().
		/// One or more cameras may be specified by setting the VideoCameras property.
		/// A custom rendertexture is specified by calling SetCustomRenderTexture.
		/// </summary>
		Failed_CameraNotFound = -8,
		
		/// <summary>
		/// Audio recording was requested but the Save Audio property was not specified in
		/// the inspector.  The Save Audio property must be set to reference a game object that 
		/// has an iVidCapProAudio component.
		/// </summary>
		Failed_AudioSourceNotFound = -9,
		
		/// <summary>
		/// The requested resolution is not supported.
		/// The maximum resolution currently supported is 1920x1080.
		/// </summary>
		Failed_ResolutionNotSupported = -10,

		/// <summary>
		/// A video asset needed by the native side of the plugin could not
		/// be loaded. This could occur if you call EndRecordingSessionWithAudioFiles
		/// and specify a file name that doesn't exist.
		/// </summary>
		Failed_AssetCouldNotBeLoaded = -11
	}
	
	/// <summary>
	/// To be notified when an error occurs during a recording session, register
	/// a delegate using this signature by calling RegisterSessionErrorDelegate.
	/// </summary>
	public delegate void SessionErrorDelegate(iVidCapPro.SessionStatusCode statusCode);
	
	/// <summary>
	/// To be notified when the video is complete, register a delegate 
	/// using this signature by calling RegisterSessionCompleteDelegate.
	/// </summary>
	public delegate void SessionCompleteDelegate();
	
	/* ------------------------------------------------------------------------
	   -- Member variables --
	   ------------------------------------------------------------------------ */
	
	/// <summary>
	/// Reference to the iVidCapProVideo capture objects (i.e. cameras) from which
	/// video will be recorded.  Generally you will want to specify at least one.
	/// If you are using a custom rendertexture for video capture you need not 
	/// specify any cameras.
	/// </summary>
	public iVidCapProVideo[] videoCameras;
	
	/// <summary>
	/// Reference to the iVidCapProAudio object for writing audio files.  This needs
	/// to be set when you are recording a video with audio.  The iVidCapProAudio
	/// script must be placed on the same game object as the AudioListener from
	/// which you wish to record.
	/// </summary>
	public iVidCapProAudio saveAudio;

	/// <summary>
	/// Specifies a delay between the time the recording session is started and
	/// frames actually start being recorded.  This addresses an issue where the
	/// first few frames of video are blank because plugin frame capture may start
	/// before the render thread has rendered any images. If you find that your videos
	/// sometimes start with blank/black frames, increase this value.
	/// </summary>
	public float syncWaitTime = 0.10f;
	
	// Recording session flags.
	private bool sessionInitialized = false; 
	private bool isRecording = false; 
	
	// Recording session attributes.
	private string videoName;
	private CaptureAudio captureAudio;
	private CaptureFramerateLock captureFramerateLock;
	private int frameWidth;
	private int frameHeight;
	private float frameRate;
	private int framesSent;
	private int bitsPerSecond = -1;
	private int keyFrameInterval = -1;
	private float gamma = -1.0f;
	private SessionStatusCode sessionStatus;
	private int finalNumberDroppedFrames = 0;
	
	// Computed from frameRate.  It's the time in secs between each frame capture.
	// Only used when captureFramerateLock is set to Throttled.
	private float frameThrottleDelay = 0.0f;
	
	// The render texture that's used to pass frames to the plugin.
	private RenderTexture rt;
	
	// This gets set to true if the client has provided their own rendertexture
	// to serve as the source of video frames.
	private bool useCustomRT = false;
	
	// The session error delegate variable.
	private SessionErrorDelegate sessionErrorDelegate = null;
	
	// The video recording complete delegate variable.
	private SessionCompleteDelegate sessionCompleteDelegate = null;
	
	// Debug
	private bool showDebug = false;
	
	// Are we currently streaming audio to the wave file writer?
	private bool isAudioStreaming = false;
	
	// Name of file to save.
	private string audioFileName = "tempAudio.wav";
	
	// The audio source that serves as the playback mechanism for mic input.
	private AudioSource micAudio = null;
	
	// Are we currently capturing audio from the mic?
	private bool isMicCapturing = false;
	
	// Variables used for computing the average framerate during the
	// current recording session.
	private float fpsUpdateInterval = 0.5f;
	private float averageFPS = 0.0f;
	private int framesRendered = 0;
	private bool isComputingFPS = false;
	
	/* ------------------------------------------------------------------------
	   -- Interface to native implementation --
	   ------------------------------------------------------------------------ */
	
	[DllImport ("__Internal")]
	private static extern void ivcp_Log (string message);
	
	[DllImport ("__Internal")]
	private static extern void ivcp_BeginRecordingSession(string videoName, 
		int frameWidth, int frameHeight, int frameRate,
		uint glTextureID, CaptureAudio captureAudio, CaptureFramerateLock captureFramerateLock,
		int bitsPerSecond, int keyFrameInterval, float gamma,
		string commObjectName,
		bool showDebug);
	
	[DllImport ("__Internal")]
	private static extern SessionStatusCode ivcp_EndRecordingSession(VideoDisposition action);
	
	[DllImport ("__Internal")]
	private static extern SessionStatusCode ivcp_EndRecordingSessionWithAudioFiles(VideoDisposition action,
		string audioFile1, string audioFile2);
	
	[DllImport ("__Internal")]
	private static extern void ivcp_Release();
	
	[DllImport ("__Internal")]
	private static extern void ivcp_CaptureFrameFromRenderTexture ();
	
	[DllImport ("__Internal")]
	private static extern SessionStatusCode ivcp_GetSessionStatusCode ();
	
	[DllImport ("__Internal")]
	private static extern int ivcp_GetNumDroppedFrames ();
	
	
	/* ------------------------------------------------------------------------
	   -- Public Interface - Video Recorder Plugin --
	   ------------------------------------------------------------------------ */
	  
	/// <summary>
	/// Turn debug printing on/off.  Printing debug trace messages may be useful
	/// while diagnosing problems with video recording.
	/// </summary>
	/// <param name='show'>
	/// Whether or not to print debug messages.
	/// </param>
	public void SetDebug(bool show)
	{
		showDebug = show;
	}
	 
	/// <summary>
	/// Display a message in the Xcode console output log.  This may be useful
	/// during debug.
	/// </summary>
	/// <param name='message'>
	/// The message to print in the Xcode console log.
	/// </param>
	public void Log(string message)
	{
		if (showDebug)
			Debug.Log("iVidCapPro-Log called with message: " + message);
		
		// Don't call plugin when running in the editor.
		if (Application.platform != RuntimePlatform.OSXEditor)
			ivcp_Log(message);
	}
	
	
	/// <summary>
	/// Register a delegate to be invoked when an error occurs during a 
	/// recording session.  Multiple delegates may be registered and 
	/// they will be invoked in the order of registration.
	/// </summary>
	/// <param name='del'>
	/// The delegate to be invoked when an error occurs.
	/// </param>
	public void RegisterSessionErrorDelegate(SessionErrorDelegate del) {
		
		sessionErrorDelegate += del;
	}
	
	/// <summary>
	/// Unregister a previously registered session error delegate.
	/// </summary>
	/// <param name='del'>
	/// The delegate to be unregistered.
	/// </param>
	public void UnregisterSessionErrorDelegate(SessionErrorDelegate del) {
		
		sessionErrorDelegate -= del;
	}
	 
	/// <summary>
	/// Register a delegate to be invoked when the video is complete.
	/// </summary>
	/// <param name='del'>
	/// The delegate to be invoked when an error occurs.
	/// </param>
	public void RegisterSessionCompleteDelegate(SessionCompleteDelegate del) {
		
		sessionCompleteDelegate += del;
	}
	
	/// <summary>
	/// Unregister a previously registered session complete delegate.
	/// </summary>
	/// <param name='del'>
	/// The delegate to be unregistered.
	/// </param>
	public void UnregisterSessionCompleteDelegate(SessionCompleteDelegate del) {
		
		sessionCompleteDelegate -= del;
	}
	
	/// <summary>
	/// Configure the quality/compression settings for the video to be produced.
	/// Use of this method is optional.  iVidCapPro will produce a good quality
	/// video by default.  If, however, you need the video to be smaller or super
	/// high quality, you can use this method to adjust the quality vs. file size
	/// characteristics of the video.
	/// </summary>
	/// <param name='bitsPerSecond'>
	/// The bit rate of the video.  Make it smaller for a smaller video file or larger
	/// for a higher quality video.  Practical values range from 100K to 10M.
	/// </param>
	/// <param name='keyFrameInterval'>
	/// The interval between key frames.  1 means every frame is a keyframe.
	/// If uncertain, try setting it to 30.
	/// </param>
	public void ConfigureVideoSettings(int bitsPerSecond, int keyFrameInterval)
	{
		this.bitsPerSecond = bitsPerSecond;
		this.keyFrameInterval = keyFrameInterval;
	}
	
	/// <summary>
	/// Configure the gamma correction value for the video to be produced.
	/// Use of this method is optional.  By default, iVidCapPro will not perform
	/// gamma correction. Use this method if you want to tweak the tonal appearance 
	/// of the output video.  Note that this method does not set the absolute gamma
	/// value of the video, but acts as an adjustment to the gamma of the incoming 
	/// frames.
	/// </summary>
	/// <param name='gamma'>
	/// The gamma correction value.  Valid values are from 0.0 to 3.0.  A value of 
	/// 1.0 is neutral.  Values less than 1.0 lighten the image, values greater than
	/// 1.0 darken it.  If you want to stop using gamma correction, specify -1.0
	/// for the gamma value.
	/// </param>
	public void ConfigureGammaSetting(float gamma)
	{
		this.gamma = gamma;
	}
	
	/// <summary>
	/// Specify that a custom rendertexture be used as a source of video frames
	/// instead of a camera. Use of this method is optional. Call this method before 
	/// invoking BeginRecordingSession.
	/// 
	/// NOTE: When recording a rendertexture there is no need to attach the iVidCapProVideo
	/// component to the camera's game object. Some users have reported very large framerate
	/// losses when doing so.
	/// </summary>
	/// <param name='customRT'>
	/// The rendertexture to use as the source of video frames. If you want to stop
	/// using a custom rendertexture, specify "null" for the parameter value (between
	/// recording sessions).
	/// </param>
	public void SetCustomRenderTexture(RenderTexture customRT)
	{
		
		this.rt = customRT;
		if (customRT != null) {
			useCustomRT = true;
			if (showDebug)
				Debug.Log("iVidCapPro-SetCustomRenderTexture: enable recording from rendertexture " + customRT.name);
		}
		else {
			useCustomRT = false;
			if (showDebug)
				Debug.Log("iVidCapPro-SetCustomRenderTexture: disable recording from rendertexture ");
		}
	}
	
	
	/* ------------------------------------------------------------------------
	   -- BeginRecordingSession --
	   ------------------------------------------------------------------------ */
	/// <summary>
	/// Initialize the attributes of the recording session and and start recording. 
	/// </summary>
	/// <returns>
	/// The status of the recording session.  This may be OK or a failure code.  See
	/// SessionStatusCode for more information.
	/// </returns>
	/// <param name='videoName'>
	/// Name for the video to be recorded. This name will comprise
	/// the portion of the file name prior to the extension.  For best results,
	/// use only alphanumerics and underscores in the name.
	/// </param>
	/// <param name='frameWidth'>
	/// Width of video frames, in pixels.
	/// </param>
	/// <param name='frameHeight'>
	/// Height of video frames, in pixels.
	/// </param>
	/// <param name='frameRate'>
	/// The frames per second of the final video.  This only applies in the case when
	/// captureFramerateLock is set to Locked or Throttled.  When capturing video in
	/// Unlocked mode, this setting has no effect.
	/// </param>
	/// <param name='captureAudio'>
	/// Whether or not the video will include audio captured from an AudioListener in
	/// the scene.
	/// </param>
	/// <param name='captureFramerateLock'>
	/// Specifies whether or not time in Unity is locked to the video framerate.
	/// </param>
	public SessionStatusCode BeginRecordingSession(string videoName, 
		int frameWidth, int frameHeight, float frameRate, 
	 	CaptureAudio captureAudio,  
		CaptureFramerateLock captureFramerateLock)
	{
		// Record the session attributes.
		this.videoName = videoName;
		this.frameWidth = frameWidth;
		this.frameHeight = frameHeight;
		this.frameRate = frameRate;
		this.captureAudio = captureAudio;
		this.captureFramerateLock = captureFramerateLock;
		
		sessionStatus  = SessionStatusCode.OK;
		framesSent = 0;
		
		if (showDebug)
			Debug.Log("iVidCapPro-BeginRecordingSession called: videoName= " + this.videoName
				+ " frameWidth=" 	+ this.frameWidth + " frameHeight=" + this.frameHeight + " frameRate=" + this.frameRate 
				+ " captureFramerateLock=" + this.captureFramerateLock
				+ " captureAudio=" + this.captureAudio);
		
		if (frameWidth < 128 || frameHeight < 128 || frameWidth > 1920 || frameHeight > 1920) {
			Debug.LogWarning("iVidCapPro-BeginRecordingSession called with an unsupported frame size: " 
				+ frameWidth + "x" + frameHeight);
			sessionStatus = SessionStatusCode.Failed_ResolutionNotSupported;
			return sessionStatus;	
		}
		
		if (!useCustomRT) {
			
			// We're not using a custom RT for capture, there must be at least
			// one capture camera specified.
			if (videoCameras.Length < 1) {
				Debug.LogWarning("iVidCapPro-BeginRecordingSession called with no cameras and no custom rendertexture.  At least one must be specified.");
				sessionStatus = SessionStatusCode.Failed_CameraNotFound;
				return sessionStatus;	
			}
			
			// We're not using a custom rendertexture as the video source.
			// Create a rendertexture for video capture.	
			// Size it according to the desired video frame size. 
			rt = new RenderTexture(frameWidth, frameHeight, 24);
			
			// Make sure the rendertexture is created so that we can get its
			// native texture id and pass it on our ivcp_BeginRecordingSession call.
			rt.Create();
			
		}
		
		if (this.captureAudio == CaptureAudio.Audio && saveAudio == null) {
			Debug.LogWarning("iVidCapPro-BeginRecordingSession called but no audio source was specified.  Set the Save Audio property to the object that has the AudioListener and iVidCapProAudio components.");
			sessionStatus = SessionStatusCode.Failed_AudioSourceNotFound;
			return sessionStatus;
		}
				
		// Lock the advancement of time to our recording framerate.
		// Must be 1 or greater.
		if (captureFramerateLock == CaptureFramerateLock.Locked)
		{
			Time.captureFramerate = Mathf.Max((int)frameRate, 1);
			frameRate = (float)Time.captureFramerate;
		}
		
		if (showDebug)
			Debug.Log("BeginRecordingSession: target render texture id=" + rt.GetNativeTextureID());
		
		// Don't call plugin when running in the editor.
		if (Application.platform != RuntimePlatform.OSXEditor) {
			// Init the plugin for recording.
			ivcp_BeginRecordingSession(videoName, frameWidth, frameHeight, (int)frameRate, 
				(uint)rt.GetNativeTextureID(), captureAudio, captureFramerateLock,
				bitsPerSecond, keyFrameInterval, gamma,
				this.gameObject.name,
				this.showDebug);
		}
		
		if (showDebug) {
			Debug.Log ("BeginRecordingSession: audio settings sample rate = " + AudioSettings.outputSampleRate);
			Debug.Log ("BeginRecordingSession: audio settings driverCaps = " + AudioSettings.driverCapabilities);
		}

		sessionInitialized = true;
		
		// Let's do this thing...
		isRecording = true;
		if (this.captureAudio == CaptureAudio.Audio || this.captureAudio == CaptureAudio.Audio_Plus_Mic) {
			if (this.captureAudio == CaptureAudio.Audio_Plus_Mic) {
				StartMicCapture();
			}
			StartAudioFileStreaming();
		}
		if (captureFramerateLock == CaptureFramerateLock.Throttled) {
			frameThrottleDelay = 1.0f/frameRate;
			StartCoroutine(ThrottleFrameCapture());
		}
		
		// Start sending frames to the plugin.
		StartCoroutine (CaptureRenderTexture());
				
		// Start computing average FPS.
		isComputingFPS = true;
		StartCoroutine (ComputeAverageFPS());
		
		// Loop thru each of the video capture objects, initialize them and start
		// them recording.
		foreach (iVidCapProVideo videoCam in videoCameras) {
			//videoCam.InitSession();
			videoCam.SetRenderTexture(rt);
			videoCam.SetCaptureViewport();
			videoCam.SetIsRecording(true);
		}
		return sessionStatus;
	}
	
	/* ------------------------------------------------------------------------
	   -- EndRecordingSession --
	   ------------------------------------------------------------------------ */
	/// <summary>
	/// Stop recording and produce the finalized video.  Note that the video file
	/// may not be completely written when this method returns.  In order to know
	/// when the video file is complete, register a SessionCompleteDelegate.
	/// </summary>
	/// <param name='action'>
	/// Specify how the resultant video file should be handled.  
	///	Typically we want to save the video to the device's photo album/camera roll
	/// or store it in the app's Documents folder.  If, however, the user canceled 
	/// the video session, we may instead want to discard the video file.
	/// </param>
	/// <param name='framesRecorded'>
	/// The number of frames in the video.
	/// </param>
	/// <returns>
	/// The current status of the recording session. 
	/// </returns>
	public SessionStatusCode EndRecordingSession(VideoDisposition action,
		out int framesRecorded)
	{	
		
		framesRecorded = 0;
		
		// If the client calls EndRecordingSession for a failed session, do nothing.
		if (sessionStatus != SessionStatusCode.OK)
			return sessionStatus;
		
		if (showDebug)
			Debug.Log("iVidCapPro-EndRecordingSession called: total frames sent=" 
			          + framesSent + "  video disposition=" + action);
		
		if (!sessionInitialized) {
			Debug.LogWarning("iVidCapPro-EndRecordingSession called but session was not initialized.");
			return SessionStatusCode.Failed_SessionNotInitialized;
		}
		
		// Pull the plug...
		CleanupSession();
		
		// Return value from the plugin.
		SessionStatusCode plgResult = 0;
		
		// Don't call plugin when running in the editor.
		if (Application.platform != RuntimePlatform.OSXEditor)
			plgResult = ivcp_EndRecordingSession(action);
		
		if (plgResult > 0) {
			framesRecorded = (int)plgResult;
			finalNumberDroppedFrames = ivcp_GetNumDroppedFrames();
		}
		
		if (showDebug) {
			Debug.Log("iVidCapPro-EndRecordingSession: total frames recorded=" 
			          + framesRecorded );
			Debug.Log("iVidCapPro-EndRecordingSession: average session FPS =" 
			          + averageFPS );
		}
		
		return sessionStatus;
	}
	
	/* ------------------------------------------------------------------------
	   -- EndRecordingSessionWithAudioFiles --	
	   ------------------------------------------------------------------------ */
	/// <summary>
	/// Stop recording, mix the video with the specified audio files and produce the
	/// finalized video.  Note that the video file may not be completely written when 
	/// this method returns.  In order to know when the video file is complete, 
	/// register a SessionCompleteDelegate.
	/// </summary>
	/// <param name='action'>
	/// Specify how the resultant video file should be handled.  
	///	Typically we want to save the video to the device's photo album/camera roll
	/// or store it in the app's Documents folder.  If, however, the user canceled 
	/// the video session, we may instead want to discard the video file.
	/// </param>
 	/// <param name='audioFile1'>
	/// A fully qualified audio file name.  This audio file will be mixed with the video
	///	just recorded.  The audio track will be truncated to the length
	///	of the video.
	/// </param>
	/// <param name='audioFile2'>
	/// A fully qualified audio file name.  This audio file will be mixed with the video
	///	just recorded.  The audio track will be truncated to the length
	///	of the video.  To mix a single audio file with the video, specify audioFile1 to the desired file 
	/// and pass the value 'null' for this parameter.
	/// </param>
	/// <param name='framesRecorded'>
	/// The number of frames in the video.
	/// </param>
	/// <returns>
	/// The current status of the recording session. 
	/// </returns>
	public SessionStatusCode EndRecordingSessionWithAudioFiles(VideoDisposition action,
		string audioFile1, string audioFile2, out int framesRecorded)
	{	
		
		framesRecorded = 0;
		
		// If the client calls EndRecordingSession for a failed session, do nothing.
		if (sessionStatus != SessionStatusCode.OK)
			return sessionStatus;
		
		if (showDebug)
			Debug.Log("iVidCapPro-EndRecordingSessionWithAudioFiles called: total frames sent=" 
			          + framesSent + "  video disposition=" + action);
		
		if (!sessionInitialized) {
			Debug.LogWarning("iVidCapPro-EndRecordingSessionWithAudioFiles called but session was not initialized.");
			return SessionStatusCode.Failed_SessionNotInitialized;
		}
		
		// Pull the plug...
		CleanupSession();
		
		// Return value from the plugin.
		SessionStatusCode plgResult = 0;
		
		// Don't call plugin when running in the editor.
		if (Application.platform != RuntimePlatform.OSXEditor)
			plgResult = ivcp_EndRecordingSessionWithAudioFiles(action, audioFile1, audioFile2);
		
		if (plgResult > 0) {
			framesRecorded = (int)plgResult;
			finalNumberDroppedFrames = ivcp_GetNumDroppedFrames();
		}
		
		if (showDebug)
		{
			Debug.Log("iVidCapPro-EndRecordingSessionWithAudioFiles: total frames recorded=" 
			          + framesRecorded );
			Debug.Log("iVidCapPro-EndRecordingSessionWithAudioFiles: average session FPS =" 
			          + averageFPS );
		}
		
		return sessionStatus;
	}
	
	
	/* ------------------------------------------------------------------------
	   -- ThrottleFrameCapture --
	   
	   This method is used when we want to perform real time capture, but have 
	   the capture rate throttled to a specific value. This enables, for example,
	   a slideshow or stop-motion effect video.
	   
	   ------------------------------------------------------------------------ */
	private IEnumerator ThrottleFrameCapture() {
		
		while (sessionInitialized) {
			isRecording = true;
			
			yield return new WaitForSeconds(frameThrottleDelay);
		}
	}
	
	/* ------------------------------------------------------------------------
	   -- CaptureRenderTexture --
	   
	   This method is used to capture the rendertexture to video at the end of
	   the current frame.  
	   
	   We'll capture the contents of the rendertexture once per rendering pass.
	   
	   ------------------------------------------------------------------------ */
	private IEnumerator CaptureRenderTexture() {
		
		//print ("CaptureRenderTexture:  thread id = " + System.Threading.Thread.CurrentThread.ManagedThreadId);

		bool needSync = true;
		while (sessionInitialized) {
			yield return new WaitForEndOfFrame();
			
			// Only perform the frame capture when we're actively recording.
			// For example, recording gets turned off during throttle mode.
			if (isRecording) {
				//print ("CaptureRenderTexture: capturing a frame; rt native id=" + rt.GetNativeTextureID());

				// 17-Aug-2014 This technique works to prevent initial blank frames.
				// Exactly WHY is still unclear.
				// It seems there is a race between this thread and the rendering thread.
				// Sometimes the first few frames of the video will be black.  Inserting a 
				// brief delay here before the first frame capture prevents that.
				if (needSync && syncWaitTime > 0.0f) {
					//print ("CaptureRenderTexture: about to wait for " + syncWaitTime + " seconds");
					yield return new WaitForSeconds(syncWaitTime);
				}
				needSync = false;

				// 17-Aug-2014 Experiment to prevent initial blank frames in video.
				// Goal was to insure all cameras have rendered to rt prior to first
				// call to plugin to capture frames.  Does not appear to be the source
				// of the problem, however, since technique did NOT help.
				/*
				if (needSync) {
					bool anyCameraNotInSync = true;
					while (anyCameraNotInSync) {
						anyCameraNotInSync = false;
						foreach (iVidCapProVideo videoCam in videoCameras) {
							if (!videoCam.isRendering)
								anyCameraNotInSync = true;
						}
						print ("CaptureRenderTexture: @C anyCameraNotInSync=" + anyCameraNotInSync);
						if (anyCameraNotInSync) {
							print ("CaptureRenderTexture: waiting for camera sync");
							yield return new WaitForEndOfFrame();
						}
					}
				}
				needSync = false;
				*/

				// Don't call plugin when running in the editor.	
				if (Application.platform != RuntimePlatform.OSXEditor) {
					ivcp_CaptureFrameFromRenderTexture();
				}
				framesSent++;
				
				// We're finished with the contents of the texture now.
				// It may help performance to let the GPU know.
				rt.DiscardContents();
				
				
				if (captureFramerateLock == CaptureFramerateLock.Throttled) {
					// Turn-off recording.  ThrottleFrameCapture will turn it on
					// again at the next capture cycle.
					isRecording = false;
					
					//print ("CaptureRenderTexture: throttling frame capture; time =" + Time.time);
				}
			}
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
	   -- StartAudioFileStreaming --
	   
	   This function is called to start streaming audio to a file. 		
	   ------------------------------------------------------------------------ */
	private void StartAudioFileStreaming() {
		
		if (showDebug) {
			Debug.Log ("StartAudioFileStreaming called...");
		}
		
		// If we're already streaming, ignore this call.
		if (isAudioStreaming) {
			return;
		}
		
		isAudioStreaming = true;
		
		// Start recording audio stream...
		saveAudio.SaveStreamStart(GetDocumentsPath(), audioFileName);
		
	}
	
	/* ------------------------------------------------------------------------
	   -- StopAudioFileStreaming --
	   
	   This function is called to stop streaming audio to a file. 		
	   ------------------------------------------------------------------------ */
	private void StopAudioFileStreaming() {
		
		if (showDebug) {
			Debug.Log ("StopAudioFileStreaming called...");
		}
		
		if (isAudioStreaming) {
			saveAudio.SaveStreamStop();
			isAudioStreaming = false;
		}
	}
	
	/* ------------------------------------------------------------------------
	   -- StartMicCapture --
	   
	   This function is called to start capturing audio from the device mic.
	   ------------------------------------------------------------------------ */
	private void StartMicCapture() {
		
		if (showDebug) {
			Debug.Log ("iVidCapPro - StartMicCapture called...");
		}
		
		// If we're already capturing, ignore this call.
		if (isMicCapturing) {
			return;
		}
		
		isMicCapturing = true;
		
		// If needed, create an audio source component through which will play the mic input.
		if (micAudio == null) {
			micAudio = gameObject.AddComponent<AudioSource>();
			micAudio.loop = true;
		}
		
		//print ("micAudio=" + micAudio);
		
		// Start capturing from the mic...
		micAudio.clip = Microphone.Start(Microphone.devices[0], true, 100, AudioSettings.outputSampleRate);
		
		// Play the mic sound in the scene.
		micAudio.Play();
		
	}
	
	/* ------------------------------------------------------------------------
	   -- StopMicCapture --
	   
	   This function is called to stop capturing audio from the device mic.
	   ------------------------------------------------------------------------ */
	private void StopMicCapture() {
		
		if (showDebug) {
			Debug.Log ("iVidCapPro - StopMicCapture called...");
		}
		
		// If we're not capturing, ignore this call.
		if (!isMicCapturing) {
			return;
		}
		
		isMicCapturing = false;
		
		// Stop capturing from the mic...
		Microphone.End (Microphone.devices[0]);
		micAudio.Stop();
		
	}
	
	/* ------------------------------------------------------------------------
	   -- GetSessionStatus --
	   
	   Returns the current status of the recording session.
	   
	   See the definition of SessionStatusCode above for an explanation of the
	   posible return values.
	   ------------------------------------------------------------------------ */
	/// <summary>
	/// Returns the current status of the recording session.
	/// See the definition of SessionStatusCode for an explanation of the
	/// posible return values.
	/// </summary>
	/// <returns>
	/// The current status of the recording session. 
	/// </returns>
	public SessionStatusCode GetSessionStatus() {
		return sessionStatus;
	}
	
	/* ------------------------------------------------------------------------
	   -- GetNumberDroppedFrames --
	   
	   Returns the number of video frames dropped during the current recording
	   session.
	   ------------------------------------------------------------------------ */
	/// <summary>
	/// Returns the number of video frames dropped during the current recording
	/// session.  If no recording session is active, the number of frames dropped
	/// during the previous session is returned.
	/// 
	/// This method is primarily useful for configuring your recording session
	/// during development to insure that frames will not be dropped.  Use this
	/// method and/or the ShowDebug method to determine if frames are being dropped.
	/// Frames may be dropped if your Unity scene is comparatively simple and the
	/// rendering speed is high.  In this case the plugin may not be able to encode
	/// and write the video fast enough to keep pace with the rendering.
	/// 
	/// If this situation occurs you likely want to take steps to remedy it. Dropped
	/// frames in significant numbers can lead to jerky video.
	/// A typical solution would be to use the Throttled mode of iVidCapPro and set
	/// the framerate to a number like 30.  Alternatively, you could consider adding
	/// something to your implmentation to slow the framerate slightly during a recording
	/// session.
	/// </summary>
	/// <returns>
	/// The number of dropped frames for the current recording session. 
	/// </returns>
	public int GetNumberDroppedFrames() {
		int numDroppedFrames = 0;
		if (sessionInitialized) {
			// Don't call plugin when running in the editor.	
			if (Application.platform != RuntimePlatform.OSXEditor) {
				numDroppedFrames = ivcp_GetNumDroppedFrames();
			}
		}
		else
			numDroppedFrames = finalNumberDroppedFrames;
		return numDroppedFrames;
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
	
	
	/* ------------------------------------------------------------------------
	   -- CleanupSession --
	   
	   Perform necessary session cleanup tasks. 
	   
	   Normally this will be called from EndRecordingSession.  In the case of 
	   a failed session, however, it will be called by the plugin error handler.
	   ------------------------------------------------------------------------ */
	private void CleanupSession() {
		
		isRecording = false;
		isComputingFPS = false;
		
		// Loop thru each of the video capture objects and shut them down.
		foreach (iVidCapProVideo videoCam in videoCameras) {
			videoCam.SetRenderTexture(null);
			videoCam.SetIsRecording(false);
		}
		
		// Stop capturing audio.
		if (this.captureAudio == CaptureAudio.Audio || this.captureAudio == CaptureAudio.Audio_Plus_Mic) {
			if (this.captureAudio == CaptureAudio.Audio_Plus_Mic) {
				StopMicCapture();
			}
			StopAudioFileStreaming();
		}
		
		// Allow time to flow as usual. 
		if (captureFramerateLock == CaptureFramerateLock.Locked)
			Time.captureFramerate = 0;
		
		// Release textures.
		if (!useCustomRT) {
			Destroy(rt);
			rt = null;
		}
		
		sessionInitialized = false;
		
	}
	
	/* ------------------------------------------------------------------------
	   -- PluginCompletionHandler --
	   
	   This method will be called by the plugin when all processing is complete
	   for the current recording session.
	   
	   ------------------------------------------------------------------------ */
	private void PluginCompletionHandler(string message) {
		
		if (showDebug) {
			Debug.Log("PluginCompletionHandler: message=" + message);
		}
		
		// Tell the native plugin to release all resources.
		StartCoroutine(ReleaseNativePlugin());
		
		// Tell our client that the session is complete.
		if (sessionCompleteDelegate != null)
			sessionCompleteDelegate();
	}
	
	/* ------------------------------------------------------------------------
	   -- PluginErrorHandler --
	   
	   This method will be called by the plugin when an error has occurred 
	   during recording.  When this happens the native plugin will abort the 
	   recording after the completion of this handler.  On this side we're 
	   going make sure that the video and audio recording are stopped.
	   
	   ------------------------------------------------------------------------ */
	private void PluginErrorHandler(string message) {
		
		if (showDebug) {
			Debug.Log("PluginErrorHandler: message=" + message);
		}
		
		// Stop recording and release session resources.
		CleanupSession();
		
		if (Application.platform != RuntimePlatform.OSXEditor) {
			sessionStatus = ivcp_GetSessionStatusCode();
		}
		
		// Tell our client that an error has occurred.
		if (sessionErrorDelegate != null)
			sessionErrorDelegate(sessionStatus);
		
		// Tell the native plugin to release all resources.
		StartCoroutine(ReleaseNativePlugin());
	}
	
	/* ------------------------------------------------------------------------
	   -- ReleaseNativePlugin --
	   
	   This method will cause the native plugin object to be released.
	   Once this has been called, another call to ivcp_BeginRecordingSession is
	   required to create a new native plugin object.
	   
	   Notice that we wait until the end of the current frame before calling
	   ivcp_Release to insure that we are not invoking ivcp_Release from inside a
	   message handler that was invoked by the native plugin. 
	   ------------------------------------------------------------------------ */
	private IEnumerator ReleaseNativePlugin() {
		
		yield return new WaitForEndOfFrame();
		
		if (showDebug) {
			Debug.Log("ReleaseNativePlugin called...");
		}
		
		// Don't call plugin when running in the editor.
		if (Application.platform != RuntimePlatform.OSXEditor) {
			ivcp_Release();
		}
	}
	
	/* ------------------------------------------------------------------------
	   -- ComputeAverageFPS --
	   
	   This method is used to compute the average rendering speed in frames per
	   second during a recording session.  It is useful for performance tuning.
	   
	   ------------------------------------------------------------------------ */
	private IEnumerator ComputeAverageFPS() {
		
		float runningTime;
		float startTime = Time.realtimeSinceStartup;
		framesRendered = 0;
		
		
		while (isComputingFPS) {
			if (framesRendered > 0) {
				runningTime = Time.realtimeSinceStartup - startTime;
				averageFPS = framesRendered / runningTime;
			}
			yield return new WaitForSeconds(fpsUpdateInterval);
		}
	}
	
	/* ------------------------------------------------------------------------
	   -- GetSessionAverageFPS --
	   
	   This method returns the average rendering fps for the current or most 
	   recently completed recording session.
	   
	   ------------------------------------------------------------------------ */
	public float GetSessionAverageFPS() {
		return averageFPS;
	}
	
	public void Update() {
		if (!isComputingFPS)
			return;
		
		framesRendered++;
	}
}
