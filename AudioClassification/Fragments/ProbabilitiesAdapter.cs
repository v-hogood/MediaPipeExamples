using Android.Content.Res;
using Android.Graphics;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using MediaPipe.Tasks.Components.Containers;

namespace AudioClassification;

public class ProbabilitiesAdapter : RecyclerView.Adapter
{
    private IList<Category> categoryList = new List<Category>();

    public void UpdateCategoryList(IList<Category> categoryList)
    {
        this.categoryList = categoryList;
        NotifyDataSetChanged();
    }

    public override RecyclerView.ViewHolder OnCreateViewHolder(
        ViewGroup parent,
        int viewType)
    {
        var view =
            LayoutInflater.From(parent.Context).Inflate(
                Resource.Layout.item_probability,
                parent,
                false
            );
        return new ViewHolder(view);
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var category = categoryList[position];
        (holder as ViewHolder).Bind(category.CategoryName(), category.Score(), category.Index());
    }

    public override int ItemCount => categoryList.Count;

    public class ViewHolder : RecyclerView.ViewHolder
    {
        View view;
        private int[] primaryProgressColorList;
        private int[] backgroundProgressColorList;

        public ViewHolder(View view) : base(view)
        {
            this.view = view;
            primaryProgressColorList =
                view.Context.Resources.GetIntArray(Resource.Array.colors_progress_primary);
            backgroundProgressColorList =
                view.Context.Resources.GetIntArray(Resource.Array.colors_progress_background);
        }

        public void Bind(string label, float score, int index)
        {
            TextView labelTextView = view.FindViewById<TextView>(Resource.Id.label_text_view);
            labelTextView.Text = label;

            ProgressBar progressBar = view.FindViewById<ProgressBar>(Resource.Id.progress_bar);
            progressBar.ProgressBackgroundTintList =
                ColorStateList.ValueOf(new Color(
                    backgroundProgressColorList[index % backgroundProgressColorList.Length])
                );
            progressBar.ProgressTintList =
                ColorStateList.ValueOf(new Color(
                    primaryProgressColorList[index % primaryProgressColorList.Length])
                );

            var newValue = (int)(score * 100);
            progressBar.Progress = newValue;
        }
    }
}
