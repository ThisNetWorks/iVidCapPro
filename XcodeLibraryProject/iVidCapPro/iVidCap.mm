// iVidCapPro Copyright (c) 2012-2014 James Allen and eccentric Orbits entertainment(eOe).
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
//  -- iVidCap.mm --
//

/* ---------------------------------------------------------------------
 Change History:
 
 - 02 Dec 16 - Improve error handling for cases in which a video or audio 
               asset fails to load.  Plugin should now abort without crashing
               the app in these cases.
 - 01 Dec 16 - Remove opacityFilter.  No longer used.
 - 08 Nov 14 - Updates for the addition of iVidUtil and iVidEdit. Laying
               groundwork for edit capabilities.
 - 09 Aug 14 - Fix various compiler warnings that accompanied a new version
               of Xcode. Remove setting of the videoComposition property for
               the video export session.  It's not used when doing a pass-through
               session and was causing a warning message in the Xcode console
               at runtime.
 - 10 Aug 13 - Remove DEMO mode limitations on video size.  DEMO mode now
               limits only the length of the video.
 - 09 Apr 13 - Changes to improve performance. Remove the use of the 
               opacity and gamma filters.  These functions have now been
               incorporated into the GPUImage movie writer.
 - 17 Mar 13 - Add support for a demo version of the plugin that has built-in
               limits. Search for IVIDCAPPRO_DEMO.
 - 12 Feb 13 - Add support for mixing two app supplied audio files with
               the video instead of one.
 - 02 Feb 13 - Add support for configuring a gamma correction setting.
 - 10 Jan 13 - Refactor EndRecordingSession to simplify flow and make it
               more readable.
 - 08 Jan 13 - When we have both a captured audio file and a user audio
               file, mix them into a single audio file before mixing with
               the video to enable proper play back in VLC & Windows MP.
 - 06 Jan 13 - Use opacity filter to flip frames upright.  See comments
               below in BeginRecordingSession().
 - 27 Dec 12 - Implement support for mixing in an audio track from file
               in addition to audio recorded from the Unity scene.
 - 15 Dec 12 - Place all debug print statements under setDebug control.
 - 09 Dec 12 - Add support for configuring video quality settings.
 --------------------------------------------------------------------- */

/* --------------------------------------------------------------------
    NOTE: To disable ARC for this file: 
            - Select iVidCapPro (top level item) in the sidebar
            - Select iVidCapPro build target
            - Select Build Phases
            - Expand Compile Sources
            - For this file, add in Compile Flags column: -fno_objc_arc
   -------------------------------------------------------------------- */

#import <CoreGraphics/CoreGraphics.h>
#import <QuartzCore/QuartzCore.h>
#import <UIKit/UIKit.h>
#import <Availability.h>

#import <OpenGLES/EAGL.h>
#import <OpenGLES/EAGLDrawable.h>
#import <OpenGLES/ES1/gl.h>
#import <OpenGLES/ES1/glext.h>
#import <OpenGLES/ES2/glext.h>

#import "iVidCap.h"
//#import "iPhone_GlesSupport.h"


// Experiment... get full screen texture directly from Unity
/*
typedef struct UnityCVTextureInfo
{
    // CVOpenGLESTextureCache support
    void*   cvTextureCache;
    void*   cvTextureCacheTexture;
    GLuint  cvTextureCacheRTid;
    
} UnityCVTextureInfo;
*/

// Use this to get the Unity GL context.
extern "C" EAGLContext* ivcp_UnityGetContext();

// Experiment... get full screen texture directly from Unity
/*
extern "C" GLuint ivcp_UnityGetRenderTexId();
extern "C" void ivcp_UnityUseCVTextureCache(UnityCVTextureInfo* cvTextureInfo);
*/

// Use this to send messages to the iVidCapPro plugin on the Unity-side. 
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);


/*  
    When a serious error occurs we want to abort the recording session completely
    and release the current ivcp_VideoRecorder object.
 
    vr_sessionStatus keeps track of the current session status; if this is set
    to any of the Failed conditions, the vr object is no longer valid.  A new
    usable vr object is created when ivcp_BeginRecordingSession is called.
*/

static SessionStatusCode vr_sessionStatus;

/* --------------------------------------------------------------------------------
 -- ivcp_VideoRecorder --
 
 To-Do:
    - Call utility moveVideoFileToPhotoAlbum; remove internal version
 -------------------------------------------------------------------------------- */

@implementation ivcp_VideoRecorder

@synthesize frameNumber;

- (id)init
{
    self = [super init];
    
    if (self) {
        
        // Set default vals for member variables.
        isRecording = false;
        recordStartTime = nil;
        videoDestination = Save_Video_To_Album;
        
        frameSize.width = 1024;
        frameSize.height = 768;
        
        frameRate = 30;
        frameNumber = 0;
        numWaitFrames = 0;
        
        // This is the temporary file where the video is stored
        // whilst being written.  When complete it is either:
        // - Audio: mixed with audio track to produce the final video
        // - No_Audio: renamed to the final video file name
        tempVideoFileName = @"tempVideo.mov";
        
        // This is the temporary file where the captured audio is stored
        // whilst being written. It is only created when audio is
        // being recorded.  It will be removed after the final
        // mixed video is created.
        capturedAudioFileName = @"tempAudio.wav";
        
        // This is the temporary file where the mixed audio is stored
        // whilst being written. It is only created when we have both a
        // captured and user specified audio track. It will be removed
        // after the final mixed video is created.
        mixedAudioFileName = @"mixedAudio.m4a";
        
        userAudioFile1 = nil;
        userAudioFile2 = nil;

        
        // Default value.  Will be overridden when setVideoName method is called.
        videoExtension = @".mov";
        
        // Init comm game object to empty.  No notification messages will be sent
        // back to Unity unless this gets filled-in with a valid string at
        // beginRecordSession time.
        strcpy(iVidCapGameObject, "");
        
        // By default we let the AV libs choose the compression settings.
        useDefaultCompression = true;
        
        // By default we won't do any gamma correction.
        useDefaultGamma = true;
        
        // When the capture type is Locked we will wait a maximum of this number of
        // 10ms intervals for the movie writer to be ready to accept the next frame.
        frameWaitLimit = 30;
        
        showDebug = true;
        
        #ifdef IVIDCAPPRO_DEMO
        /* Not using this anymore. There are too many edge cases to make it worthwhile. */
        /*
        // Limit the framesize for the DEMO version.  We go through this complicated
        // business to ensure that we always have a framesize that's the proper aspect ratio.
        CGRect screenBounds = [[UIScreen mainScreen] bounds];
        CGFloat screenScale = [[UIScreen mainScreen] scale];
        CGSize screenSize = CGSizeMake(screenBounds.size.width * screenScale, screenBounds.size.height * screenScale);
        NSLog(@"iVidCapPro - screen size =  %f x %f", screenSize.width, screenSize.height);
        Demo_Framewidth  = (int)(screenSize.width/2);
        Demo_Frameheight = (int)(screenSize.height/2);
        
        // For Retina iPad we still want to keep it small.
        if (screenSize.width == 2048.0 || screenSize.height == 2048.0) {
            Demo_Framewidth = Demo_Framewidth / 2;
            Demo_Frameheight = Demo_Frameheight / 2;
        }
        
        //NSLog(@"iVidCapPro: DEMO Version - Device Orientation value=%i", [UIDevice currentDevice].orientation);
        
        // UIScreen always returns height x width.  Swap them in landscape.
        if ( [UIDevice currentDevice].orientation == UIDeviceOrientationLandscapeLeft
            || [UIDevice currentDevice].orientation == UIDeviceOrientationLandscapeRight) {
            int temp = Demo_Frameheight;
            Demo_Frameheight = Demo_Framewidth;
            Demo_Framewidth = temp;
            //NSLog(@"iVidCapPro: DEMO Version - Device Orientation is Landscape");
        }
        
        NSLog(@"iVidCapPro: DEMO Version - Framesize set to %ix%i", Demo_Framewidth, Demo_Frameheight);
        NSLog(@"iVidCapPro: DEMO Version - Maximum video length is %i seconds" , (int)(Demo_TimeLimit / 1000));
        */
        #endif
        
    }
    
    return self;
}

