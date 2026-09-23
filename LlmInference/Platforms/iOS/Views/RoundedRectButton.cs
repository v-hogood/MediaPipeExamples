using CoreGraphics;
using UIKit;

namespace LlmInference;

public class RoundedRectButton : UIButton
{
    public RoundedRectButton(string title, UIColor backgroundColor = null, UIColor foregroundColor = null, UIImage logo = null)
        : base(UIButtonType.System)
    {
        backgroundColor = backgroundColor ?? Metadata.GlobalColor;
        foregroundColor = foregroundColor ?? UIColor.White;

        var config = UIButtonConfiguration.FilledButtonConfiguration;
        config.Title = title;
        config.BaseBackgroundColor = backgroundColor;
        config.BaseForegroundColor = foregroundColor;
        config.CornerStyle = UIButtonConfigurationCornerStyle.Capsule;
        config.ContentInsets = new NSDirectionalEdgeInsets(top: 10, leading: 20, bottom: 10, trailing: 20);

        if (logo != null)
        {
            config.Image = logo;
            config.ImagePlacement = NSDirectionalRectEdge.Leading;
            config.ImagePadding = 8;
        }

        this.Configuration = config;
    
        // Add subtle shadow
        this.Layer.ShadowColor = UIColor.Black.CGColor;
        this.Layer.ShadowOpacity = 0.2f;
        this.Layer.ShadowOffset = new CGSize(width: 0, height: 2);
        this.Layer.ShadowRadius = 3;
    }

    public RoundedRectButton(IntPtr handle) : base(handle) { }
}

public sealed class HuggingFaceButton : RoundedRectButton
{
    public HuggingFaceButton(string title)
        : base(title, UIColor.Black, UIColor.White, UIImage.FromBundle("HfLogo")) { }

    public HuggingFaceButton(IntPtr handle) : base(handle) { }
}
