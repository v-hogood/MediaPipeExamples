using AndroidX.Lifecycle;

namespace ImageClassification;

//
//  This ViewModel is used to store image classifier helper settings
//
class MainViewModel : ViewModel
{
    private int _delegate = ImageClassifierHelper.DelegateCpu;
    private float _threshold = ImageClassifierHelper.ThresholdDefault;
    private int _maxResults = ImageClassifierHelper.MaxResultsDefault;
    private int _model = ImageClassifierHelper.ModelEfficientnetV0;

    public int Delegate
    {
        get { return _delegate; }
        set { _delegate = value; }
    }
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
    public int Model
    {
        get { return _model; }
        set { _model = value; }
    }
}
