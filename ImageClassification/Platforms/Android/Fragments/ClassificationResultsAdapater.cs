using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using MediaPipe.Tasks.Components.Containers;
using MediaPipe.Tasks.Vision.ImageClassifier;
using View = Android.Views.View;

namespace ImageClassification;

public class ClassificationResultsAdapter : RecyclerView.Adapter
{
    private const string NoValue = "--";

    private IList<Category> categories = new List<Category>();
    private int adapterSize = 0;

    public void UpdateResults(ImageClassifierResult imageClassifierResult)
    {
        categories = Enumerable.Repeat<Category>(null, adapterSize).ToList();
        if (imageClassifierResult != null)
        {
            var sortedCategories = imageClassifierResult.ClassificationResult()
                .Classifications()[0].Categories().OrderBy(it => it.Index()).ToList();
            var min = Math.Min(sortedCategories.Count, categories.Count);
            for (int i = 0; i < min; i++)
            {
                categories[i] = sortedCategories[i];
            }
        }
    }

    public void UpdateAdapterSize(int size)
    {
        adapterSize = size;
    }

    public override RecyclerView.ViewHolder OnCreateViewHolder(
        ViewGroup parent,
        int viewType)
    {
        var view = LayoutInflater.From(parent.Context).Inflate(
            Resource.Layout.item_classification_result,
            parent,
            false);
        return new ViewHolder(view);
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var viewHolder = holder as ViewHolder;
        var category = categories[position];
        viewHolder.Bind(category?.CategoryName(), category?.Score());
    }

    public override int ItemCount => categories.Count;

    public class ViewHolder : RecyclerView.ViewHolder
    {
        public ViewHolder(View view) : base(view) { }

        public void Bind(string label, float? score)
        {
            ItemView.FindViewById<TextView>(Resource.Id.tvLabel).Text =
                label ?? NoValue;
            ItemView.FindViewById<TextView>(Resource.Id.tvScore).Text =
                score != null ? string.Format("{0:0.00}", score) : NoValue;
        }
    }
}
