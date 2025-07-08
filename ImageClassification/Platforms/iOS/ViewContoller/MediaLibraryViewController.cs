using AVFoundation;
using AVKit;
using CoreFoundation;
using CoreMedia;
using Foundation;
using MediaPipeTasksVision;
using MobileCoreServices;
using ObjCRuntime;
using UIKit;

namespace ImageClassification;

//
// The view controller is responsible for performing classification on videos or images selected by the user from the device media library and
// presenting the frames with the class of the classified objects to the user.
//
partial class MediaLibraryViewController : UIViewController
{
    public MediaLibraryViewController(NativeHandle handle) : base(handle) { }

    private struct Constants
    {
        public const float InferenceTimeIntervalInMilliseconds = 300;
        public const float MilliSeconds = 1000;
        public const string SavedPhotosNotAvailableText = "Saved photos album is not available.";
        public const string MediaEmptyText =
            "Click + to add an image or a video to begin running the classification.";
        public const float PickFromGalleryButtonInset = 10;
    }

    public InterfaceUpdatesDelegate InterfaceUpdatesDelegate;
    public InferenceResultDeliveryDelegate InferenceResultDeliveryDelegate;

    private UIImagePickerController pickerController = new();
    private AVPlayerViewController playerViewController;

    public ImageClassifierService imageClassifierService;

    private NSObject playerTimeObserverToken;
  
    override public void ViewDidLoad()
    {
        base.ViewDidLoad();
    }
  
    override public void ViewWillLayoutSubviews()
    {
        base.ViewWillLayoutSubviews();
        RedrawBoundingBoxesForCurrentDeviceOrientation();
    }
  
    override public void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        InterfaceUpdatesDelegate?.ShouldClicksBeEnabled(true);

#pragma warning disable CA1422
        if (!UIImagePickerController.IsSourceTypeAvailable(UIImagePickerControllerSourceType.SavedPhotosAlbum))
#pragma warning restore
        {
            pickFromGalleryButton.Enabled = false;
            this.imageEmptyLabel.Text = Constants.SavedPhotosNotAvailableText;
            return;
        }
        pickFromGalleryButton.Enabled = true;
        this.imageEmptyLabel.Text = Constants.MediaEmptyText;
    }
  
    override public void ViewWillDisappear(bool animated)
    {
        base.ViewWillDisappear(animated);
        ClearPlayerView();
        imageClassifierService = null;
    }

    [Export("onClickPickFromGallery:")]
    void OnClickPickFromGallery(UIButton sender)
    {
        InterfaceUpdatesDelegate?.ShouldClicksBeEnabled(true);
        ConfigurePickerController();
        PresentViewController(pickerController, animated: true, null);
    }
    
    private void ConfigurePickerController()
    {
        pickerController.Delegate = this;
#pragma warning disable CA1422
        pickerController.SourceType = UIImagePickerControllerSourceType.SavedPhotosAlbum;
        pickerController.MediaTypes = new[] { (string)UTType.Image, (string)UTType.Movie };
#pragma warning restore
        pickerController.AllowsEditing = false;
    }

    private void AddPlayerViewControllerAsChild()
    {
        if (playerViewController == null)
            return;
        playerViewController.View.TranslatesAutoresizingMaskIntoConstraints = false;

        this.AddChildViewController(playerViewController);
        this.View.AddSubview(playerViewController.View);
        this.View.BringSubviewToFront(this.pickFromGalleryButton);
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            playerViewController.View.LeadingAnchor.ConstraintEqualTo(
                anchor: View.LeadingAnchor, constant: 0),
            playerViewController.View.TrailingAnchor.ConstraintEqualTo(
                anchor: View.TrailingAnchor, constant: 0),
            playerViewController.View.TopAnchor.ConstraintEqualTo(
                anchor: View.TopAnchor, constant: 0),
            playerViewController.View.BottomAnchor.ConstraintEqualTo(
                anchor: View.BottomAnchor, constant: 0),
        });

        playerViewController.DidMoveToParentViewController(parent: this);
    }
  
    private void RemovePlayerViewController()
    {
        if (playerViewController == null)
            return;
        RemoveObservers(player: playerViewController?.Player);
        playerViewController?.Player?.Pause();
        playerViewController.Player = null;

        playerViewController?.View.RemoveFromSuperview();
        playerViewController?.WillMoveToParentViewController(parent: null);
        playerViewController?.RemoveFromParentViewController();
    }

    private void RemoveObservers(AVPlayer player)
    {
        if (player == null)
            return;

        var timeObserverToken = playerTimeObserverToken;
        if (timeObserverToken != null)
        {
            player.RemoveTimeObserver(timeObserverToken);
            playerTimeObserverToken = null;
        }   
    }

    private void OpenMediaLibrary()
    {
        ConfigurePickerController();
        PresentViewController(pickerController, animated: true, null);
    }
  
    private void ClearPlayerView()
    {
        imageEmptyLabel.Hidden = false;
        RemovePlayerViewController();
    }
  
    private void ShowProgressView()
    {
        var progressSuperview = progressView.Superview?.Superview;
        if (progressSuperview == null)
            return;
        progressSuperview.Hidden = false;
        progressView.Progress = 0;
        progressView.ObservedProgress = null;
        this.View.BringSubviewToFront(progressSuperview);
    }
  
    private void HideProgressView()
    {
        var progressSuperview = progressView.Superview?.Superview;
        if (progressSuperview == null)
            return;
        this.View.SendSubviewToBack(progressSuperview);
        this.progressView.Superview.Superview.Hidden = true;
    }
  
    public void LayoutUIElements(float height)
    {
        pickFromGalleryButtonBottomSpace.Constant =
            height + Constants.PickFromGalleryButtonInset;
        View.LayoutSubviews();
    }

    void RedrawBoundingBoxesForCurrentDeviceOrientation()
    {
        if (imageClassifierService == null ||
            // imageClassifierService.RunningMode == MediaPipeTasksVision.MPPRunningMode.Image ||
            this.playerViewController?.Player?.TimeControlStatus == AVPlayerTimeControlStatus.Paused)
            return;
    }
  
    ~MediaLibraryViewController()
    {
        playerViewController?.Player?.RemoveTimeObserver(this);
    }
}

