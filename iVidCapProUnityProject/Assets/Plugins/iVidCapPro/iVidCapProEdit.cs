// iVidCapProEdit Copyright (c) 2012-2014 James Allen and eccentric Orbits entertainment (eOe)
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

   - 26 Oct 14 - Created.
   
   To-Do:
   	- Create DeleteVideo/File function.
   --------------------------------------------------------------------- */


/* ---------------------------------------------------------------------*/
/**
	@file 		iVidCapProEdit.cs
	@brief 		Place this script on a game object to enable video editing
	            in your project.	
**/
/// <summary>
/// This class provides video editing functions to use in conjunction with
/// the iVidCapPro video recording capability or independently.
/// 
/// Place it on an enabled game object in your scene.
/// </summary>


public class iVidCapProEdit : MonoBehaviour {
	
	/* ------------------------------------------------------------------------
	   -- Class defined types --
	   ------------------------------------------------------------------------ */
	
	/// <summary>
	// Indicates the completion status of a requested operation. 
	/// </summary>	
	
	public enum StatusCode {
		
		/// <summary>
		/// The operation completed successfully.
		/// </summary>
		OK = 0,
		
		/// <summary>
		/// A failure occurred while attempting to copy the video the Camera Roll/Photo Album
		/// on the device.  A possible reason for this is that storage on the device is full.
		/// </summary>
    	Failed_CopyToAlbum = -1,
		
		/// <summary>
		/// When the video was checked for compatibility with the Camera Roll/Photo Album it was
		/// discovered to be incompatible.
		/// </summary>
    	Failed_VideoIncompatible = -2,
		
		/// <summary>
		/// A failure occurred while exporting a video into a file.
		/// </summary>
    	Failed_VideoExport = -3,
		
		/// <summary>
		/// A failure occurred for reasons unknown.
		/// </summary>
		Failed_Unknown = -4,
		
		/// <summary>
		/// A failure occurred because an input file was specified that does not exist.
		/// </summary>
		Failed_FileDoesNotExist = -5
	}

	/// <summary>
	/// Specifies the asynchronous operation that has just completed. This value
	/// will be passed to each delegate function registered using 
	/// RegisterNotificationDelegate.
	/// </summary>
	public enum ActionCode {
		
		/// <summary>
		/// The action is unknown. This most likely represents a 
		/// programming error.
		/// </summary>
		Unknown = 0,
		
		
		/// <summary>
		/// Moving a video to the device photo album has completed.
		/// </summary>
		Copy_Video_To_Photo_Album = 1,
		
		/// <summary>
		/// Encoding and writing a video asset to a file has completed.
		/// </summary>
	    Write_Video = 2
	}


	/// <summary>
	/// To be notified when an asynchronous operation has completed, register
	/// a delegate using this signature by calling RegisterNotificationDelegate.
	/// This delegate will be invoked for either successful or failed completion.
	/// </summary>
	/// <param name='actionCode'>
	/// Identifies the operation that has just completed.
	/// </param>
	/// <param name='statusCode'>
	/// Specifies the completion status for the operation that has just completed.
	/// </param>
	public delegate void NotificationDelegate(iVidCapProEdit.ActionCode actionCode, 
		iVidCapProEdit.StatusCode statusCode);
	
	// The notification delegate variable.
	private NotificationDelegate notificationDelegate = null;
	
	
	/* ------------------------------------------------------------------------
	   -- Interface to native implementation --
	   ------------------------------------------------------------------------ */
	
	[DllImport ("__Internal")]
	private static extern void ivcp_VE_Log (string message);
	
	[DllImport ("__Internal")]
	private static extern void ivcp_VE_Init (string commObjectName);
	
	[DllImport ("__Internal")]
	private static extern void ivcp_VE_ShowDebug (bool value);
	
	[DllImport ("__Internal")]
	private static extern void ivcp_VE_CopyVideoFileToPhotoAlbum(string fileName); 
		
	/* ------------------------------------------------------------------------
	   -- Member Variables --
	   ------------------------------------------------------------------------ */

	private bool showDebug = false;
	
	
	/* ------------------------------------------------------------------------
	   -- Public Interface - Video Editing Plugin --
	   ------------------------------------------------------------------------ */
	  
