using AndroidX.Lifecycle;

namespace AudioClassification;

//
//  This ViewModel is used to store image classifier helper settings
//
class MainViewModel : ViewModel
{
    private float _threshold = AudioClassifierHelper.DisplayThreshold;
    private int _maxResults = AudioClassifierHelper.DefaultNumOfResults;
    private int _overlapPosition = AudioClassifierHelper.DefaultOverlap;

    public float Threshold
    {
        get { return _threshold; }
        set { _threshold = value; }
    }
    public int MaxResults
    {
        get { return _maxResults; }
        set { _maxResults = value; }
    }
    public int OverlapPosition
    {
        get { return _overlapPosition; }
        set { _overlapPosition = value; }
    }
}
