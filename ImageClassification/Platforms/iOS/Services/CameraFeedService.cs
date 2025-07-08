using System.Diagnostics;
using AVFoundation;
using CoreFoundation;
using CoreGraphics;
using CoreMedia;
using CoreVideo;
using Foundation;
using UIKit;

namespace ImageClassification;

public interface CameraFeedServiceDelegate
{
    //
    // This method delivers the pixel buffer of the current frame seen by the device's camera.
    //
    void DidOutput(CMSampleBuffer sampleBuffer, UIImageOrientation orientation);

    //
    // This method initimates that a session runtime error occured.
    //
    void DidEncounterSessionRuntimeError();

    //
    // This method initimates that the session was interrupted.
    //
    void SessionWasInterrupted(bool canResumeManually);

    //
    // This method initimates that the session interruption has ended.
    //
    void SessionInterruptionEnded();
}

//
// This class manages all camera related functionality
//
public partial class CameraFeedService : NSObject
{
    //
    // This enum holds the state of the camera initialization.
    //
    public enum CameraConfigurationStatus
    {
        Success,
        Failed,
        PermissionDenied
    }

    CGSize VideoResolution
    {
        get
        {
            var size = imageBufferSize;
            var minDimension = Math.Min(size.Width, size.Height);
            var maxDimension = Math.Max(size.Width, size.Height);
            return UIDevice.CurrentDevice.Orientation switch
            {
                UIDeviceOrientation.Portrait => new CGSize(width: minDimension, height: maxDimension),
                UIDeviceOrientation.LandscapeLeft => new CGSize(width: maxDimension, height: minDimension),
                UIDeviceOrientation.LandscapeRight => new CGSize(width: maxDimension, height: minDimension),
                _ => new CGSize(width: minDimension, height: maxDimension)
            };
        }
    }

    AVLayerVideoGravity videoGravity = AVLayerVideoGravity.ResizeAspectFill;

    private AVCaptureSession session = new();
    private AVCaptureVideoPreviewLayer videoPreviewLayer;
    private DispatchQueue sessionQueue = new(label: "com.google.mediapipe.CameraFeedService.sessionQueue");
    private AVCaptureDevicePosition cameraPosition = AVCaptureDevicePosition.Back;

    private CameraConfigurationStatus cameraConfigurationStatus = CameraConfigurationStatus.Failed;
    private AVCaptureVideoDataOutput videoDataOutput = new();
    private bool isSessionRunning = false;
    private CGSize imageBufferSize = CGSize.Empty;

    public CameraFeedServiceDelegate Delegate;

    public CameraFeedService(UIView previewView) : base()
    {
        // Initializes the session
        videoPreviewLayer = new(session: session);
        session.SessionPreset = AVCaptureSession.PresetHigh;
        SetUpPreviewView(previewView);

        AttemptToConfigureSession();
        NSNotificationCenter.DefaultCenter.AddObserver(
            this, aSelector: new ObjCRuntime.Selector("orientationChanged:"),
            aName: UIDevice.OrientationDidChangeNotification,
            anObject: null);
    }

    ~CameraFeedService()
    {
        NSNotificationCenter.DefaultCenter.RemoveObserver(this);
    }

    private void SetUpPreviewView(UIView view)
    {
        videoPreviewLayer.VideoGravity = videoGravity;
        if (videoPreviewLayer.Connection != null)
            videoPreviewLayer.Connection.VideoOrientation = AVCaptureVideoOrientation.Portrait;
        view.Layer.AddSublayer(videoPreviewLayer);
    }

    [Export("orientationChanged:")]
    public void OrientationChanged(NSNotification notification)
    {
        if (videoPreviewLayer.Connection == null)
            return;
        videoPreviewLayer.Connection.VideoOrientation =
            UIDevice.CurrentDevice.Orientation.ToUIImageOrientation() switch
            {
                UIImageOrientation.Up => AVCaptureVideoOrientation.Portrait,
                UIImageOrientation.Left => AVCaptureVideoOrientation.LandscapeRight,
                UIImageOrientation.Right => AVCaptureVideoOrientation.LandscapeLeft,
                _ => videoPreviewLayer.Connection.VideoOrientation
            };
    }

    //
    // This method starts an AVCaptureSession based on whether the camera configuration was successful.
    //
    public void StartLiveCameraSession(Action<CameraConfigurationStatus> completion)
    {
        sessionQueue.DispatchAsync(() =>
        {
            switch (this.cameraConfigurationStatus)
            {
                case CameraConfigurationStatus.Success:
                    this.AddObservers();
                    this.StartSession();
                    break;
                default:
                    break;
            }
            completion(this.cameraConfigurationStatus);
        });
    }