- (void) cleanupSession {
    
    //NSLog(@"iVidCap cleanupSession called...\n");

    // We need to remove the movie writer's dependency on our movieWriterErrorHandler,
    // else our dealloc will never be called due to a retain cycle (i.e. setting a
    // failure block in the movie writer causes it to retain our object).
    [movieWriter setFailureBlock: nil];
    
    
    // Get rid of our temporary files.
    NSString *filePath;
    filePath = [[self getDocumentsFileURL:tempVideoFileName] path];
    [self removeFile:filePath];
    
    filePath = [[self getDocumentsFileURL:capturedAudioFileName] path];
    [self removeFile:filePath];

}

- (void)dealloc {
    
    if (showDebug)
        NSLog(@"iVidCapPro - dealloc: called...\n");
    
    if (textureInput != nil) {
        [textureInput release];
        textureInput = nil;
    }

    
    if (movieWriter != nil) {
        [movieWriter release];
        movieWriter = nil;
    }
    
    if (videoAsset != nil) {
        [videoAsset release];
        videoAsset = nil;
    }
    
    if (capturedAudioAsset != nil) {
        [capturedAudioAsset release];
        capturedAudioAsset = nil;
    }
    
    if (userAudioFile1 != nil) {
        [userAudioFile1 release];
        userAudioFile1 = nil;
    }
    
    if (userAudioFile2 != nil) {
        [userAudioFile2 release];
        userAudioFile2 = nil;
    }
    
    if (userAudioAsset1 != nil) {
        [userAudioAsset1 release];
        userAudioAsset1 = nil;
    }
    
    if (userAudioAsset2 != nil) {
        [userAudioAsset2 release];
        userAudioAsset2 = nil;
    }
    
    if (mixedAudioAsset != nil) {
        [mixedAudioAsset release];
        mixedAudioAsset = nil;
    }
	
    if (recordStartTime != nil) {
        [recordStartTime release];
        recordStartTime = nil;
    }
    
    if (finalVideoFileName != nil) {
        [finalVideoFileName release];
        finalVideoFileName = nil;
    }
    
    strcpy(iVidCapGameObject, "");

	[super dealloc];
}


// Remove the fully specified file.
- (void) removeFile: (NSString *) filePath {
    
    NSFileManager *fileManager = [[NSFileManager alloc] init];
    
    // Remove the file if it exists. 
    if ([fileManager fileExistsAtPath:filePath]) {
        [fileManager removeItemAtPath:filePath error:nil];
    }
    
    [fileManager release];
}

// Check for the existence of the specified file.
- (bool) fileExists: (NSString *) filePath {
    
    NSFileManager *fileManager = [[NSFileManager alloc] init];
    
    bool fileExists = [fileManager fileExistsAtPath:filePath];
    
    [fileManager release];
    return fileExists;
}

// Return a URL to the Documents directory that includes the specified filename.
- (NSURL*) getDocumentsFileURL: (NSString * ) fileName {
	NSString* outputPath = [[NSString alloc] initWithFormat:@"%@/%@", [NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES) objectAtIndex:0], fileName];
	NSURL* outputURL = [NSURL fileURLWithPath:outputPath];
	
	[outputPath release];
	return outputURL;
}

// NOTE!  This method removes the file if it already exists.  
- (NSURL*) getFileURLAndRemoveExisting: (NSString * ) fileName {
	
	NSURL* outputURL = [self getDocumentsFileURL:fileName];
    NSString *outputPath = [outputURL path];
	NSFileManager* fileManager = [NSFileManager defaultManager];
	if ([fileManager fileExistsAtPath: outputPath]) {
		NSError* error;
		if ([fileManager removeItemAtPath:outputPath error:&error] == NO) {
			NSLog(@"iVidCapPro - ERROR - Unable to remove old recording file at path:  %@", outputPath);
		}
	}
	
	return outputURL;
}

- (void) setVideoName: (char*) videoName {
    NSString* namePrefix = [NSString stringWithCString:videoName encoding:NSUTF8StringEncoding];
    finalVideoFileName = [[namePrefix stringByAppendingString:videoExtension] retain];
    //finalVideoFileName = [namePrefix stringByAppendingString:videoExtension];
    if (showDebug)
        NSLog(@"setVideoName finalVideoName=%@\n", finalVideoFileName);
}


- (void) setFrameWidth: (int) width Height: (int) height {
    
    frameSize.width = width;
    frameSize.height = height;
    
}

- (int) getFrameWidth {
    return frameSize.width;
}

- (int) getFrameHeight {
    return frameSize.height;
}

- (void) setFrameRate: (int) fps {
    frameRate = fps;
}

- (void) setDebug: (BOOL) show {
    showDebug = show;
}

- (BOOL) getDebug {
    return showDebug;
}

- (void) setTextureID: (GLuint)texID  {
    textureID = texID;
}

- (GLuint) getTextureID {
    return textureID;
}

- (void) setCommGameObject:(char *)objectName {
    
    if (objectName != NULL)
        strcpy(iVidCapGameObject, objectName);
}

- (char*) getCommGameObject {
    return iVidCapGameObject;
}

- (void) setCaptureFramerateLock: (VideoCaptureFramerateLock)capFramerateLock  {
    captureFramerateLock = capFramerateLock;
}

- (VideoCaptureFramerateLock) getCaptureFramerateLock {
    return captureFramerateLock;
}

- (void) setCaptureAudio: (AudioCapture)capAudio  {
    captureAudio = capAudio;
}

- (AudioCapture) getCaptureAudio {
    return captureAudio;
}

- (void) setBitsPerSecond: (int) bps {
    bitsPerSecond = bps;
    useDefaultCompression = false;
}

- (void) setKeyframeInterval: (int) kfInterval {
    keyFrameInterval = kfInterval;
    useDefaultCompression = false;
}

- (void) setGamma: (float)gammaVal {
    gamma = gammaVal;
    useDefaultGamma = false;
}

- (int) getNumDroppedFrames {
    if (movieWriter != nil)
        return [movieWriter numDroppedFrames];
    else
        return 0;
}

- (int) getNumWaitFrames {

    return numWaitFrames;
}

