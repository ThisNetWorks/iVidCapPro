#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>
#import "GPUImageOpenGLESContext.h"

extern NSString *const kGPUImageColorSwizzlingFragmentShaderString;

// eOe
extern NSString *const kGPUImageSwizzleAndGammaFragmentShaderString;

@protocol GPUImageMovieWriterDelegate <NSObject>

@optional
- (void)movieRecordingCompleted;
- (void)movieRecordingFailedWithError:(NSError*)error;

@end

@interface GPUImageMovieWriter : NSObject <GPUImageInput>
{
    CMVideoDimensions videoDimensions;
	CMVideoCodecType videoType;

    NSURL *movieURL;
    NSString *fileType;
	AVAssetWriter *assetWriter;
	AVAssetWriterInput *assetWriterAudioInput;
	AVAssetWriterInput *assetWriterVideoInput;
    AVAssetWriterInputPixelBufferAdaptor *assetWriterPixelBufferInput;
	dispatch_queue_t movieWritingQueue;
    
    CVOpenGLESTextureCacheRef coreVideoTextureCache;
    CVPixelBufferRef renderTarget;
    CVOpenGLESTextureRef renderTexture;

    CGSize videoSize;
    GPUImageRotationMode inputRotation;
    
    // eOe start...
    GLint gammaUniform;
    // eOe end
}

@property(readwrite, nonatomic) BOOL hasAudioTrack;
@property(readwrite, nonatomic) BOOL shouldPassthroughAudio;
@property(nonatomic, copy) void(^completionBlock)(void);
@property(nonatomic, copy) void(^failureBlock)(NSError*);
@property(nonatomic, assign) id<GPUImageMovieWriterDelegate> delegate;
@property(readwrite, nonatomic) BOOL encodingLiveVideo;
@property(nonatomic, copy) void(^videoInputReadyCallback)(void);
@property(nonatomic, copy) void(^audioInputReadyCallback)(void);
@property(nonatomic) BOOL enabled;

// eOe start...
// Gamma ranges from 0.0 to 3.0, with 1.0 as the normal level
@property(readwrite, nonatomic) CGFloat gamma;
@property(readwrite, nonatomic) int numDroppedFrames;
// eOe end

// Initialization and teardown
- (id)initWithMovieURL:(NSURL *)newMovieURL size:(CGSize)newSize;
// - (id)initWithMovieURL:(NSURL *)newMovieURL size:(CGSize)newSize fileType:(NSString *)newFileType outputSettings:(NSMutableDictionary *)outputSettings;
// eOe
- (id)initWithMovieURL:(NSURL *)newMovieURL size:(CGSize)newSize fileType:(NSString *)newFileType gammaEnable:(bool)allowGammaAdjust outputSettings:(NSMutableDictionary *)outputSettings;

- (void)setHasAudioTrack:(BOOL)hasAudioTrack audioSettings:(NSDictionary *)audioOutputSettings;

// Movie recording
- (void)startRecording;
- (void)startRecordingInOrientation:(CGAffineTransform)orientationTransform;
- (void)finishRecording;
- (void)finishRecordingWithCompletionHandler:(void (^)(void))handler;
- (void)recordingCompleteHandler;
- (void)processAudioBuffer:(CMSampleBufferRef)audioBuffer;
- (void)enableSynchronizationCallbacks;
- (bool)isReadyForNextFrame;

@end