partial class MediaLibraryViewController : IUIImagePickerControllerDelegate, IUINavigationControllerDelegate
{
    [Export("imagePickerControllerDidCancel:")]
    void ImagePickerControllerDidCancel(UIImagePickerController picker)
    {
        picker.DismissViewController(animated: true, null);
    }

    [Export("imagePickerController:didFinishPickingMediaWithInfo:")]
    void DidFinishPickingMedia(UIImagePickerController picker, NSDictionary info)
    {
        ClearPlayerView();
        pickedImageView.Image = null;

        picker.DismissViewController(animated: true, null);

        var mediaType = info[UIImagePickerController.MediaType] as NSString;
        if (mediaType == null)
            return;

#pragma warning disable CA1422
        if (mediaType.Equals(UTType.Movie))
#pragma warning restore
        {
            var mediaURL = info[UIImagePickerController.MediaURL] as NSUrl;
            if (mediaURL == null)
            {
                imageEmptyLabel.Hidden = false;
                return;
            }
            ClearAndInitializeImageClassifierService(runningMode: MPPRunningMode.Video);
            var asset = AVAsset.FromUrl(url: mediaURL);
            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                InterfaceUpdatesDelegate?.ShouldClicksBeEnabled(false);
                ShowProgressView();

                var videoDuration = asset.Duration.Seconds;

                var resultBundle = this.imageClassifierService?.Classify(
                    videoAsset: asset,
                    durationInMilliseconds: videoDuration * Constants.MilliSeconds,
                    inferenceIntervalInMilliseconds: Constants.InferenceTimeIntervalInMilliseconds);

                HideProgressView();

                PlayVideo(
                  mediaURL: mediaURL,
                  videoDuration: videoDuration,
                  resultBundle: resultBundle);
            });

            imageEmptyLabel.Hidden = true;
        }
#pragma warning disable CA1422
        else if (mediaType.Equals(UTType.Image))