- (NSMutableDictionary *) getVideoSettings {
    
    NSMutableDictionary *videoSettings;
    
    if (useDefaultCompression) {
        videoSettings = [NSMutableDictionary dictionaryWithObjectsAndKeys:
                         AVVideoCodecH264, AVVideoCodecKey,
                         
                         // TESTING...
                         //AVVideoProfileLevelH264Main41, AVVideoProfileLevelKey,
                         
                         [NSNumber numberWithInteger:frameSize.width], AVVideoWidthKey,
                         [NSNumber numberWithInteger:frameSize.height], AVVideoHeightKey,
                         nil];

    } else {
        videoSettings = [NSMutableDictionary dictionaryWithObjectsAndKeys:
                         AVVideoCodecH264, AVVideoCodecKey,
                         
                         [NSNumber numberWithInteger:frameSize.width], AVVideoWidthKey,
                         [NSNumber numberWithInteger:frameSize.height], AVVideoHeightKey,
                         [NSDictionary dictionaryWithObjectsAndKeys:
                           [NSNumber numberWithInteger:bitsPerSecond], AVVideoAverageBitRateKey,
                           [NSNumber numberWithInteger:keyFrameInterval], AVVideoMaxKeyFrameIntervalKey,
                          
                            // TESTING...
                            //AVVideoProfileLevelH264Main41, AVVideoProfileLevelKey,
                          
                           nil], AVVideoCompressionPropertiesKey,
                         nil];
    }
    return videoSettings;
}


-(BOOL) beginRecordingSession {
    
    NSURL* movieFileURL;
    
    // Save the Unity GL context to restore after initializing GPUImage.
    //glFlush();
    glFinish();
    EAGLContext* unity_context = ivcp_UnityGetContext();
    
	//NSError* error = nil;
    
    if (showDebug)
        NSLog(@"beginRecordingSession called...");
    
    //statusCode = OK;
    
    // EXPERIMENT start...
    // Experiment... get full screen texture directly from Unity
    
    //UnityCVTextureInfo cvTextureInfo;
    //ivcp_UnityUseCVTextureCache(&cvTextureInfo);
    
    // Get texture id directly from Unity.
    //GLuint unityRenderTexId = ivcp_UnityGetRenderTexId();
    //NSLog(@"unityRenderTexId=%d", unityRenderTexId);
    
    // Setup GL texture from which we will be capturing images.
    //textureInput = [[GPUImageTextureInput alloc] initWithTexture:unityRenderTexId size:frameSize];
    
    // ... EXPERIMENT end
    
    // Setup GL texture from which we will be capturing images.
    textureInput = [[GPUImageTextureInput alloc] initWithTexture:textureID size:frameSize];
    [textureInput retain];
    
    // Use a temporary file for the movie writer.
    movieFileURL = [self getFileURLAndRemoveExisting:tempVideoFileName];
    
    // Make sure the final mixed audio/video file gets removed if it's still around from a previous session.
    [self getFileURLAndRemoveExisting:mixedAudioFileName];
    [self getFileURLAndRemoveExisting:finalVideoFileName];
    if (showDebug)
        NSLog(@"beginRecordingSession finalVideoName=%@\n", finalVideoFileName);
    
    // Get the combined video settings to use for this video.
    NSMutableDictionary *videoSettings = [self getVideoSettings];
        
    // Create the video writer.
    if (showDebug)
        NSLog(@"beginRecordingSession useDefaultGamma=%d\n", useDefaultGamma);
    if (useDefaultGamma)
    {
        movieWriter = [[GPUImageMovieWriter alloc] initWithMovieURL:movieFileURL size:frameSize fileType:AVFileTypeQuickTimeMovie gammaEnable:false outputSettings:videoSettings];
    }
    else
    {
        movieWriter = [[GPUImageMovieWriter alloc] initWithMovieURL:movieFileURL size:frameSize fileType:AVFileTypeQuickTimeMovie gammaEnable:true outputSettings:videoSettings];
    }
    [movieWriter retain];
    
    [movieWriter setFailureBlock: ^(NSError* error) {
        [self movieWriterErrorHandler:error];
    }];
    
    // If gamma correction is requested, adjust the movie writer gamma setting.
    if (!useDefaultGamma) {
        [movieWriter setGamma:gamma];
    }
    
    // Pipe the texture directly to the movie writer.
    [textureInput addTarget:movieWriter];
    
    //movieWriter.shouldPassthroughAudio = NO;
    //[movieWriter setHasAudioTrack:YES audioSettings:audioOutputSettings];
   
    // An attempted approach to insuring the video is right side up was
    // to set an orientation on the movie writer.  Unfortunately this simply sets a
    // metadata tag in the output video.  Not all video players (e.g. VLC) respect this
    // tag, with the result that the video appears flipped in those players.
    // Instead, the flipping of the video is now explicitly embedded in the render
    // that occurs in the movie writer (renderInternalSize).
    //[movieWriter startRecordingInOrientation:CGAffineTransformMake(1.0, 0.0, 0.0, -1.0, 0.0, 0.0)];
    
    [movieWriter startRecording];
    
    // Restore to Unity's context.
    //glFlush();
    glFinish();
    [EAGLContext setCurrentContext: unity_context];
    
    // Set the video recording start time.
    recordStartTime = [[NSDate alloc] init];
    [recordStartTime retain];
    
    isRecording = true;
    
    if (showDebug)
        NSLog(@"iVidCapPro - beginRecordingSession exiting.");
    
	return YES;
}

- (int) endRecordingSession: (VideoDisposition) action AddAudioFile1:(NSString*) audioFile1 AddAudioFile2:(NSString*) audioFile2 {
    
    if (showDebug)
        NSLog(@"iVidCapPro - endRecordingSession called... action=%d", action);
    
    // Set the final location for this video.
    videoDestination = action;
    
    // Save any audio file names we've been passed.
    if (audioFile1 != nil)
        userAudioFile1 = [[NSString alloc] initWithString:audioFile1];
    if (audioFile2 != nil)
        userAudioFile2 = [[NSString alloc] initWithString:audioFile2];
	
    
    // Make sure all openGL work in the Unity context is complete.
    glFinish();
    
    // Don't think we need this here.  Should be OK to call it in
    // movieWriterCompleteHandler.
    // Save the Unity context to restore after calling GPUImage.
    //EAGLContext* unity_context = ivcp_UnityGetContext();
    
    // When the movie writer is finished, we'll continue completing the recording
    // session.
    [movieWriter finishRecordingWithCompletionHandler:^{
        [self movieWriterCompleteHandler];}];
    
    return 0;
}

// This is the continuation of endRecordingSession.
// It's called when the GPUImage movie writer has completed writing the movie.
- (void) movieWriterCompleteHandler {
    NSLog(@"movieWriterCompleteHandler called.. \n");
    
    if (showDebug) {
        NSLog(@"iVidCapPro - endRecordingSession NUMBER OF FRAMES DROPPED = %d", [movieWriter numDroppedFrames]);
        NSLog(@"iVidCapPro - endRecordingSession NUMBER OF FRAMES WAITED = %d", numWaitFrames);
    }
    
    // Restore to Unity's context.
    glFinish();
    EAGLContext* unity_context = ivcp_UnityGetContext();
    [EAGLContext setCurrentContext: unity_context];
    
    // The pre-mixed video was successfully finalized.
    if (videoDestination == Discard_Video) {
        // We don't want to keep this video.  Remove the related files.
        [self discardVideo];
        
    } else {
        // We're going to keep the video.
        if (showDebug) {
            NSLog(@"iVidCapPro - endRecordingSession tempVideoFileName=%@\n", tempVideoFileName);
            NSLog(@"iVidCapPro - endRecordingSession finalVideoFileName=%@\n", finalVideoFileName);
            if (captureAudio == Audio || captureAudio == Audio_Plus_Mic)
                NSLog(@"iVidCapPro - endRecordingSession capturedAudioFileName=%@\n", capturedAudioFileName);
            else
                NSLog(@"iVidCapPro - endRecordingSession capturedAudioFileName=not in use\n");
            if (userAudioFile1 != nil)
                NSLog(@"iVidCapPro - endRecordingSession userAudioFileName1=%@\n", userAudioFile1);
            else
                NSLog(@"iVidCapPro - endRecordingSession userAudioFileName1=not in use\n");
            if (userAudioFile2 != nil)
                NSLog(@"iVidCapPro - endRecordingSession userAudioFileName2=%@\n", userAudioFile2);
            else
                NSLog(@"iVidCapPro - endRecordingSession userAudioFileName2=not in use\n");
        }
        
        if (captureAudio == Audio || captureAudio == Audio_Plus_Mic || userAudioFile1 != nil || userAudioFile2 != nil) {
            // We have audio.  Mix the audio and video.
            [self createVideoWithAudio];
            
        } else {
            // There's no audio. Create the final video file without mixing.
            [self createVideoWithoutAudio];
        }
    }
    
    
    isRecording = false;
    
    if (showDebug)
        NSLog(@"iVidCapPro - endRecordingSession: exiting.");
    
}


