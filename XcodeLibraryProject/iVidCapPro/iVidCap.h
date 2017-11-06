// iVidCap Copyright (c) 2012-2013 James Allen and eccentric Orbits entertainment(eOe).

/*
 Permission is hereby granted, free of charge, to any person or organization obtaining a copy of
 the software and accompanying documentation covered by this license (the "Software") to use
 and prepare derivative works of the Software, for commercial or other purposes, excepting that
 the Software may not be repackaged for sale as a Unity asset.
 
 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
 INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE,
 TITLE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING
 THE SOFTWARE BE LIABLE FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,
 ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
 IN THE SOFTWARE.

 
 The copyright notices in the Software and this entire statement, including the above license grant,
 this restriction and the following disclaimer, must be included in all copies of the Software,
 in whole or in part. 
*/

//
//  -- iVidCap.h --
//

#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>

#import "GPUImage.h"

/* --------------------------------------------------------------------------------
    -- ivcp_VideoRecorder --
   -------------------------------------------------------------------------------- */

#ifdef IVIDCAPPRO_DEMO
// The demo version of the plugin has a fixed frame size.
//static int Demo_Framewidth = 480;
//static int Demo_Frameheight = 360;

// The demo version of the plugin has a time limit.  Specified in milliseconds or frames.
static const float Demo_TimeLimit = 10.5 * 1000;
static int Demo_FrameLimit = 10 * 30;               // set later based on actual framerate
#endif

// This enum must be kept in sync with the CaptureAudio enum in iVidCap.cs.
enum AudioCapture {
    No_Audio = 0,
    Audio = 1,
    Audio_Plus_Mic = 2,
    Audio_Mic = 3
};

// This enum must be kept in sync with the VideoDisposition enum in iVidCap.cs.
enum VideoDisposition {
    Save_Video_To_Album = 0,
    Save_Video_To_Documents = 1,
    Discard_Video = 2
};

// This enum must be kept in sync with the SessionStatusCode enum in iVidCap.cs.
enum SessionStatusCode {
    OK = 0,
    Failed_FrameCapture = -1,
    Failed_Memory = -2,
    Failed_CopyToAlbum = -3,
    Failed_VideoIncompatible = -4,
    Failed_SessionExport = -5,
    Failed_Unknown = -6,
    Failed_AssetCouldNotBeLoaded = -11
};

// This enum must be kept in sync with the CaptureFramerateLock enum in iVidCap.cs.
enum VideoCaptureFramerateLock {
    Unlocked = 0,
    Locked = 1,
    Throttled = 2
};

@interface ivcp_VideoRecorder : NSObject <AVAudioRecorderDelegate> {

    
    // Video attributes.
    CGSize frameSize;
    int frameRate;
    int bitsPerSecond;
    int keyFrameInterval;
    bool useDefaultCompression;
    float gamma;
    bool useDefaultGamma;
        
    // Recording session attributes.
    VideoCaptureFramerateLock captureFramerateLock;
    AudioCapture captureAudio;
    VideoDisposition videoDestination;
    int frameWaitLimit;
    NSString* userAudioFile1;
    NSString* userAudioFile2;
    
    // Files.
    NSString* tempVideoFileName;
    NSString* capturedAudioFileName;
    NSString* videoExtension;
    NSString* finalVideoFileName;
    NSString* mixedAudioFileName;
    
    // Current recording state.
	BOOL isRecording;
	NSDate* recordStartTime;
    int frameNumber;
    int numWaitFrames;
    
    // The name of the game object on the Unity-side to which we'll be sending messages.
    char iVidCapGameObject[256];
    
    // Image pipeline.
    GPUImageTextureInput *textureInput;
    GLuint textureID;
    GPUImageMovieWriter *movieWriter;
    
    
    // Assets for mixing audio with the video.
    AVAsset* videoAsset;
    AVAsset* capturedAudioAsset;
    AVAsset* userAudioAsset1;
    AVAsset* userAudioAsset2;
    AVAsset* mixedAudioAsset;
    
    // Mic audio recording.
    AVAudioRecorder * micAudioRecorder;
    BOOL bMicAudioRecordFinished;
    BOOL bMovieWriterFinished;
    
    // Debug.
    BOOL showDebug;
    
}

@property int frameNumber;

/* --------------------------------------------------------------------------------
 -- setVideoName --
 
 Name that will be used to store the recorded video file.  Will only be saved
 permanently under this name when the option to save to the Documents folder
 is specified to EndRecordingSession.
 -------------------------------------------------------------------------------- */