#pragma warning restore
        {
            var image = info[UIImagePickerController.OriginalImage] as UIImage;
            if (image == null)
            {
                imageEmptyLabel.Hidden = false;
                return;
            }
            pickedImageView.Image = image;
            imageEmptyLabel.Hidden = true;

            ShowProgressView();

            ClearAndInitializeImageClassifierService(runningMode: MPPRunningMode.Image);

            DispatchQueue.GetGlobalQueue(DispatchQualityOfService.UserInteractive).
                DispatchAsync(() =>
                {
                    var imageClassifierResult = this.imageClassifierService?.Classify(image: image);
                    if (imageClassifierResult == null)
                    {
                        DispatchQueue.MainQueue.DispatchAsync(() =>
                            this.HideProgressView());
                        return;
                    }

                    DispatchQueue.MainQueue.DispatchAsync(() =>
                    {
                        this.HideProgressView();
                        this.InferenceResultDeliveryDelegate?.DidPerformInference(result: imageClassifierResult);
                    });
                });
        }
    }
  
    void ClearAndInitializeImageClassifierService(MPPRunningMode runningMode)
    {
        imageClassifierService = null;
        switch (runningMode)
        {
            case MPPRunningMode.Image:
                imageClassifierService = ImageClassification.ImageClassifierService
                    .StillImageClassifierService(
                        model: InferenceConfigurationManager.SharedInstance.Model,
                        scoreThreshold: InferenceConfigurationManager.SharedInstance.ScoreThreshold,
                        maxResult: InferenceConfigurationManager.SharedInstance.MaxResults,
                        imageClassifierDelegate: InferenceConfigurationManager.SharedInstance.Delegate
                );
                break;
            case MPPRunningMode.Video:
                imageClassifierService = ImageClassification.ImageClassifierService
                    .VideoClassifierService(
                        model: InferenceConfigurationManager.SharedInstance.Model,
                        scoreThreshold: InferenceConfigurationManager.SharedInstance.ScoreThreshold,
                        maxResult: InferenceConfigurationManager.SharedInstance.MaxResults,
                        videoDelegate: this,
                        imageClassifierDelegate: InferenceConfigurationManager.SharedInstance.Delegate);
                break;
            default:
                break;
        }
    }

    private void PlayVideo(NSUrl mediaURL, double videoDuration, ResultBundle? resultBundle)
    {
        PlayVideo(asset: AVAsset.FromUrl(url: mediaURL));
        playerTimeObserverToken = playerViewController?.Player?.AddPeriodicTimeObserver(
            interval: new CMTime(value: (long)Constants.InferenceTimeIntervalInMilliseconds,
                                 timescale: (int)Constants.MilliSeconds),
            queue: new DispatchQueue(label: "com.google.mediapipe.MediaLibraryViewController.timeObserverQueue",
                                     attributes: new DispatchQueue.Attributes() { QualityOfService = DispatchQualityOfService.UserInteractive }),
            handler: ((CMTime time) =>
            {
                DispatchQueue.MainQueue.DispatchAsync(() =>
                {
                    var index = time.Seconds * Constants.MilliSeconds / Constants.InferenceTimeIntervalInMilliseconds;
                    if (resultBundle == null)
                        return;
                    this.InferenceResultDeliveryDelegate?.DidPerformInference(result: resultBundle, index: (int)index);

                    // Enable clicks on inferenceVC if playback has ended.
                    if (Math.Floor(time.Seconds +
                        Constants.InferenceTimeIntervalInMilliseconds / Constants.MilliSeconds)
                        >= Math.Floor(videoDuration))
                    {
                        this.InterfaceUpdatesDelegate?.ShouldClicksBeEnabled(true);
                    }
                });
            })
        );
    }

    private void PlayVideo(AVAsset asset)
    {
        if (playerViewController == null)
        {
            var playerViewController = new AVPlayerViewController();
            this.playerViewController = playerViewController;
        }

        var playerItem = new AVPlayerItem(asset: asset);
        var player = playerViewController?.Player;
        if (player != null)
        {
            player.ReplaceCurrentItemWithPlayerItem(item: playerItem);
        }
        else
        {
            playerViewController.Player = new AVPlayer(item: playerItem);
        }

        playerViewController.ShowsPlaybackControls = false;
        AddPlayerViewControllerAsChild();
        playerViewController?.Player?.Play();
    }
}

public partial class MediaLibraryViewController : ImageClassifierServiceVideoDelegate
{
    public void ImageClassifierService(
        ImageClassifierService imageClassifierService,
        int index,
        NSError error)
    {
        progressView.ObservedProgress.CompletedUnitCount = index + 1;
    }
  
    public void ImageClassifierService(
        ImageClassifierService imageClassifierService,
        int totalframeCount)
    {
        progressView.ObservedProgress = NSProgress.FromTotalUnitCount(unitCount: totalframeCount);
    }
}