// Final action on EndRecordingSession specified to discard the video.
// Remove the temporary files we created.
- (void) discardVideo {
    
    NSString *filePath;
    
    // Client specified to discard the video.  No point in going any further.
    // Remove any audio/video files now.
    if (showDebug)
        NSLog(@"iVidCapPro - endRecordingSession: Video cancelled. Removing video/audio files.");
    filePath = [[self getDocumentsFileURL:tempVideoFileName] path];
    [self removeFile:filePath];
    filePath = [[self getDocumentsFileURL:capturedAudioFileName] path];
    [self removeFile:filePath];

}

- (void) createVideoWithAudio {
    
    NSURL* userAudioFileURL1 = nil;
    NSURL* userAudioFileURL2 = nil;
    NSURL* capturedAudioFileURL = nil;
    
    if (captureAudio == Audio || captureAudio == Audio_Plus_Mic) {
        // We're capturing audio from scene.  Get the URL for the file.
        capturedAudioFileURL = [self getDocumentsFileURL:capturedAudioFileName];
    }
    
    if (userAudioFile1 != nil) {
        // The user specified an audio file to mix in.  Get the URL for the file.
        // Make sure the specified file exists.
        if ([self fileExists:userAudioFile1])
            userAudioFileURL1 = [NSURL fileURLWithPath:userAudioFile1];
        else if (showDebug)
            NSLog(@"iVidCapPro - createVideoWithAudioUserFile: user specified audio file 1 does not exist. %@", userAudioFile1);
    }
    
    if (userAudioFile2 != nil) {
        // The user specified an audio file to mix in.  Get the URL for the file.
        // Make sure the specified file exists.
        if ([self fileExists:userAudioFile2])
            userAudioFileURL2 = [NSURL fileURLWithPath:userAudioFile2];
        else if (showDebug)
            NSLog(@"iVidCapPro - createVideoWithAudioUserFile: user specified audio file 2 does not exist. %@", userAudioFile2);
    }
    
    NSURL* videoFileURL = [self getDocumentsFileURL:tempVideoFileName];
    NSURL* outputFileURL = [self getDocumentsFileURL:finalVideoFileName];
    
    [self mixAudioFiles:capturedAudioFileURL UserAudioFile1:userAudioFileURL1 UserAudioFile2:userAudioFileURL2 VideoFile:videoFileURL ToOutput:outputFileURL];
    
}

- (void) createVideoWithoutAudio {
    
    NSString *filePath;
    
    // There's no audio file so we don't need to do any mixing, just
    // make use of the video we already have.
    if (videoDestination == Save_Video_To_Album) {
        
        // Move the temp video to the photo album on the device.
        filePath = [[self getDocumentsFileURL:tempVideoFileName] path];
        [self moveVideoToPhotoAlbum:filePath];
        
    } else {
        // We're saving the movie to the documents folder.
        // Rename temp video file to final name.
        NSString *tempFilePath, *newFilePath;
        tempFilePath = [[self getDocumentsFileURL:tempVideoFileName] path];
        newFilePath = [[tempFilePath stringByDeletingLastPathComponent] stringByAppendingPathComponent:finalVideoFileName];
        [[NSFileManager defaultManager] moveItemAtPath:tempFilePath toPath:newFilePath error:nil];
        if (showDebug) {
            NSLog(@"iVidCapPro - endRecordingSession: renamed tempFilePath=%@ to newFilePath=%@", tempFilePath, newFilePath);
        }
        
        // Successful - Send message to completion handler.
        UnitySendMessage(iVidCapGameObject, "PluginCompletionHandler", "Aye, it's done!");
    }

}

- (SessionStatusCode) writeVideoFrameFromRenderTexture {
    
    CMTime time;
    float currentTimeInMilliseconds = [[NSDate date] timeIntervalSinceDate:recordStartTime] * 1000.0;

    if (captureFramerateLock == Unlocked || captureFramerateLock == Throttled) {
        //NSLog(@"writeVideoFrameFromRenderTexture: currentTimeInMilliseconds=%f\n", currentTimeInMilliseconds);
#ifdef IVIDCAPPRO_DEMO
        if (currentTimeInMilliseconds > Demo_TimeLimit) {
            return OK;
        }
#endif
        time = CMTimeMake((int)currentTimeInMilliseconds, 1000);
    } else {
#ifdef IVIDCAPPRO_DEMO
        if (frameNumber > Demo_FrameLimit) {
            return OK;
        }
#endif
        time = CMTimeMake(frameNumber, frameRate);
    }
    
    @synchronized(self) {
        
        if (captureFramerateLock == Locked) {
            // For Locked capture type we want to ensure that every frame is recorded.
            // For this reason we'll wait if the movie writer is not ready for the next frame.
            int waited = 0;
            while (![movieWriter isReadyForNextFrame] && waited < frameWaitLimit) {
                [NSThread sleepForTimeInterval: 0.01f];
                waited++;
            }
            if (waited > 0)
                numWaitFrames++;
        }

    
        [textureInput processTextureWithFrameTime:time];
    }

    frameNumber++;

    return OK;
    
}

- (void) movieWriterErrorHandler: (NSError *) error {
    
    if (showDebug)
        NSLog(@"iVidCapPro - movieWriterErrorHandler: error code=%ldd description=%@", (long)[error code], [error localizedDescription] );
    
    // Any error that occurs during the recording of the video will cause the recording
    // session to be aborted.
    if ([[error localizedDescription] isEqual: @"appendPixelBuffer_failed"])
        [self abortRecordingSession: Failed_FrameCapture];
    else
        [self abortRecordingSession: Failed_Unknown];
}

- (void) moveVideoToPhotoAlbum: (NSString *) videoPath {
    
    if (showDebug)
        NSLog(@"iVidCapPro: Saving temporary video file=%@ to photo album.", videoPath);
    
    if (UIVideoAtPathIsCompatibleWithSavedPhotosAlbum(videoPath)) {
        if (showDebug)
            NSLog(@"iVidCapPro: Video IS compatible. Adding it to photo album.");
        UISaveVideoAtPathToSavedPhotosAlbum(videoPath, self, @selector(copyToPhotoAlbumCompleteFromVideo: didFinishSavingWithError: contextInfo:), nil);
    } else {
        if (showDebug)
            NSLog(@"iVidCapPro: Video IS NOT compatible. Could not be added to the photo album.");
        [self abortRecordingSession: Failed_VideoIncompatible];
    }
    return;
}