    //
    // This method stops a running an AVCaptureSession.
    //
    public void StopSession()
    {
        this.RemoveObservers();
        sessionQueue.DispatchAsync(() =>
        {
            if (this.session.Running)
            {
                this.session.StopRunning();
                this.isSessionRunning = this.session.Running;
            }
        });
    }

    //
    // This method resumes an interrupted AVCaptureSession.
    //
    public void ResumeInterruptedSession(Action<bool> completion)
    {
        sessionQueue.DispatchAsync(() =>
        {
            this.StartSession();

            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                completion(this.isSessionRunning);
            });
        });
    }

    public void UpdateVideoPreviewLayer(CGRect frame)
    {
        videoPreviewLayer.Frame = frame;
    }

    //
    // This method starts the AVCaptureSession
    //
    private void StartSession()
    {
        this.session.StartRunning();
        this.isSessionRunning = this.session.Running;
    }

    //
    // This method requests for camera permissions and handles the configuration of the session and stores the result of configuration.
    //
    private void AttemptToConfigureSession()
    {
        switch (AVCaptureDevice.GetAuthorizationStatus(mediaType: AVAuthorizationMediaType.Video))
        {
            case AVAuthorizationStatus.Authorized:
                this.cameraConfigurationStatus = CameraConfigurationStatus.Success;
                break;
            case AVAuthorizationStatus.NotDetermined:
                this.sessionQueue.Suspend();
                this.RequestCameraAccess(completion: (granted) =>
                {
                    if (granted)
                        this.sessionQueue.Resume();
                });
                break;
            case AVAuthorizationStatus.Denied:
                this.cameraConfigurationStatus = CameraConfigurationStatus.PermissionDenied;
                break;
            default:
                break;
        }

        this.sessionQueue.DispatchAsync(() =>
            this.ConfigureSession());
    }

    //
    // This method requests for camera permissions.
    //
    private void RequestCameraAccess(Action<bool> completion)
    {
        AVCaptureDevice.RequestAccessForMediaType(mediaType: AVAuthorizationMediaType.Video,
            completion: ((granted) =>
        {
            if (!granted)
            {
                this.cameraConfigurationStatus = CameraConfigurationStatus.PermissionDenied;
            }
            else
            {
                this.cameraConfigurationStatus = CameraConfigurationStatus.Success;
            }
            completion(granted);
        }));
    }

    //
    // This method handles all the steps to configure an AVCaptureSession.
    //
    private void ConfigureSession()
    {
        if (cameraConfigurationStatus != CameraConfigurationStatus.Success)
            return;

        session.BeginConfiguration();

        // Tries to add an AVCaptureDeviceInput.
        if (!AddVideoDeviceInput())
        {
            this.session.CommitConfiguration();
            this.cameraConfigurationStatus = CameraConfigurationStatus.Failed;
            return;
        }

        // Tries to add an AVCaptureVideoDataOutput.
        if (!AddVideoDataOutput())
        {
            this.session.CommitConfiguration();
            this.cameraConfigurationStatus = CameraConfigurationStatus.Failed;
            return;
        }

        session.CommitConfiguration();
        this.cameraConfigurationStatus = CameraConfigurationStatus.Success;
    }

    //
    // This method tries to an AVCaptureDeviceInput to the current AVCaptureSession.
    //
    private bool AddVideoDeviceInput()
    {
        // Tries to get the default back camera.
        //
        var camera = AVCaptureDevice.GetDefaultDevice(AVCaptureDeviceType.BuiltInWideAngleCamera, AVMediaTypes.Video, cameraPosition);
        if (camera == null)
            return false;

        NSError error;
        var videoDeviceInput = new AVCaptureDeviceInput(device: camera, error: out error);
        if (error != null)
            throw new Exception("Cannot create video device input");
        if (session.CanAddInput(videoDeviceInput))
        {
            session.AddInput(videoDeviceInput);
            return true;
        }
        else
        {
            return false;
        }
    }

    //
    // This method tries to an AVCaptureVideoDataOutput to the current AVCaptureSession.
    //
    private bool AddVideoDataOutput()
    {
        var sampleBufferQueue = new DispatchQueue(label: "sampleBufferQueue");
        videoDataOutput.SetSampleBufferDelegate(this, sampleBufferCallbackQueue: sampleBufferQueue);
        videoDataOutput.AlwaysDiscardsLateVideoFrames = true;
        videoDataOutput.WeakVideoSettings = new CVPixelBufferAttributes { PixelFormatType = CVPixelFormatType.CV32BGRA }.Dictionary;

        if (session.CanAddOutput(videoDataOutput))
        {
            session.AddOutput(videoDataOutput);
            videoDataOutput.ConnectionFromMediaType(AVMediaTypes.Video.GetConstant()).VideoOrientation = AVCaptureVideoOrientation.Portrait;
            if (videoDataOutput.ConnectionFromMediaType(AVMediaTypes.Video.GetConstant()).SupportsVideoOrientation == true
                && cameraPosition == AVCaptureDevicePosition.Front)
            {
                videoDataOutput.ConnectionFromMediaType(AVMediaTypes.Video.GetConstant()).VideoMirrored = true;
            }
            return true;
        }
        return false;
    }

    private void AddObservers()
    {
        NSNotificationCenter.DefaultCenter.AddObserver(this, aSelector: new ObjCRuntime.Selector("sessionRuntimeErrorOccured:"), aName: AVCaptureSession.RuntimeErrorNotification, anObject: session);
        NSNotificationCenter.DefaultCenter.AddObserver(this, aSelector: new ObjCRuntime.Selector("sessionWasInterrupted:"), aName: AVCaptureSession.WasInterruptedNotification, anObject: session);
        NSNotificationCenter.DefaultCenter.AddObserver(this, aSelector: new ObjCRuntime.Selector("sessionInterruptionEnded:"), aName: AVCaptureSession.InterruptionEndedNotification, anObject: session);
    }

    private void RemoveObservers()
    {
        NSNotificationCenter.DefaultCenter.RemoveObserver(this, aName: AVCaptureSession.RuntimeErrorNotification, anObject: session);
        NSNotificationCenter.DefaultCenter.RemoveObserver(this, aName: AVCaptureSession.WasInterruptedNotification, anObject: session);
        NSNotificationCenter.DefaultCenter.RemoveObserver(this, aName: AVCaptureSession.InterruptionEndedNotification, anObject: session);
    }

    [Export("sessionWasInterrupted:")]
    void sessionWasInterrupted(NSNotification notification)
    {
        var userInfoValue = notification.UserInfo?.ValueForKey(AVCaptureSession.InterruptionReasonKey) as NSNumber;
        var reasonIntegerValue = userInfoValue.Int64Value;
        var reason = (AVCaptureSessionInterruptionReason)reasonIntegerValue;
        Debug.Print("Capture session was interrupted with reason " + reason.ToString());

        var canResumeManually = false;
        if (reason == AVCaptureSessionInterruptionReason.VideoDeviceInUseByAnotherClient)
            canResumeManually = true;
        else if (reason == AVCaptureSessionInterruptionReason.VideoDeviceNotAvailableWithMultipleForegroundApps)
            canResumeManually = false;

        this.Delegate?.SessionWasInterrupted(canResumeManually: canResumeManually);
    }

    [Export("sessionInterruptionEnded:")]
    void sessionInterruptionEnded(NSNotification notification)
    {
        this.Delegate?.SessionInterruptionEnded();
    }

    [Export("sessionRuntimeErrorOccurred:")]
    void sessionRuntimeErrorOccurred(NSNotification notification)
    {
        var value = notification.UserInfo?.ValueForKey(AVCaptureSession.ErrorKey) as NSNumber;
        if (value == null) return;
        var error = (AVError)value.Int64Value;

        Debug.Print("Capture session runtime error: " + error.ToString());

        if (error == AVError.MediaServicesWereReset)
        {
            sessionQueue.DispatchAsync(() =>
            {
                if (this.isSessionRunning)
                {
                    this.StartSession();
                }
                else
                {
                    DispatchQueue.MainQueue.DispatchAsync(() =>
                    {
                        this.Delegate?.DidEncounterSessionRuntimeError();
                    });
                }
            });
        }
        else
        {
            this.Delegate?.DidEncounterSessionRuntimeError();
        }
    }
}

//
// AVCaptureVideoDataOutputSampleBufferDelegate
//
public partial class CameraFeedService :
    IAVCaptureVideoDataOutputSampleBufferDelegate
{ 
    //
    // This method delegates the CVPixelBuffer of the frame seen by the camera currently.
    //
    [Export("captureOutput:didOutputSampleBuffer:fromConnection:")]
    void DidOutputSampleBuffer(AVCaptureOutput output, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
    {
        using var imageBuffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer;
        if (imageBufferSize.IsEmpty)
        {
            imageBufferSize = new CGSize(width: imageBuffer.Width, height: imageBuffer.Height);
        }
        Delegate?.DidOutput(sampleBuffer: sampleBuffer, orientation: UIDevice.CurrentDevice.Orientation.ToUIImageOrientation());
    }
}

public static class Eextension
{
    public static UIImageOrientation ToUIImageOrientation(this UIDeviceOrientation deviceOrientation) =>
        deviceOrientation switch
        {
            UIDeviceOrientation.Portrait => UIImageOrientation.Up,
            UIDeviceOrientation.LandscapeLeft => UIImageOrientation.Left,
            UIDeviceOrientation.LandscapeRight => UIImageOrientation.Right,
            _ => UIImageOrientation.Up
        };
}
