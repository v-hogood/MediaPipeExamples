using AVFoundation;
using CoreFoundation;
using CoreMedia;
using Foundation;
using ObjCRuntime;
using UIKit;
using static ImageClassification.CameraFeedService;

namespace ImageClassification;

//
// The view controller is responsible for performing classification on incoming frames from the live camera and presenting the frames with the
// class of the classified objects to the user.
//
public partial class CameraViewController : UIViewController
{
    public CameraViewController(NativeHandle handle) : base(handle) { }

    public InferenceResultDeliveryDelegate InferenceResultDeliveryDelegate;
    public InterfaceUpdatesDelegate InterfaceUpdatesDelegate;

    private bool isObserving = false;
    private DispatchQueue backgroundQueue = new(label: "com.google.mediapipe.cameraController.backgroundQueue");

    // Handles all the camera related functionality
    private CameraFeedService cameraFeedService;

    private DispatchQueue imageClassifierServiceQueue = new(
      label: "com.google.mediapipe.cameraController.imageClassifierServiceQueue",
      attributes: new DispatchQueue.Attributes() { Concurrent = false });
  
    private ImageClassifierService imageClassifierService { get; set; }

    override public void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        InitializeImageClassifierServiceOnSessionResumption();
        cameraFeedService.StartLiveCameraSession((cameraConfiguration) =>
            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                switch (cameraConfiguration)
                {
                    case CameraConfigurationStatus.Failed:
                        this.PresentVideoConfigurationErrorAlert();
                        break;
                    case CameraConfigurationStatus.PermissionDenied:
                        this.PresentCameraPermissionsDeniedAlert();
                        break;
                    default:
                        break;
                }
            }));
    }      
  
    override public void ViewWillDisappear(bool animated)
    {
        base.ViewWillDisappear(animated);
        cameraFeedService.StopSession();
        ClearImageClassifierServiceOnSessionInterruption();
    }
  
    override public void ViewDidLoad()
    {
        base.ViewDidLoad();
        cameraFeedService = new(previewView: previewView);
        cameraFeedService.Delegate = this;
        // Do any additional setup after loading the view.
    }
  
    override public void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        cameraFeedService.UpdateVideoPreviewLayer(frame: previewView.Bounds);
    }
  
    override public void ViewWillLayoutSubviews()
    {
        base.ViewWillLayoutSubviews();
        cameraFeedService.UpdateVideoPreviewLayer(frame: previewView.Bounds);
    }

    // Resume camera session when click button resume
    [Export("onClickResume:")]
    void OnClickResume(UIButton sender)
    {
        cameraFeedService.ResumeInterruptedSession((isSessionRunning) =>
        {
            if (isSessionRunning)
            {
                this.resumeButton.Hidden = true;
                this.cameraUnavailableLabel.Hidden = true;
                this.InitializeImageClassifierServiceOnSessionResumption();
            }
        });
    }
  
    private void PresentCameraPermissionsDeniedAlert()
    {
        var alertController = UIAlertController.Create(
            title: "Camera Permissions Denied",
            message:
                "Camera permissions have been denied for this app. You can change this by going to Settings",
            preferredStyle: UIAlertControllerStyle.Alert);

        var cancelAction = UIAlertAction.Create(title: "Cancel", style: UIAlertActionStyle.Cancel, handler: null);
        var settingsAction = UIAlertAction.Create(title: "Settings", style: UIAlertActionStyle.Default, ((_) =>
#pragma warning disable CA1422
            UIApplication.SharedApplication.OpenUrl(
                new NSUrl(UIApplication.OpenSettingsUrlString))));
#pragma warning restore

        alertController.AddAction(cancelAction);
        alertController.AddAction(settingsAction);

        PresentViewController(alertController, animated: true, completionHandler: null);
    }
  
    private void PresentVideoConfigurationErrorAlert()
    {
        var alert = UIAlertController.Create(
          title: "Camera Configuration Failed",
          message: "There was an error while configuring camera.",
          preferredStyle: UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create(title: "OK", style: UIAlertActionStyle.Default, handler: null));

        this.PresentViewController(alert, animated: true, completionHandler: null);
    }
  
    private void InitializeImageClassifierServiceOnSessionResumption()
    {
        ClearAndInitializeImageClassifierService();
        StartObserveConfigChanges();
    }

    [Export("clearAndInitializeImageClassifierService")]
    private void ClearAndInitializeImageClassifierService()
    {
        imageClassifierService = null;
        imageClassifierService = ImageClassification.ImageClassifierService
            .LiveStreamClassifierService(
                model: InferenceConfigurationManager.SharedInstance.Model,
                scoreThreshold: InferenceConfigurationManager.SharedInstance.ScoreThreshold,
                maxResult: InferenceConfigurationManager.SharedInstance.MaxResults,
                liveStreamDelegate: this,
                imageClassifierDelegate: InferenceConfigurationManager.SharedInstance.Delegate);
    }
  
    private void ClearImageClassifierServiceOnSessionInterruption()
    {
        StopObserveConfigChanges();
        imageClassifierService = null;
    }
  
    private void StartObserveConfigChanges()
    {
        NSNotificationCenter.DefaultCenter
            .AddObserver(this,
                aSelector: new ObjCRuntime.Selector("clearAndInitializeImageClassifierService"),
                aName: (NSString)InferenceConfigurationManager.NotificationName,
                anObject: null);
        isObserving = true;
    }
  
    private void StopObserveConfigChanges()
    {
        if (isObserving)
        {
            NSNotificationCenter.DefaultCenter
                .RemoveObserver(this,
                    aName: InferenceConfigurationManager.NotificationName,
                    anObject: null);
        }
        isObserving = false;
    }
}