- (void) copyToPhotoAlbumCompleteFromVideo: (NSString *) videoPath
                  didFinishSavingWithError: (NSError *) error
                  contextInfo: (void *) contextInfo {
    
    // Tell the Unity side that we've completed writing the video for the current session.
    if (strcmp(iVidCapGameObject, "") != 0)
    {
        if ([error code] == 0) {
            // Successful - Send message to completion handler.
            UnitySendMessage(iVidCapGameObject, "PluginCompletionHandler", "Aye, it's done!");
        }
        else {
            // Failed - Abort the recording session.
            [self abortRecordingSession:Failed_CopyToAlbum];
            if (showDebug)
                NSLog(@"iVidCapPro - copyToPhotoAlbumCompleteFromVideo: Failed! error code=%ld description=%@\n", (long)[error code], [error localizedDescription]);
        }
    }
    
}

-(void) sendErrorMessageToUnity: (NSString*) error ForReason: (NSString*) reason
{
    NSString* message = [NSString stringWithFormat:@"error: %@ reason: %@", error, reason];
    
    UnitySendMessage(iVidCapGameObject, "PluginErrorHandler", [message UTF8String]);

}

// Immediately terminate the recording session and free the resources
// being used.
- (SessionStatusCode) abortRecordingSession: (SessionStatusCode) errorCode {
    
    //statusCode = errorCode;
    
    // Set the global session status code.
    vr_sessionStatus = errorCode;
    
    if (showDebug)
        NSLog(@"iVidCapPro: Video recording aborted. Sending error message to Unity-side plugin.");
    if (errorCode == Failed_CopyToAlbum)
        [self sendErrorMessageToUnity:@"aborted" ForReason:@"failed_copy_to_album"];
    else if (errorCode == Failed_FrameCapture)
        [self sendErrorMessageToUnity:@"aborted" ForReason:@"failed_frame_capture"];
    else if (errorCode == Failed_Memory)
        [self sendErrorMessageToUnity:@"aborted" ForReason:@"failed_memory"];
    else if (errorCode == Failed_VideoIncompatible)
        [self sendErrorMessageToUnity:@"aborted" ForReason:@"failed_video_incompatible"];
    else if (errorCode == Failed_SessionExport)
        [self sendErrorMessageToUnity:@"aborted" ForReason:@"failed_session_export"];
    else if (errorCode == Failed_AssetCouldNotBeLoaded)
        [self sendErrorMessageToUnity:@"aborted" ForReason:@"failed_asset_could_not_be_loaded"];
    else 
        [self sendErrorMessageToUnity:@"aborted" ForReason:@"failed_unknown"];
    
    if (showDebug)
        NSLog(@"iVidCapPro: Video recording aborted. Performing session cleanup.");
    [self cleanupSession];

    return errorCode;
}

- (void)mixAudioFiles:(NSURL*)capturedAudioFileURL UserAudioFile1:(NSURL*) userAudioFileURL1 UserAudioFile2:(NSURL*) userAudioFileURL2 VideoFile: (NSURL*) videoFileURL ToOutput: (NSURL*) outputFileURL {
    
    if (showDebug) {
        if (capturedAudioFileURL != nil)
            NSLog(@"iVidCapPro - mixAudioFiles: captured audio file = %@", capturedAudioFileURL);
        else
            NSLog(@"iVidCapPro - mixAudioFiles: captured audio file = NONE");
        if (userAudioFileURL1 != nil)
            NSLog(@"iVidCapPro - mixAudioFiles: user audio file 1 = %@", userAudioFileURL1);
        else
            NSLog(@"iVidCapPro - mixAudioFiles: user audio file 1 = NONE");
        if (userAudioFileURL2 != nil)
            NSLog(@"iVidCapPro - mixAudioFiles: user audio file 2 = %@", userAudioFileURL2);
        else
            NSLog(@"iVidCapPro - mixAudioFiles: user audio file 2 = NONE");
        NSLog(@"iVidCapPro - mixAudioFiles: video file = %@", videoFileURL);
        NSLog(@"iVidCapPro - mixAudioFiles: output file = %@", outputFileURL);
    }
    
    int numAudioTracks = 0;
    if (capturedAudioFileURL != nil) {
        if (![self LoadCapturedAudioAsset: capturedAudioFileURL]) {
            NSLog(@"iVidCapPro - mixAudioFiles: ERROR - Unable to load audio asset = %@.  Video will not be created!", capturedAudioFileURL);
            [self abortRecordingSession:Failed_AssetCouldNotBeLoaded];
            return;
        }
        numAudioTracks++;
    }
    if (userAudioFileURL1 != nil) {
        if (![self LoadUserAudioAsset1: userAudioFileURL1]) {
            NSLog(@"iVidCapPro - mixAudioFiles: ERROR - Unable to load audio asset = %@.  Video will not be created!", userAudioFileURL1);
            [self abortRecordingSession:Failed_AssetCouldNotBeLoaded];
            return;
        }
        numAudioTracks++;
    }
    if (userAudioFileURL2 != nil) {
        if (![self LoadUserAudioAsset2: userAudioFileURL2]) {
            NSLog(@"iVidCapPro - mixAudioFiles: ERROR - Unable to load audio asset = %@.  Video will not be created!", userAudioFileURL2);
            [self abortRecordingSession:Failed_AssetCouldNotBeLoaded];
            return;
        }
        numAudioTracks++;
    }
    
    if (numAudioTracks > 1) {
        // We have multiple audio tracks.
        // Mix the specified audio files into a single audio track and then create the video.
        NSURL* mixedAudioFileURL = [self getDocumentsFileURL:mixedAudioFileName];
        [self MixAudioTracksToURL: mixedAudioFileURL];
    } else {
        // We have only a single audio track.  No need to perform separate audio mix.
        if (capturedAudioFileURL != nil) {
            if (![self LoadMixedAudioAsset: capturedAudioFileURL]) {
                NSLog(@"iVidCapPro - mixAudioFiles: ERROR - Unable to load audio asset = %@.  Video will not be created!", capturedAudioFileURL);
                [self abortRecordingSession:Failed_AssetCouldNotBeLoaded];
                return;
            }
        } else if (userAudioFileURL1 != nil) {
            if (![self LoadMixedAudioAsset: userAudioFileURL1]) {
                NSLog(@"iVidCapPro - mixAudioFiles: ERROR - Unable to load audio asset = %@.  Video will not be created!", userAudioFileURL1);
                [self abortRecordingSession:Failed_AssetCouldNotBeLoaded];
                return;
            }
        } else {
            if (![self LoadMixedAudioAsset: userAudioFileURL2]) {
                NSLog(@"iVidCapPro - mixAudioFiles: ERROR - Unable to load audio asset = %@.  Video will not be created!", userAudioFileURL2);
                [self abortRecordingSession:Failed_AssetCouldNotBeLoaded];
                return;
            }
        }
        if ( ![self LoadVideoAsset: videoFileURL] ) {
            NSLog(@"iVidCapPro - mixAudioFiles: ERROR - Unable to load video asset = %@.  Video will not be created!", videoFileURL);
            [self abortRecordingSession:Failed_AssetCouldNotBeLoaded];
            return;
        }
        [self ComposeTracksToURL:outputFileURL];
    }
    
}


