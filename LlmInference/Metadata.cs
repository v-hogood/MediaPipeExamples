#if __IOS__
using UIKit;

namespace LlmInference;

public static class Metadata
{
    public static UIColor GlobalColor = UIColor.FromName("AppColor") ?? UIColor.SystemBlue;
}
#endif