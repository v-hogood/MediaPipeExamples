using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.Content;
using AndroidX.Core.Widget;
using Google.Android.Material.BottomSheet;
using Google.Android.Material.Button;
using Google.Android.Material.ProgressIndicator;
using Google.Android.Material.Tabs;
using Google.Android.Material.TextField;
using Java.Lang;
using Java.Util.Concurrent;
using MediaPipe.Tasks.Decision;
using Color = Android.Graphics.Color;
using Exception = Java.Lang.Exception;
using ImageButton = Android.Widget.ImageButton;
using ProgressBar = Android.Widget.ProgressBar;
using Slider = Google.Android.Material.Slider.Slider;
using View = Android.Views.View;
using ViewStates = Android.Views.ViewStates;

namespace DecisionMaker;

[Activity(Name = "com.google.mediapipe.examples.decisionmaker.MainActivity", Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
public class MainActivity : AppCompatActivity
{
    private enum QuestionType { BOOLEAN, CHOICE, SCORE }

    // A choice option (key + description) or a rubric level (name + description).
    private record Item(string label, string description = "");

    // Editable fields of one question type; kept per tab so switching tabs keeps your edits.
    private record struct Draft(
        string input,
        string condition = "",
        // Boolean only: answer Yes when P(yes) >= threshold.
        float threshold = DefaultThreshold,
        List<Item> items = null,
        string instructions = ""
    );

    // What the result card shows: a headline, an optional subtitle and probability bars.
    private record Outcome(string headline, string subtitle, List<Bar> bars);
    private record Bar(string label, float probability, bool highlighted);

    private Dictionary<QuestionType, Draft> drafts = new()
    {
        {
            // Clear-cut sentiment works well with every bundled model; try lowering / raising the
            // threshold, or editing the review, to see the answer flip.
            QuestionType.BOOLEAN, new Draft(
                input: "This product is amazing, it works perfectly and I use it every day!",
                condition: "The review is positive.",
                items: new List<Item>()
            )
        },
        {
            QuestionType.CHOICE, new Draft(
                input: "My package never arrived even though tracking says delivered.",
                items: new List<Item>
                {
                    new("shipping", "Delivery problems, lost packages, tracking"),
                    new("billing", "Payment issues, duplicate charges, invoices"),
                    new("technical", "App crashes, login errors, bugs")
                },
                instructions: "Which department should handle this ticket?"
            )
        },
        {
            // Give each level a short, concrete description: bare labels such as "Negative" /
            // "Positive" give much less accurate scores.
            QuestionType.SCORE, new Draft(
                input: "The support agent was friendly and fixed my issue fast.",
                items: new List<Item>
                {
                    new("Very dissatisfied", "angry, problem not solved, terrible service"),
                    new("Dissatisfied", "slow or unhelpful support, problem partly solved"),
                    new("Neutral", "okay, average, nothing special"),
                    new("Satisfied", "helpful support, problem solved"),
                    new("Very satisfied", "excellent, fast, friendly support, delighted")
                },
                instructions: "Rate how satisfied the customer is with the support experience."
            )
        }
    };

    private DecisionMakerHelper helper;

    // Option / level rows currently shown in the editor, in order.
    private List<ItemQuestionRow> itemRows = new();

    private sealed class ItemQuestionRow
    {
        public View Root { get; }
        public TextView tvBadge { get; }
        public EditText etLabel { get; }
        public EditText etDescription { get; }
        public ImageButton btnRemove { get; }

        public ItemQuestionRow(LayoutInflater inflater, ViewGroup parent)
        {
            Root = inflater.Inflate(Resource.Layout.item_question_row, parent, false);
            tvBadge = Root.FindViewById<TextView>(Resource.Id.tvBadge);
            etLabel = Root.FindViewById<EditText>(Resource.Id.etLabel);
            etDescription = Root.FindViewById<EditText>(Resource.Id.etDescription);
            btnRemove = Root.FindViewById<ImageButton>(Resource.Id.btnRemove);
        }
    }

    // Model loading and inference run here, one at a time, off the main thread.
    private IExecutorService executor = Executors.NewSingleThreadExecutor();
    private QuestionType questionType = QuestionType.BOOLEAN;
    private bool isModelReady = false;

    // True while the result card shows a result (not a placeholder or status message).
    private bool hasResult = false;

    override protected void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        helper = new DecisionMakerHelper(this);
        ShowDraft(questionType);

        var tabQuestionType = FindViewById<TabLayout>(Resource.Id.tabQuestionType);
        var btnAddItem = FindViewById<MaterialButton>(Resource.Id.btnAddItem);
        var btnEvaluate = FindViewById<MaterialButton>(Resource.Id.btnEvaluate);

        tabQuestionType.TabSelected += (sender, e) =>
        {
            SaveDraft();
            questionType = (QuestionType)(e.Tab.Position);
            ShowDraft(questionType);
            ResetResult();
        };
        tabQuestionType.TabUnselected += (sender, e) => { };
        tabQuestionType.TabReselected += (sender, e) => { };

        btnAddItem.Click += (sender, e) => AddItemRow(new Item(""), requestFocus: true);
        btnEvaluate.Click += (sender, e) => Evaluate();
        InitThresholdSlider();

        InitBottomSheetControls();
        SetupDecisionMaker();
    }

    override protected void OnDestroy()
    {
        base.OnDestroy();
        executor.Execute(new Runnable(() => helper.ClearDecisionMaker()));
        executor.Shutdown();
    }

    private void InitThresholdSlider()
    {
        var sliderThreshold = FindViewById<Slider>(Resource.Id.sliderThreshold);
        var tvThreshold = FindViewById<TextView>(Resource.Id.tvThresholdValue);
        sliderThreshold.Change += (sender, e) =>
        {
            var draft = drafts[QuestionType.BOOLEAN];
            draft.threshold = e.P1;
            drafts[QuestionType.BOOLEAN] = draft;
            tvThreshold.Text = string.Format("{0:F2}", e.P1);
        };
        // Re-run the question when the slider is released, so the answer follows the threshold.
        sliderThreshold.StartTrackingTouch += (sender, e) => { };
        sliderThreshold.StopTrackingTouch += (sender, e) =>
        {
            if (hasResult) Evaluate();
        };
    }

    private void InitBottomSheetControls()
    {
        var sheet = FindViewById<NestedScrollView>(Resource.Id.bottom_sheet_layout);
        var behavior = BottomSheetBehavior.From(sheet);
        var header = FindViewById(Resource.Id.sheet_header);
        var toggleDelegate = FindViewById<MaterialButtonToggleGroup>(Resource.Id.toggle_delegate);
        var dropdownModel = FindViewById<MaterialAutoCompleteTextView>(Resource.Id.dropdown_model);

        header.Click += (sender, e) =>
        {
            behavior.State =
                behavior.State == BottomSheetBehavior.StateExpanded ?
                    BottomSheetBehavior.StateCollapsed :
                    BottomSheetBehavior.StateExpanded;
        };

        toggleDelegate.Check(DelegateButtonId(helper.CurrentDelegate));
        toggleDelegate.ButtonChecked += (sender, e) =>
        {
            if (!e.P2) return;
            var del =
                e.P1 == Resource.Id.btn_gpu ?
                    DecisionMakerHelper.DelegateGpu :
                    DecisionMakerHelper.DelegateCpu;
            if (del != helper.CurrentDelegate)
            {
                helper.CurrentDelegate = del;
                SetupDecisionMaker();
            }
        };

        var models = Resources.GetStringArray(Resource.Array.models_spinner_titles);
        dropdownModel.SetText(models[helper.CurrentModel], false);
        dropdownModel.ItemClick += (sender, e) =>
        {
            if (e.Position != helper.CurrentModel)
            {
                helper.CurrentModel = e.Position;
                SetupDecisionMaker();
            }
        };
    }

    private int DelegateButtonId(int del) =>
        del == DecisionMakerHelper.DelegateGpu ?
            Resource.Id.btn_gpu : Resource.Id.btn_cpu;

    private void SetupDecisionMaker()
    {
        isModelReady = false;
        // The last inference time belongs to the previous model / delegate.
        var inferenceTimeVal = FindViewById<TextView>(Resource.Id.inference_time_val);
        inferenceTimeVal.SetText(Resource.String.inference_time_placeholder);
        SetBusy(true, GetString(Resource.String.tv_loading_model));
        executor.Execute(new Runnable(() =>
        {
            Exception error = null;
            try
            {
                helper.SetupDecisionMaker();
            }
            catch (Exception e)
            {
                error = e;
            }
            RunOnUiThread(() =>
            {
                SetBusy(false);
                if (error == null)
                {
                    isModelReady = true;
                    ResetResult();
                    return;
                }
                Log.Error(Tag, "Failed to create DecisionMaker", error);
                if (helper.CurrentDelegate == DecisionMakerHelper.DelegateGpu &&
                    error is not DecisionMakerHelper.ModelNotFoundException)
                {
                    // GPU unavailable on this device: fall back to CPU, like the other samples.
                    // (A missing model file would fail on CPU too, so it's reported directly.)
                    Toast.MakeText(this, Resource.String.toast_gpu_fallback, ToastLength.Short).Show();
                    helper.CurrentDelegate = DecisionMakerHelper.DelegateCpu;
                    // currentDelegate is already CPU, so the toggle listener won't reload.
                    var toggleDelegate = FindViewById<MaterialButtonToggleGroup>(Resource.Id.toggle_delegate);
                    toggleDelegate.Check(Resource.Id.btn_cpu);
                    SetupDecisionMaker();
                }
                else
                {
                    ShowHeadline(error.Message ?? error.ToString(), placeholder: true);
                }
            });
        }));
    }

    private void Evaluate()
    {
        if (!isModelReady) return;
        SaveDraft();
        var draft = drafts[questionType];
        var type = questionType;
        if (string.IsNullOrWhiteSpace(draft.input))
        {
            Toast.MakeText(this, Resource.String.toast_need_input, ToastLength.Short).Show();
            return;
        }
        var items = draft.items
            .Select(it => new Item(it.label.Trim(), it.description.Trim()))
            .Where(it => !string.IsNullOrEmpty(it.label));
        if (type != QuestionType.BOOLEAN && items.Count() < 2)
        {
            Toast.MakeText(this, Resource.String.toast_need_items, ToastLength.Short).Show();
            return;
        }

        SetBusy(true);
        executor.Execute(new Runnable(() =>
        {
            (long, Outcome) outcome = new(0, null);
            Exception error = null;
            try
            {
                switch(type)
                {
                    case QuestionType.BOOLEAN:
                    {
                        var threshold = draft.threshold;
                        var result = helper.EvaluateBoolean(draft.input, draft.condition.Trim(), threshold);
                        outcome = new(result.inferenceTime, FormatBoolean(result.result, threshold));
                    }
                    break;

                    case QuestionType.CHOICE:
                    {
                        var options = new Dictionary<string, string>();
                        foreach (var item in items)
                        {
                            options[item.label] = item.description;
                        }
                        var result = helper.EvaluateChoice(draft.input, options, draft.instructions.Trim());
                        outcome = new(result.inferenceTime, FormatChoice(result.result, options));
                    }
                    break;

                    case QuestionType.SCORE:
                    {
                        // Each level is sent as "name: description".
                        var rubric = items.Select(it => string.IsNullOrEmpty(it.description) ? it.label : $"{it.label}: {it.description}").ToList();
                        var result = helper.EvaluateScore(draft.input, rubric, draft.instructions.Trim());
                        outcome = new(result.inferenceTime, FormatScore(result.result, items.Select(i => i.label).ToList()));
                    }
                    break;
                }
            }
            catch (Exception e)
            {
                error = e;
            }
            RunOnUiThread(() =>
            {
                SetBusy(false);
                if (error == null)
                {
                    ShowOutcome(outcome.Item2);
                    var inferenceTimeVal = FindViewById<TextView>(Resource.Id.inference_time_val);
                    inferenceTimeVal.Text =
                        string.Format("{0} ms", outcome.Item1);
                }
                else
                {
                    Log.Error(Tag, "Evaluation failed", error);
                    Toast.MakeText(this, error.Message ?? error.ToString(), ToastLength.Short).Show();
                    return;
                }
            });
        }));
    }

    // ---- Result formatting ----

    private Outcome FormatBoolean(BooleanResult r, float threshold) => new Outcome(
        headline: GetString(r.Value ? Resource.String.result_yes : Resource.String.result_no),
        subtitle: GetString(Resource.String.result_threshold, threshold),
        bars: new List<Bar>
        {
            new Bar(GetString(Resource.String.result_yes), r.ProbabilityTrue, r.Value),
            new Bar(GetString(Resource.String.result_no), 1f - r.ProbabilityTrue, !r.Value),
        }
    );

    private Outcome FormatChoice(ChoiceResult r, Dictionary<string, string> options) => new Outcome(
        headline: r.SelectedKey,
        subtitle: options.GetValueOrDefault(r.SelectedKey),
        bars: r.Probabilities.OrderByDescending(it => (float)it.Value)
            .Select(it => new Bar(it.Key, (float)it.Value, it.Key == r.SelectedKey)).ToList()
    );

    private Outcome FormatScore(ScoreResult r, List<string> levels)
    {
        var probabilities = r.LevelProbabilities;
        // Expected score on a 1..N scale.
        var expected = probabilities.Select((p, i) => (i + 1) * (float)p).Sum();
        var best = Enumerable.Range(0, probabilities.Count).MaxBy(i => (float)probabilities[i]);
        return new Outcome(
            headline: string.Format("{0:F2} / {1}", expected, probabilities.Count),
            subtitle: GetString(Resource.String.result_most_likely, levels[best]),
            bars: probabilities.Select((p, i) =>
                new Bar($"{i + 1} · {levels[i]}", (float)p, i == best)).ToList()
        );
    }

    private void ShowOutcome(Outcome outcome)
    {
        hasResult = true;
        ShowHeadline(outcome.headline, placeholder: false);
        var tvResultSubtitle = FindViewById<TextView>(Resource.Id.tvResultSubtitle);
        tvResultSubtitle.Text = outcome.subtitle;
        tvResultSubtitle.Visibility = outcome.subtitle == null ? ViewStates.Gone : ViewStates.Visible;

        var container = FindViewById<LinearLayout>(Resource.Id.resultBars);
        container.RemoveAllViews();
        var primary = ContextCompat.GetColor(this, Resource.Color.mp_color_primary);
        var muted = ContextCompat.GetColor(this, Resource.Color.result_bar_muted);
        foreach (var bar in outcome.bars)
        {
            var row = LayoutInflater.Inflate(Resource.Layout.item_result_bar, container, false);
            var p = System.Math.Clamp(bar.probability, 0f, 1f);
            var tvBarLabel = row.FindViewById<TextView>(Resource.Id.tvBarLabel);
            var tvBarValue = row.FindViewById<TextView>(Resource.Id.tvBarValue);
            var barProgress = row.FindViewById<LinearProgressIndicator>(Resource.Id.barProgress);
            tvBarLabel.Text = bar.label;
            tvBarLabel.SetTypeface(null, bar.highlighted ? TypefaceStyle.Bold : TypefaceStyle.Normal);
            tvBarValue.Text = string.Format("{0:F1}%", p * 100);
            barProgress.SetIndicatorColor(bar.highlighted ? primary : muted);
            barProgress.Progress = (int)((p * barProgress.Max) + 0.5f);
            container.AddView(row);
        }
        container.Visibility = outcome.bars.Count == 0 ? ViewStates.Gone : ViewStates.Visible;
    }

    // Large, dark headline for results; smaller grey text for placeholders and status.
    private void ShowHeadline(string text, bool placeholder)
    {
        var tvResult = FindViewById<TextView>(Resource.Id.tvResult);
        tvResult.Text = text;
        tvResult.SetTextSize(ComplexUnitType.Sp, placeholder ? 16f : 28f);
        tvResult.SetTypeface(null, placeholder ? TypefaceStyle.Normal : TypefaceStyle.Bold);
        tvResult.SetTextColor(new Color(ContextCompat.GetColor(this, placeholder ?
            Resource.Color.label_text_color : Resource.Color.mp_color_primary_dark)));
    }

    // ---- Question editor ----

    private void ShowDraft(QuestionType type)
    {
        var draft = drafts.GetValueOrDefault(type);
        var etInputText = FindViewById<TextInputEditText>(Resource.Id.etInputText);
        var etQuestion = FindViewById<TextInputEditText>(Resource.Id.etQuestion);
        var etInstructions = FindViewById<TextInputEditText>(Resource.Id.etInstructions);
        etInputText.Text = draft.input;
        etQuestion.Text = draft.condition;
        etInstructions.Text = draft.instructions;

        // Boolean questions only need a condition; the others need a list and instructions.
        var isBoolean = type == QuestionType.BOOLEAN;
        var conditionLayout = FindViewById<LinearLayout>(Resource.Id.conditionLayout);
        var thresholdSection = FindViewById<LinearLayout>(Resource.Id.thresholdSection);
        var sliderThreshold = FindViewById<Slider>(Resource.Id.sliderThreshold);
        var itemsSection = FindViewById<LinearLayout>(Resource.Id.itemsSection);
        var instructionsLayout = FindViewById<LinearLayout>(Resource.Id.instructionsLayout);
        conditionLayout.Visibility = isBoolean ? ViewStates.Visible : ViewStates.Gone;
        thresholdSection.Visibility = isBoolean ? ViewStates.Visible : ViewStates.Gone;
        sliderThreshold.Value = drafts.GetValueOrDefault(QuestionType.BOOLEAN).threshold;
        itemsSection.Visibility = isBoolean ? ViewStates.Gone : ViewStates.Visible;
        instructionsLayout.Visibility = isBoolean ? ViewStates.Gone : ViewStates.Visible;

        var isScore = type == QuestionType.SCORE;
        var tvItemsTitle = FindViewById<TextView>(Resource.Id.tvItemsTitle);
        var tvItemsHelper = FindViewById<TextView>(Resource.Id.tvItemsHelper);
        var btnAddItem = FindViewById<MaterialButton>(Resource.Id.btnAddItem);
        tvItemsTitle.Text = isScore ? GetString(Resource.String.title_rubric) : GetString(Resource.String.title_options);
        tvItemsHelper.Text = isScore ? GetString(Resource.String.helper_rubric) : GetString(Resource.String.helper_options);
        btnAddItem.Text = isScore ? GetString(Resource.String.btn_add_level) : GetString(Resource.String.btn_add_option);

        var itemsContainer = FindViewById<LinearLayout>(Resource.Id.itemsContainer);
        itemsContainer.RemoveAllViews();
        itemRows.Clear();
        foreach (var item in draft.items)
        {
            AddItemRow(item);
        }
    }

    private void AddItemRow(Item item, bool requestFocus = false)
    {
        var itemsContainer = FindViewById<LinearLayout>(Resource.Id.itemsContainer);
        var row = new ItemQuestionRow(LayoutInflater, itemsContainer);
        row.etLabel.SetHint(
            questionType == QuestionType.SCORE ? Resource.String.hint_level_label : Resource.String.hint_option_key
        );
        row.etLabel.Text = item.label;
        row.etDescription.Text = item.description;
        row.btnRemove.Click += (sender, e) => {
            itemsContainer.RemoveView(row.Root);
            itemRows.Remove(row);
            RenumberRows();
        };
        itemRows.Add(row);
        itemsContainer.AddView(row.Root);
        RenumberRows();
        if (requestFocus) row.etLabel.RequestFocus();
    }

    // Score levels are numbered 1..N (lowest first); choice options are lettered A, B, C…
    private void RenumberRows()
    {
        for (int i = 0; i < itemRows.Count; i++)
        {
            var row = itemRows[i];
            row.tvBadge.Text = questionType == QuestionType.SCORE ? $"{i + 1}" :
                (i < 26 ? $"{(char)('A' + i)}" : $"{i + 1}");
        }
    }
    private void SaveDraft()
    {
        var etInputText = FindViewById<TextInputEditText>(Resource.Id.etInputText);
        var etQuestion = FindViewById<TextInputEditText>(Resource.Id.etQuestion);
        var etInstructions = FindViewById<TextInputEditText>(Resource.Id.etInstructions);
        var draft = drafts[questionType] with {
            input = etInputText.Text,
            condition = etQuestion.Text,
            instructions = etInstructions.Text
        };
        if (questionType != QuestionType.BOOLEAN)
        {
            draft.items.Clear();
            draft.items.AddRange(itemRows.Select(row =>
                new Item(row.etLabel.Text, row.etDescription.Text)));
        }
        drafts[questionType] = draft;
    }

    // ---- UI state ----

    private void ResetResult()
    {
        hasResult = false;
        ShowHeadline(GetString(Resource.String.tv_result_placeholder), placeholder: true);
        var tvResultSubtitle = FindViewById<TextView>(Resource.Id.tvResultSubtitle);
        var resultBars = FindViewById<LinearLayout>(Resource.Id.resultBars);
        tvResultSubtitle.Visibility = ViewStates.Gone;
        resultBars.Visibility = ViewStates.Gone;
    }

    private void SetBusy(bool busy, string message = null)
    {
        var btnEvaluate = FindViewById<MaterialButton>(Resource.Id.btnEvaluate);
        var progressBar = FindViewById<ProgressBar>(Resource.Id.progressBar);
        btnEvaluate.Enabled = !busy;
        progressBar.Visibility = busy ? ViewStates.Visible : ViewStates.Invisible;
        if (message != null)
        {
            hasResult = false;
            ShowHeadline(message, placeholder: true);
            var tvResultSubtitle = FindViewById<TextView>(Resource.Id.tvResultSubtitle);
            var resultBars = FindViewById<LinearLayout>(Resource.Id.resultBars);
            tvResultSubtitle.Visibility = ViewStates.Gone;
            resultBars.Visibility = ViewStates.Gone;
        }
    }

    private const string Tag = "DecisionMaker";
    private const float DefaultThreshold = 0.5f;
}