- (BOOL)LoadVideoAsset:(NSURL*)videoURL {
    videoAsset = [[AVAsset assetWithURL:videoURL] retain];
    if (videoAsset == nil) {
        if (showDebug)
            NSLog(@"iVidCapPro - LoadVideoAsset: Could not load video asset!");
        return false;
    } else {
        if ([[videoAsset tracksWithMediaType:AVMediaTypeVideo] count] < 1) {
            if (showDebug)
                NSLog(@"iVidCapPro - LoadVideoAsset: Video asset contains no video track!");
            [videoAsset release];
            videoAsset = nil;
            return false;
        } else {
            return true;
        }
    }
}

- (BOOL)LoadCapturedAudioAsset:(NSURL*)audioURL {
    
    capturedAudioAsset = [[AVAsset assetWithURL:audioURL] retain];
    if (capturedAudioAsset == nil) {
        if (showDebug)
            NSLog(@"iVidCapPro - LoadCapturedAudioAsset: Could not load audio asset!");
        return false;
    } else {
        if ([[capturedAudioAsset tracksWithMediaType:AVMediaTypeAudio] count] < 1) {
            if (showDebug)
                NSLog(@"iVidCapPro - LoadCapturedAudioAsset: Audio asset contains no audio track!");
            [capturedAudioAsset release];
            capturedAudioAsset = nil;
            return false;
        } else {
            return true;
        }
    }
}

- (BOOL)LoadUserAudioAsset1:(NSURL*)audioURL {
    
    userAudioAsset1 = [[AVAsset assetWithURL:audioURL] retain];
    if (userAudioAsset1 == nil) {
        if (showDebug)
            NSLog(@"iVidCapPro - LoadUserAudioAsset1: Could not load audio asset!");
        return false;
    } else {
        if ([[userAudioAsset1 tracksWithMediaType:AVMediaTypeAudio] count] < 1) {
            if (showDebug)
                NSLog(@"iVidCapPro - LoaduserAudioAsset1: Audio asset contains no audio track!");
            [userAudioAsset1 release];
            userAudioAsset1 = nil;
            return false;
        } else {
            return true;
        }
    }
}

- (BOOL)LoadUserAudioAsset2:(NSURL*)audioURL {
    
    userAudioAsset2 = [[AVAsset assetWithURL:audioURL] retain];
    if (userAudioAsset2 == nil) {
        if (showDebug)
            NSLog(@"iVidCapPro - LoadUserAudioAsset2: Could not load audio asset!");
        return false;
    } else {
        if ([[userAudioAsset2 tracksWithMediaType:AVMediaTypeAudio] count] < 1) {
            if (showDebug)
                NSLog(@"iVidCapPro - LoadUserAudioAsset2: Audio asset contains no audio track!");
            [userAudioAsset2 release];
            userAudioAsset2 = nil;
            return false;
        } else {
            return true;
        }
    }
}

- (BOOL)LoadMixedAudioAsset:(NSURL*)audioURL {
    
    mixedAudioAsset = [[AVAsset assetWithURL:audioURL] retain];
    if (mixedAudioAsset == nil) {
        if (showDebug)
            NSLog(@"iVidCapPro - LoadMixedAudioAsset: Could not load audio asset!");
        return false;
    } else {
        if ([[mixedAudioAsset tracksWithMediaType:AVMediaTypeAudio] count] < 1) {
            if (showDebug)
                NSLog(@"iVidCapPro - LoadMixedAudioAsset: Audio asset contains no audio track!");
            [mixedAudioAsset release];
            mixedAudioAsset = nil;
            return false;
        } else {
            return true;
        }
    }
}


- (int)ComposeTracksToURL: (NSURL*)outputURL {
    
    if (videoAsset != nil && mixedAudioAsset != nil) {
        
        NSError* trackError = nil;
        
        //Create an object to hold our multiple tracks to compose.
        AVMutableComposition* mixComposition = [[AVMutableComposition alloc] init];
        
        // Create a track for our video and insert the video asset contents.
        AVMutableCompositionTrack *videoTrack = [mixComposition addMutableTrackWithMediaType:AVMediaTypeVideo preferredTrackID:kCMPersistentTrackID_Invalid];
        
        // Don't need this as long as we're using a filter to flip every frame.
        //videoTrack.preferredTransform = CGAffineTransformMake(1.0, 0.0, 0.0, -1.0, 0.0, 0.0);
        
        [videoTrack insertTimeRange:CMTimeRangeMake(kCMTimeZero, videoAsset.duration) ofTrack:[[videoAsset tracksWithMediaType:AVMediaTypeVideo] objectAtIndex:0] atTime:kCMTimeZero error:&trackError];
        
        if (trackError != nil) {
            if (showDebug)
                NSLog(@"ComposeTracksToURL: video track result = %@\n", [trackError localizedDescription]);
        }
        
        // Create a track for our audio and insert the audio asset contents.
        // Note that the audio track duration is matched to the length of the video.
        if (mixedAudioAsset != nil) {
            AVMutableCompositionTrack *mixedAudioTrack = [mixComposition addMutableTrackWithMediaType:AVMediaTypeAudio preferredTrackID:kCMPersistentTrackID_Invalid];
            
            // Following lines are strictly for debug...
            //AVAsset* mixedAudioAsset_debug = mixedAudioAsset;
            //NSArray* tracks = [mixedAudioAsset tracksWithMediaType:AVMediaTypeAudio];
            //int numTracks = tracks.count;
            
            [mixedAudioTrack insertTimeRange:CMTimeRangeMake(kCMTimeZero, videoAsset.duration) ofTrack:[[mixedAudioAsset tracksWithMediaType:AVMediaTypeAudio] objectAtIndex:0] atTime:kCMTimeZero error:&trackError];
        }
        
        if (trackError != nil) {
            if (showDebug)
                NSLog(@"ComposeTracksToURL: mixed audio track result = %@\n", [trackError localizedDescription]);
        }
        
        AVMutableVideoCompositionInstruction * MainInstruction = [AVMutableVideoCompositionInstruction videoCompositionInstruction];
        MainInstruction.timeRange = CMTimeRangeMake(kCMTimeZero, videoAsset.duration);
        
        // Create a layer instruction for the video track.
        AVMutableVideoCompositionLayerInstruction *videoLayerInstruction = [AVMutableVideoCompositionLayerInstruction videoCompositionLayerInstructionWithAssetTrack:videoTrack];
        
        MainInstruction.layerInstructions = [NSArray arrayWithObjects:videoLayerInstruction,nil];
        
        AVMutableVideoComposition *MainCompositionInst = [AVMutableVideoComposition videoComposition];
        MainCompositionInst.instructions = [NSArray arrayWithObject:MainInstruction];
        
        // The frame duration must be specified for a valid composition, but it will be
        // overridden by the export session preset.
        MainCompositionInst.frameDuration = CMTimeMake(1, 30);
        MainCompositionInst.renderSize = CGSizeMake(frameSize.width, frameSize.height);
        
        
        // Create the export session and specify its settings.
        //AVAssetExportSession *exporter = [[AVAssetExportSession alloc] initWithAsset:mixComposition presetName:AVAssetExportPresetHighestQuality];
        AVAssetExportSession *exporter = [[AVAssetExportSession alloc] initWithAsset:mixComposition presetName:AVAssetExportPresetPassthrough];
        exporter.outputURL=outputURL;
        exporter.outputFileType = AVFileTypeQuickTimeMovie;
        
        // The export session doesn't use video composition when processing with
        // AVAssetExportPresetPassthrough.  If you change to use a different export
        // preset, you may wish to uncomment the following line to use video composition.
        //exporter.videoComposition = MainCompositionInst;
        exporter.shouldOptimizeForNetworkUse = YES;
        [exporter exportAsynchronouslyWithCompletionHandler:^
         {
             dispatch_async(dispatch_get_main_queue(), ^{
                 [self exportDidFinish:exporter];
             });
         }];
        
        [mixComposition release];
    }
    return 0;
}


