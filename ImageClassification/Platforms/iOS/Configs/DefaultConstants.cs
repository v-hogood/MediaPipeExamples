using Foundation;
using MediaPipeTasksVision;
using UIKit;

namespace ImageClassification;

public struct DefaultConstants
{
    public const int MaxResults = 3;
    public const float ScoreThreshold = 0.2f;
    static UIColor[] LabelColors =
    {
        UIColor.Red,
        new(red: 90.0f/255.0f, green: 200.0f/255.0f, blue: 250.0f/255.0f, alpha: 1.0f),
        UIColor.Green,
        UIColor.Orange,
        UIColor.Blue,
        UIColor.Purple,
        UIColor.Magenta,
        UIColor.Yellow,
        UIColor.Cyan,
        UIColor.Brown
    };
    static UIColor OverlayColor = new(red: 0, green: 127 / 255.0f, blue: 139 / 255.0f, alpha: 1);
    static UIFont DisplayFont = UIFont.SystemFontOfSize(size: 14.0f, weight: UIFontWeight.Medium);
    public const Model model = Model.EfficientnetLite0;
    public const ImageClassifierDelegate Delegate = ImageClassifierDelegate.CPU;
}

public enum Model
{
    EfficientnetLite0,
    EfficientnetLite2
}

public enum ImageClassifierDelegate
{
    CPU,
    GPU
}

public static class Extensions
{
    public static Model ToModel(this string text) =>
       text switch
       {
           "EfficientNetLite0" => Model.EfficientnetLite0,
           "EfficientNetLite2" => Model.EfficientnetLite2,
           _ => Model.EfficientnetLite0
       };

    public static string ModelPath(this Model model) =>
        model switch
        {
            Model.EfficientnetLite0 => NSBundle.MainBundle.PathForResource(
                "efficientnet_lite0", ofType: "tflite"),
            Model.EfficientnetLite2 => NSBundle.MainBundle.PathForResource(
                "efficientnet_lite2", ofType: "tflite"),
            _ => ""
        };

    public static ImageClassifierDelegate ToDelegate(this string text) =>
       text switch
       {
           "CPU" => ImageClassifierDelegate.CPU,
           "GPU" => ImageClassifierDelegate.GPU,
           _ => ImageClassifierDelegate.CPU
       };

    public static MPPDelegate Delegate(this ImageClassifierDelegate imageClassifierDelegate) =>
        imageClassifierDelegate switch
        {
            ImageClassifierDelegate.CPU => MPPDelegate.Cpu,
            ImageClassifierDelegate.GPU => MPPDelegate.Gpu,
            _ => MPPDelegate.Cpu
        };
}