partial class CameraViewController : CameraFeedServiceDelegate
{
    public void DidOutput(CMSampleBuffer sampleBuffer, UIImageOrientation orientation)
    {
        var currentTimeMs = new NSDate().SecondsSince1970 * 1000;
        // Pass the pixel buffer to mediapipe
        backgroundQueue.DispatchAsync(() =>
            this.imageClassifierService?.ClassifyAsync(
                sampleBuffer: sampleBuffer,
                orientation: orientation,
                timeStamp: (nint)currentTimeMs));
    }
  
    public void SessionWasInterrupted(bool resumeManually)
    {
        // Updates the UI when session is interupted.
        if (resumeManually)
        {
            resumeButton.Hidden = false;
        }
        else
        {
            cameraUnavailableLabel.Hidden = false;
        }
        ClearImageClassifierServiceOnSessionInterruption();
    }
  
    public void SessionInterruptionEnded()
    {
        // Updates UI once session interruption has ended.
        cameraUnavailableLabel.Hidden = true;
        resumeButton.Hidden = true;
        InitializeImageClassifierServiceOnSessionResumption();
    }
  
    public void DidEncounterSessionRuntimeError()
    {
        // Handles session run time error by updating the UI and providing a button if session can be
        // manually resumed.
        resumeButton.Hidden = false;
        ClearImageClassifierServiceOnSessionInterruption();
    }
}

partial class CameraViewController : ImageClassifierServiceLiveStreamDelegate
{
    public void ImageClassifierService(ImageClassifierService imageClassifierService, ResultBundle? result, NSError error)
    {
        DispatchQueue.MainQueue.DispatchAsync(() =>
            this.InferenceResultDeliveryDelegate?.DidPerformInference(result: result));
    }
}

static class Extension
{
    static UIViewContentMode ContentMode(this AVLayerVideoGravity gravity)
    {
        return gravity switch
        {
            AVLayerVideoGravity.ResizeAspectFill => UIViewContentMode.ScaleAspectFill,
            AVLayerVideoGravity.ResizeAspect => UIViewContentMode.ScaleAspectFit,
            AVLayerVideoGravity.Resize => UIViewContentMode.ScaleToFill,
            _ => UIViewContentMode.ScaleAspectFill
        };
    }
}
