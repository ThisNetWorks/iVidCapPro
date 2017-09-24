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
//  -- iVidEdit.h --
//
/* ---------------------------------------------------------------------
 Change History:
 
 - 26 Oct 14 - Created.
 --------------------------------------------------------------------- */

#import "iVidUtil.h"

static const char* OP_COPY_FILE = "copy file to photo album";

static const char* STATUS_OK = "OK";
static const char* STATUS_FAILED = "FAILED";

static const char* REASON_SUCCESS = "success";
static const char* REASON_VID_INCOMPATIBLE = "video incompatible";
static const char* REASON_FAILED_COPY = "copy to album";

@interface ivcp_VideoEditor : NSObject {
    
    // Reference to the Utility singleton object.
    ivcp_Utility* util;
    
    // Whether or not the plugin will display developer debug messages.
    bool showDebug;
    
    // The name of the game object on the Unity-side to which we'll be sending messages.
    char iVidEditUnityObject[256];
    
}

/*
 --------------------------------------------------------------------------------
 -- singleton --
 
 Allocate/init a single instance of the Edit class. It will be shared by all
 users.
 --------------------------------------------------------------------------------
 */
+(ivcp_VideoEditor *)singleton;

/*
 --------------------------------------------------------------------------------
 -- getShowDebug --
 
 Return showDebug flag status.
 --------------------------------------------------------------------------------
 */
-(bool) getShowDebug;

/*
 --------------------------------------------------------------------------------
 -- setShowDebug --
 
 Set showDebug flag status.
 --------------------------------------------------------------------------------
 */
-(void) setShowDebug: (bool) value;


/*
 --------------------------------------------------------------------------------
 -- get/set iVidEditUnityObject --
 --------------------------------------------------------------------------------
 */
-(char*) getIVidEditUnityObject;
-(void)  setIVidEditUnityObject: (char*) value;


/*
 --------------------------------------------------------------------------------
 -- copyVideoFileToPhotoAlbum --
 
 Move the specified fully-qualified video file to the device photo album.
 
 Returns:
 
  0 => the video is compatible with the photo album and is being moved
 -1 => the video is not compatible; no action taken
 -------------------------------------------------------------------------------- 
*/
- (int) copyVideoFileToPhotoAlbum: (NSString *) videoPath;

@end

