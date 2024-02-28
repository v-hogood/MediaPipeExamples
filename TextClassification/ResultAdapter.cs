using System.Globalization;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using MediaPipe.Tasks.Components.Containers;

namespace TextClassification
{
    // An immutable result returned by a TextClassifier describing what was classified.
    public class ResultsAdapter : RecyclerView.Adapter
    {
        private List<Category> resultsList = new();
        private string currentModel = TextClassifierHelper.WordVec;

        public class ViewHolder : RecyclerView.ViewHolder
        {
            public ViewHolder(View view) : base(view) { }

            public void Bind(string currentModel, string label, float score)
            {
                var displayLabel =
                    currentModel == TextClassifierHelper.WordVec ?
                    // Category name 1 is Positive and 0 is Negative.
                    label == "1" ? "Positive" : "Negative" :
                    CultureInfo.CurrentCulture.TextInfo.ToTitleCase(label);

                var result = ItemView.FindViewById<TextView>(Resource.Id.result);
                result.Text = ItemView.Context.Resources.GetString(
                    Resource.String.result_display_text,
                    displayLabel,
                    score);
            }
        }

        public void UpdateResult(List<Category> results, string model)
        {
            resultsList = results;
            currentModel = model;
            NotifyDataSetChanged();
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(
            ViewGroup parent,
            int viewType)
        {
            var view = LayoutInflater.From(parent.Context).Inflate(
                Resource.Layout.item_classification,
                parent,
                false);
            return new ViewHolder(view);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var viewHolder = holder as ViewHolder;
            var category = resultsList[position];
            viewHolder.Bind(currentModel, category.CategoryName(), category.Score());
        }

        public override int ItemCount => resultsList.Count;
    }
}