// This method is used when there are multiple audio tracks, i.e. an audio track
// captured from the Unity scene and a user-supplied audio file.  It seems that some
// popular video players (VLC, Windows MP) cannot handle multiple audio tracks properly.
// Since we want to use the passthrough export setting when creating the final video,
// we must pre-mix the audio tracks before creating the final video so that the video
// contains a single audio track.
- (int)MixAudioTracksToURL: (NSURL*)outputURL {
        
    NSError* trackError = nil;
    
    //Create an object to hold our multiple tracks to mix.
    AVMutableComposition* mixComposition = [[AVMutableComposition alloc] init];
    
    CMTime mixDuration = kCMTimeZero;
    
    // Choose the longest asset to determine combined length.
    if (capturedAudioAsset != nil)
        mixDuration = capturedAudioAsset.duration;
    if (userAudioAsset1 != nil) {
        if (CMTIME_COMPARE_INLINE(mixDuration, <, userAudioAsset1.duration))
            mixDuration = userAudioAsset1.duration;
    }
    if (userAudioAsset2 != nil) {
        if (CMTIME_COMPARE_INLINE(mixDuration, <, userAudioAsset2.duration))
            mixDuration = userAudioAsset2.duration;
    }
    
    // Create a track for our captured audio.
    if (capturedAudioAsset != nil) {
        AVMutableCompositionTrack *capturedAudioTrack = [mixComposition addMutableTrackWithMediaType:AVMediaTypeAudio preferredTrackID:kCMPersistentTrackID_Invalid];
        
        [capturedAudioTrack insertTimeRange:CMTimeRangeMake(kCMTimeZero, mixDuration) ofTrack:[[capturedAudioAsset tracksWithMediaType:AVMediaTypeAudio] objectAtIndex:0] atTime:kCMTimeZero error:&trackError];
    }
    
    if (trackError != nil) {
        if (showDebug)
            NSLog(@"ComposeTracksToURL: captured audio track result = %@\n", [trackError localizedDescription]);
    }
    
    // Create a track for our user audio 1.
    if (userAudioAsset1 != nil) {
        AVMutableCompositionTrack *userAudioTrack = [mixComposition addMutableTrackWithMediaType:AVMediaTypeAudio preferredTrackID:kCMPersistentTrackID_Invalid];
        
        [userAudioTrack insertTimeRange:CMTimeRangeMake(kCMTimeZero, mixDuration) ofTrack:[[userAudioAsset1 tracksWithMediaType:AVMediaTypeAudio] objectAtIndex:0] atTime:kCMTimeZero error:&trackError];
    }
    
    if (trackError != nil) {
        if (showDebug)
            NSLog(@"ComposeTracksToURL: user audio track 1 result = %@\n", [trackError localizedDescription]);
    }
    
    // Create a track for our user audio 2.
    if (userAudioAsset2 != nil) {
        AVMutableCompositionTrack *userAudioTrack = [mixComposition addMutableTrackWithMediaType:AVMediaTypeAudio preferredTrackID:kCMPersistentTrackID_Invalid];
        
        [userAudioTrack insertTimeRange:CMTimeRangeMake(kCMTimeZero, mixDuration) ofTrack:[[userAudioAsset2 tracksWithMediaType:AVMediaTypeAudio] objectAtIndex:0] atTime:kCMTimeZero error:&trackError];
    }
    
    if (trackError != nil) {
        if (showDebug)
            NSLog(@"ComposeTracksToURL: user audio track 2 result = %@\n", [trackError localizedDescription]);
    }
    
    // Create the export session and specify its settings.
    AVAssetExportSession *exporter = [[AVAssetExportSession alloc] initWithAsset:mixComposition presetName:AVAssetExportPresetAppleM4A];
    exporter.outputURL=outputURL;
    exporter.outputFileType = AVFileTypeAppleM4A;
    exporter.shouldOptimizeForNetworkUse = NO;
    [exporter exportAsynchronouslyWithCompletionHandler:^
     {
         dispatch_async(dispatch_get_main_queue(), ^{
             [self audioMixDidFinish:exporter];
         });
     }];
    
    [mixComposition release];

    return 0;
}

- (void)audioMixDidFinish:(AVAssetExportSession*)session
{
    if (showDebug)
        NSLog(@"iVidCapPro - audioMixDidFinish called... outputURL=%@ status=%ld", session.outputURL, (long)session.status);
    
    if (session.status == AVAssetExportSessionStatusCompleted) {
        
        if (showDebug)
            NSLog(@"iVidCapPro - audioMixDidFinish: session status is 'completed'");
        
        // Our audio mix was successful.  Now create the video.
        if ( ![self LoadMixedAudioAsset: session.outputURL] ) {
            NSLog(@"iVidCapPro - audioMixDidFinish: ERROR - Unable to load audio asset = %@.  Video will not be created!", session.outputURL);
            [self abortRecordingSession:Failed_AssetCouldNotBeLoaded];
            return;
        }
        NSURL* videoFileURL = [self getDocumentsFileURL:tempVideoFileName];
        NSURL* outputFileURL = [self getDocumentsFileURL:finalVideoFileName];
        if ( ![self LoadVideoAsset: videoFileURL] ) {
            NSLog(@"iVidCapPro - audioMixDidFinish: ERROR - Unable to load video asset = %@.  Video will not be created!", videoFileURL);
            [self abortRecordingSession:Failed_AssetCouldNotBeLoaded];
            return;
        }
        [self ComposeTracksToURL:outputFileURL];
        
        
    } else if (session.status == AVAssetExportSessionStatusFailed) {
        if (showDebug) {
            NSLog(@"iVidCapPro - audioMixDidFinish: session failed because: %@", [session.error localizedFailureReason]);
            NSLog(@"iVidCapPro - audioMixDidFinish: session failed, try: %@", [session.error localizedRecoverySuggestion]);
        }
        [self abortRecordingSession:Failed_SessionExport];
    }
    
    if (showDebug)
        NSLog(@"iVidCapPro - audioMixDidFinish: exiting.");
}


- (void)exportDidFinish:(AVAssetExportSession*)session
{
    NSString *filePath;
    if (showDebug)
        NSLog(@"iVidCapPro - exportDidFinish called... outputURL=%@ status=%ld", session.outputURL, (long)session.status);
    
    if (session.status == AVAssetExportSessionStatusCompleted) {
        
        if (showDebug)
            NSLog(@"iVidCapPro - exportDidFinish: session status is 'completed'");
        
        // Move to album if requested, otherwise video is already in documents folder.
        if (videoDestination == Save_Video_To_Album) {
            filePath = [session.outputURL path];
            [self moveVideoToPhotoAlbum:filePath];
        } else {
            // Successful - Send message to completion handler.
            UnitySendMessage(iVidCapGameObject, "PluginCompletionHandler", "Aye, it's done!");
        }
        
    } else if (session.status == AVAssetExportSessionStatusFailed) {
        if (showDebug) {
            NSLog(@"iVidCapPro - exportDidFinish: session failed because: %@", [session.error localizedFailureReason]);
            NSLog(@"iVidCapPro - exportDidFinish: session failed, try: %@", [session.error localizedRecoverySuggestion]);
        }
        [self abortRecordingSession:Failed_SessionExport];
    }
    
    if (showDebug)
        NSLog(@"iVidCapPro - exportDidFinish: exiting.");
}