	/// <summary>
	/// Turn debug printing on/off.  Printing debug trace messages may be useful
	/// while diagnosing problems with video editing.
	/// </summary>
	/// <param name='show'>
	/// Whether or not to print debug messages.
	/// </param>
	public void SetDebug(bool show)
	{
		showDebug = show;
		
		// Don't call plugin when running in the editor.
		if (Application.platform != RuntimePlatform.OSXEditor) {
			// Tell the native side we want debug.
			ivcp_VE_ShowDebug(show);
		}
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
			Debug.Log("iVidCapProEdit-Log called with message: " + message);
		
		// Don't call plugin when running in the editor.
		if (Application.platform != RuntimePlatform.OSXEditor)
			ivcp_VE_Log(message);
	}
	
	
	/// <summary>
	/// Register a delegate to be invoked when an asynchronous operation completes. 
	/// Multiple delegates may be registered and they will be invoked in the order 
	/// of registration.
	/// </summary>
	/// <param name='del'>
	/// The delegate to be invoked when an asychronous operation completes.
	/// </param>
	public void RegisterNotificationDelegate(NotificationDelegate del) {
		
		notificationDelegate += del;
	}
	
	/// <summary>
	/// Unregister a previously registered notification delegate.
	/// </summary>
	/// <param name='del'>
	/// The delegate to be unregistered.
	/// </param>
	public void UnregisterNotificationDelegate(NotificationDelegate del) {
		
		notificationDelegate -= del;
	}
	 
	
	/* ------------------------------------------------------------------------
	   -- CopyVideoFileToPhotoAlbum --
	   ------------------------------------------------------------------------ */
	/// <summary>
	/// Copies the specified video file to the device photo album. 
	/// 
	/// This is an operation that completes asynchronously. To be notified when 
	/// it is complete, register a NotificationDelegate. 
	/// </summary>
	/// <returns>
	/// The status of the operation.  This may be OK or a failure code.  See
	/// StatusCode for more information.
	/// 
	/// The status code returned by this method does not indicate the final 
	/// completion status of this operation. To get the final status you must 
	/// register a NotificationDelegate and interpret the received ActionCode and
	/// StatusCode.
	/// </returns>
	/// <param name='fileName'>
	/// Fully qualified name of the file to be copied.
	/// </param>
	public StatusCode CopyVideoFileToPhotoAlbum(string fileName)
	{
		StatusCode status  = StatusCode.OK;
		
		if (showDebug)
			Debug.Log("iVidCapProEdit-CopyVideoFileToPhotoAlbum called: fileName= " + 
				fileName);
				
		if (!File.Exists(fileName)) {
			Debug.LogWarning("iVidCapProEdit-CopyVideoFileToPhotoAlbum called with a file that does not exists: " 
				+ fileName);
			status = StatusCode.Failed_FileDoesNotExist;
			return status;	
		}
		
		// Don't call plugin when running in the editor.
		if (Application.platform != RuntimePlatform.OSXEditor) {
			// Init the plugin for recording.
			ivcp_VE_CopyVideoFileToPhotoAlbum(fileName);
		}
		
		return status;
	}
	
	
	/* ------------------------------------------------------------------------
	   -- NotificationHandler --
	   
	   This method will be called by the native code when all processing is 
	   complete for an asynchronous action.  It's used only when an asynchronous
	   action has been requested, e.g. copying a video file to the photo album.
	   
	   The message received will have the following form:
	   
	     <operation>:<result>:<reason>
	     
	    For example:  "copy file to video album:OK:success"  
	    			  "copy file to video album:FAILED:video incompatible" 
	   
	   ------------------------------------------------------------------------ */
	private void NotificationHandler(string message) {
		
		if (showDebug) {
			Debug.Log("iVidCapProEdit-NotificationHandler: message=" + message);
		}
	
		// Parse the message.
		string[] tokens = message.Split(':');
		string operation = tokens[0];
		string result = tokens[1];
		string reason = tokens[2];
		
		ActionCode action;
		
		switch (operation) {
			case "copy file to photo album":
				action = ActionCode.Copy_Video_To_Photo_Album;
			break;
		
			default:
				action = ActionCode.Unknown;
			break;
		}
		
		StatusCode status;
		
		switch (result) {
			case "OK":
				status = StatusCode.OK;
			break;
			
			case "FAILED": {
			    switch (reason) {
					case "video incompatible":
						status = StatusCode.Failed_VideoIncompatible;
					break;
					case "video export":
							status = StatusCode.Failed_VideoExport;
					break;
					case "copy to album":
							status = StatusCode.Failed_CopyToAlbum;
					break;
					case "unknown":
							status = StatusCode.Failed_Unknown;
					break;
					default:
						status = StatusCode.Failed_Unknown;
					break;
				}
			}
			break;
		
			default:
				status = StatusCode.Failed_Unknown;
			break;
		}
		
		// Tell our client that the current action is complete.
		if (notificationDelegate != null) {
			notificationDelegate(action, status);
		}
	}
	
	public void Awake() {
		
		// Don't call plugin when running in the editor.
		if (Application.platform != RuntimePlatform.OSXEditor) {
			// Initialize the native side of the plugin.
			ivcp_VE_Init(this.gameObject.name);
		}
	}
}
