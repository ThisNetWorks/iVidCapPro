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
//  -- iVidCap.h --
//
/* ---------------------------------------------------------------------
 Change History:
 
 - 22 Oct 14 - Created.
 --------------------------------------------------------------------- */


@interface ivcp_Utility : NSObject {
    
    
}

// Whether or not the plugin will display developer debug messages.
@property bool showDebug;

/*
 --------------------------------------------------------------------------------
 -- singleton --
 
 Allocate/init a single instance of the Utility class. It will be shared by all
 users.
 --------------------------------------------------------------------------------
 */
+(ivcp_Utility *)singleton;


/* --------------------------------------------------------------------------------
    -- copyVideoToPhotoAlbum --
 
    Move the specified fully-qualified video file to the device photo album.
 
    CompletionTarget - object that contains the method to invoke when the 
        move is complete.
 
    CompletionSelector - the method to invoke when the move is complete.
 
    If either CompletionTarget or CompletionSelector is nil, the default 
    method will be used.
 
    Returns:
 
        0 => the video is compatible with the photo album and is being moved
       -1 => the video is not compatible; no action taken
   -------------------------------------------------------------------------------- */
- (int) copyVideoToPhotoAlbum: (NSString *) videoPath CompletionTarget: (id) completionTarget CompletionSelector: (SEL) completionSelector;

/* 
 --------------------------------------------------------------------------------
 -- sendMessageToUnityObject --
 
 Send a message to the Unity-side of the plugin.
 
 Returns:
    Nothing.
 -------------------------------------------------------------------------------- 
*/
-(void) sendMessageToUnityObject: (const char*) objectName Method: (const char*) methodName Message: (NSString*) message;

@end