@end


/* --------------------------------------------------------------------------------
   -- Plugin External Interface --
 
   -------------------------------------------------------------------------------- */

extern "C" {
    
    static ivcp_VideoRecorder *vr;
    static bool ivcp_showDebug = false;
    
    //  Display the specified message using NSLog.
    void ivcp_Log(const char* message)
	{
        NSString *nString = [NSString stringWithCString:message encoding:NSUTF8StringEncoding];

        NSLog(@"%@", nString);
    }
    
    // Initialize a recording session.
    void ivcp_BeginRecordingSession(char* videoName,
                                   int frameWidth, int frameHeight, int frameRate,
                                   GLuint textureID, int captureAudio, int capFramerateLock,
                                   int bitsPerSecond, int keyFrameInterval, float gamma,
                                   char* commObjectName,
                                   bool showDebug)
	{
        ivcp_showDebug = showDebug;
        if (showDebug) {
            NSLog(@"ivcp_BeginRecordingSession called: name=%s width=%i height=%i rate=%i textureID=%i audio=%d rateLock=%d bitsPerSecond=%d keyFrameInterval=%d gamma=%f commObjectName=%s showDebug=%d", videoName, frameWidth, frameHeight, frameRate, textureID, captureAudio, capFramerateLock, bitsPerSecond, keyFrameInterval, gamma, commObjectName, showDebug);
        }
        
        vr_sessionStatus = OK;
        
        vr = [[ivcp_VideoRecorder alloc] init];
        [vr setDebug:showDebug];
        
        [vr setVideoName:videoName];
        
        // For the demo version of the lib the frame size is fixed.
        #ifdef IVIDCAPPRO_DEMO
            //[vr setFrameWidth:Demo_Framewidth Height:Demo_Frameheight];
            [vr setFrameWidth:frameWidth Height:frameHeight];
            if (capFramerateLock == Locked)
                Demo_FrameLimit = frameRate * 10;
        #else
            [vr setFrameWidth:frameWidth Height:frameHeight];
        #endif
        [vr setFrameRate:frameRate];
        [vr setTextureID:textureID];
        
        // Compression values will be -1 if they want the default values.
        if (bitsPerSecond != -1) {
            [vr setBitsPerSecond:bitsPerSecond];
        }
        
        if (keyFrameInterval != -1) {
            [vr setKeyframeInterval:keyFrameInterval];
        }
        
        // Gamma value will be -1 if they don't want gamma correction.
        if (gamma != -1.0) {
            [vr setGamma:gamma];
        }
        
        [vr setCaptureAudio:(AudioCapture)captureAudio];
        [vr setCaptureFramerateLock:(VideoCaptureFramerateLock)capFramerateLock];
        [vr setCommGameObject:commObjectName];
        
        [vr beginRecordingSession];
    }
    
    //  Finalize the recording session and free resources.
    int ivcp_EndRecordingSession(VideoDisposition action)
	{
        int rc;
        
        if (ivcp_showDebug)
            NSLog(@"ivcp_EndRecordingSession called... ");
        
        if (vr_sessionStatus == OK) {
            // End session, finalizing the video file and mixing it with recorded audio as needed.
            rc = [vr endRecordingSession: action AddAudioFile1:nil AddAudioFile2:nil];
        
            if (rc == 0)
                // Session ended successfully.  Return number of frames recorded.
                rc = [vr frameNumber];
        } else {
            // Session was aborted.  Return failure code.
            rc = vr_sessionStatus;
            if (ivcp_showDebug)
                NSLog(@"ivcp_EndRecordingSession detects aborted session rc=%d", rc);
        }
        
        return rc;
    }
    
    //  Finalize the recording session, mix the specified audio file with the video
    //  and free resources.
    int ivcp_EndRecordingSessionWithAudioFiles(VideoDisposition action, char *audioString1, char *audioString2)
	{
        int rc;
        
        if (ivcp_showDebug)
            NSLog(@"ivcp_EndRecordingSessionWithAudioFiles called... action=%d", action);
        
        if (vr_sessionStatus == OK) {
            
            // End session, finalizing the video file and mixing it with the specified audio files.
            NSString* audioFile1 = nil;
            NSString* audioFile2 = nil;
            if (audioString1 != NULL)
                audioFile1 = [NSString stringWithCString:audioString1 encoding:NSUTF8StringEncoding];
            if (audioString2 != NULL)
                audioFile2 = [NSString stringWithCString:audioString2 encoding:NSUTF8StringEncoding];
            rc = [vr endRecordingSession: action AddAudioFile1:audioFile1 AddAudioFile2:audioFile2];
            
            if (rc == 0)
            {
                // Session ended successfully.  Return number of frames recorded.
                rc = [vr frameNumber];
            }
            
        } else {
            // Session was aborted.  Return failure code.
            rc = vr_sessionStatus;
            if (ivcp_showDebug)
                NSLog(@"ivcp_EndRecordingSession detects aborted session rc=%d", rc);
        }
        
        return rc;
    }

    
    
    void ivcp_Release(void)
    {
        // This should never happen.
        if (vr == nil) {
            if (ivcp_showDebug)
                NSLog(@"ivcp_Release called with vr == nil!");
            return;
        }
        
        if (vr != nil) {
            
            if (ivcp_showDebug) {
                NSLog(@"ivcp_Release called... ");
            }
            
            // Release the video recorder resources.
            [vr cleanupSession];
            [vr release];
            vr = nil;
        }
    }
    
    int ivcp_CaptureFrameFromRenderTexture(void)
    {
        if (vr_sessionStatus == OK) {
            //if (ivcp_showDebug) {
            //    NSLog(@"ivcp_CaptureFrameFromRenderTexture called...");
            //}
            
            [vr writeVideoFrameFromRenderTexture];
            
        } else {
            // The session has been aborted.  Return the failing status code.
            if (ivcp_showDebug)
                NSLog(@"ivcp_CaptureFrameFromRenderTexture detects aborted session.  No action taken.");
            return vr_sessionStatus;
        }
        return 0;
    }
    
    void ivcp_Abort(SessionStatusCode errorCode)
    {
        if (vr_sessionStatus != OK) {
            if (ivcp_showDebug)
                NSLog(@"ivcp_Abort detects session already aborted.  No action taken.");
            return;
        }
        
        if (ivcp_showDebug) {
            NSLog(@"ivcp_Abort called... errorCode=%d", errorCode);
        }
        
        [vr abortRecordingSession: errorCode];
        
    }
    
    int ivcp_GetNumDroppedFrames(void)
    {
        if (vr != nil)
            return [vr getNumDroppedFrames];
        else
            return -1;
    }
    
    int ivcp_GetNumWaitFrames(void)
    {
        if (vr != nil)
            return [vr getNumWaitFrames];
        else
            return -1;
    }
    

    
    
    SessionStatusCode ivcp_GetSessionStatusCode(void)
    {
        
        return vr_sessionStatus;
    }

}