- (void) setVideoName: (char*) videoName;

/* --------------------------------------------------------------------------------
    -- set/get FrameWidthHeight --
    
    Sets/gets the width and height of the video frames to be captured.
 
    setFrameWidthHeight establishes the frameTransform to flip the rendered frame to 
    be right side up.
   -------------------------------------------------------------------------------- */
- (void) setFrameWidth: (int) width Height: (int) height;
- (int)  getFrameWidth;
- (int) getFrameHeight;

/* --------------------------------------------------------------------------------
    -- getNumDroppedFrames --
 
    Return the number of dropped frames for the current session.
   -------------------------------------------------------------------------------- */
- (int) getNumDroppedFrames;

/* --------------------------------------------------------------------------------
 -- getNumWaitFrames --
 
 Return the number of frames for which we had to wait in the current session.
 -------------------------------------------------------------------------------- */
- (int) getNumWaitFrames;

/* --------------------------------------------------------------------------------
 -- setFrameRate --
 
 The rate, in frames per second, at which the video will be recorded.
 -------------------------------------------------------------------------------- */
- (void) setFrameRate: (int) fps;

/* --------------------------------------------------------------------------------
    -- set/get Render texture ID --
 
   -------------------------------------------------------------------------------- */
- (void) setTextureID: (GLuint)texID;
- (GLuint) getTextureID;

/* --------------------------------------------------------------------------------
 -- set/get Unity-side game object name for communication --
 
 -------------------------------------------------------------------------------- */
- (void) setCommGameObject: (char*)objectName;
- (char*) getCommGameObject;

/* --------------------------------------------------------------------------------
    -- beginRecordingSession --
 
    Initialize the various resources to be used during recording and prepare the
    video file for writing.
   -------------------------------------------------------------------------------- */
-(BOOL) beginRecordingSession;

/* --------------------------------------------------------------------------------
    -- endRecordingSession --
 
    Finalize the recording and release resources.
 
    Returns:
        -2  : The video was aborted.  Session terminated.
        -1  : The video could not be successfully finalized.
        <n> : The number of frames in the video.
   -------------------------------------------------------------------------------- */
//- (int) endRecordingSession: (VideoDisposition) action AddAudioFile:(NSString*) audioFile;
- (int) endRecordingSession: (VideoDisposition) action AddAudioFile1:(NSString*) audioFile1 AddAudioFile2:(NSString*) audioFile2;
- (void) movieWriterCompleteHandler;
    
// To-Do:  Comments...
- (SessionStatusCode) writeVideoFrameFromRenderTexture;
- (BOOL) LoadVideoAsset:(NSURL*)videoURL;
- (BOOL) LoadCapturedAudioAsset:(NSURL*)audioURL;
- (BOOL) LoadUserAudioAsset1:(NSURL*)audioURL;
- (BOOL) LoadUserAudioAsset2:(NSURL*)audioURL;
- (BOOL) LoadMixedAudioAsset:(NSURL*)audioURL;
- (int) ComposeTracksToURL: (NSURL*)outputURL;
- (void) movieWriterErrorHandler: (NSError *) error;
- (void) setBitsPerSecond: (int) bps;
- (void) setKeyframeInterval: (int) kfInterval;
- (void) setGamma: (float) gammaVal;
- (NSMutableDictionary *) getVideoSettings;
- (void) cleanupSession;


/* --------------------------------------------------------------------------------
    -- set/get Debug flag --
 
   -------------------------------------------------------------------------------- */
- (void) setDebug: (BOOL)show;
- (BOOL) getDebug;

/* --------------------------------------------------------------------------------
 -- sendErrorMessageToUnity --
 
 Send an error message to the Unity-side plugin, indicating an error.
 
 Returns:
 Nothing.
 -------------------------------------------------------------------------------- */
-(void) sendErrorMessageToUnity: (NSString*) error ForReason: (NSString*) reason;

/* --------------------------------------------------------------------------------
 -- abortRecordingSession --
 
 Immediately end recording and free resources.  The current video will be discarded.
  
 Returns:
    The failing status code of the session.
 -------------------------------------------------------------------------------- */
-(SessionStatusCode) abortRecordingSession: (SessionStatusCode) errorCode;


extern "C" {
    void ivcp_Abort(SessionStatusCode errorCode);
}

@end

