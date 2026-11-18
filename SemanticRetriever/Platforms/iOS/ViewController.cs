using CoreAnimation;
using CoreFoundation;
using CoreGraphics;
using Foundation;
using MediaPipeTasksRetrieval;
using ObjCRuntime;
using UIKit;

namespace SemanticRetriever;

// Indexes ten bundled photos into an on-device vector store and searches them by meaning.
//
// Everything the engine does — loading the model, indexing, searching — runs on
// `retrieverQueue`. A serial queue guarantees that those steps can never overlap: the native
// engine is not reentrant, and overlapping work is what produces "EmbeddingEngine is not
// initialized".
public partial class ViewController: UIViewController
{
    // MARK: - Sample content

    private string[] sampleImages = [
        "red_apple", "yellow_banana", "cute_cat", "fast_car", "green_tree",
        "blue_sky", "coffee_mug", "open_book", "sunny_beach", "snowy_mountain",
    ];

    private string[] suggestions = [
        "a piece of fruit", "an animal", "something to drink", "a vacation spot", "a vehicle",
    ];

    // MARK: - Views

    private GradientView headerView = new();
    private UIView cardView = new();
    private UILabel acceleratorLabel = new();
    private UILabel gpuLabel = new();
    private UISwitch gpuSwitch = new();
    private UIButton indexButton = new(type: UIButtonType.System);
    private UIProgressView indexProgressView = new(style: UIProgressViewStyle.Default);
    private UIActivityIndicatorView statusSpinner = new(style: UIActivityIndicatorViewStyle.Medium);
    private UILabel statusLabel = new();
    private UIView searchBar = new();
    private UITextField queryField = new();
    private UIButton searchButton = new(type: UIButtonType.System);
    private UIScrollView suggestionScrollView = new();
    private UIStackView suggestionStack = new();
    private UILabel resultsLabel = new();
    private UITableView tableView = new();
    private UILabel emptyLabel = new();

    // MARK: - State

    private SemanticRetrieverHelper helper = new();
    private DispatchQueue retrieverQueue = new(
        label: "com.google.mediapipe.examples.semanticretriever.engine");

    private MPPRetrievalResult[] results = [];

    // True while the model is loading, indexing or searching.
    private bool isBusy = false;

    // True once the sample images have been embedded into the vector store.
    private bool isIndexed = false;

    private bool useGPU = false;

    // Assets never change at runtime, so a tiny in-memory cache is all we need.
    private Dictionary<string, UIImage> thumbnailCache = new();

    // MARK: - Lifecycle

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        OverrideUserInterfaceStyle = UIUserInterfaceStyle.Light;
        View.BackgroundColor = Palette.background;
        SetupUI();
        Reload();
    }

    ~ViewController()
    {
        // Releasing the native engine can block, so keep it off the main thread.
        retrieverQueue.DispatchAsync(() => helper.Close());
    }

    // MARK: - UI

    private void SetupUI()
    {
        headerView.Colors = [Palette.primary, Palette.primaryVariant];
        headerView.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(headerView);

        var titleLabel = new UILabel();
        titleLabel.Text = "Semantic Image Search";
        titleLabel.Font = UIFont.SystemFontOfSize(size: 22, weight: UIFontWeight.Bold);
        titleLabel.TextColor = UIColor.White;
        titleLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        headerView.AddSubview(titleLabel);

        cardView.BackgroundColor = Palette.surface;
        cardView.Layer.CornerRadius = 16;
        cardView.Layer.ShadowColor = UIColor.Black.CGColor;
        cardView.Layer.ShadowOpacity = 0.08f;
        cardView.Layer.ShadowRadius = 6;
        cardView.Layer.ShadowOffset = new CGSize(width: 0, height: 2);
        cardView.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(cardView);

        acceleratorLabel.Text = "Accelerator";
        acceleratorLabel.Font = UIFont.SystemFontOfSize(size: 13, weight: UIFontWeight.Semibold);
        acceleratorLabel.TextColor = Palette.onSurfaceMuted;

        gpuLabel.Text = "GPU";
        gpuLabel.Font = UIFont.SystemFontOfSize(size: 12);
        gpuLabel.TextColor = Palette.onSurfaceMuted;

        gpuSwitch.OnTintColor = Palette.primary;
        gpuSwitch.ValueChanged += (_, _) => GpuSwitched();

        var headerRow = new UIStackView(views: [acceleratorLabel, new UIView(), gpuLabel, gpuSwitch]);
        headerRow.Axis = UILayoutConstraintAxis.Horizontal;
        headerRow.Alignment = UIStackViewAlignment.Center;
        headerRow.Spacing = 8;

        indexButton.SetTitle("Index 10 sample images", forState: UIControlState.Normal);
        indexButton.TitleLabel?.Font = UIFont.SystemFontOfSize(size: 16, weight: UIFontWeight.Semibold);
        indexButton.BackgroundColor = Palette.primary;
        indexButton.SetTitleColor(UIColor.White, forState: UIControlState.Normal);
        indexButton.SetTitleColor(UIColor.White.ColorWithAlpha(0.6f), forState: UIControlState.Disabled);
        indexButton.Layer.CornerRadius = 12;
        indexButton.TouchUpInside += (_, _) => IndexTapped();
        indexButton.HeightAnchor.ConstraintEqualTo(constant: 46).Active = true;

        indexProgressView.ProgressTintColor = Palette.primary;
        indexProgressView.TrackTintColor = Palette.scoreTrack;
        indexProgressView.Hidden = true;

        // The spinner lives in a fixed-width box so the status text does not jump around when the
        // spinner stops and hides itself.
        statusSpinner.Color = Palette.primary;
        statusSpinner.HidesWhenStopped = true;
        statusSpinner.TranslatesAutoresizingMaskIntoConstraints = false;
        var spinnerBox = new UIView();
        spinnerBox.AddSubview(statusSpinner);
        spinnerBox.WidthAnchor.ConstraintEqualTo(constant: 16).Active = true;

        statusLabel.Text = "Ready";
        statusLabel.Font = UIFont.SystemFontOfSize(size: 13);
        statusLabel.TextColor = Palette.onSurfaceMuted;
        statusLabel.Lines = 0;

        var statusRow = new UIStackView(views: [spinnerBox, statusLabel]);
        statusRow.Axis = UILayoutConstraintAxis.Horizontal;
        statusRow.Alignment = UIStackViewAlignment.Center;
        statusRow.Spacing = 8;

        var cardStack = new UIStackView(views: [
            headerRow, indexButton, indexProgressView, statusRow,
        ]);
        cardStack.Axis = UILayoutConstraintAxis.Vertical;
        cardStack.Spacing = 12;
        cardStack.TranslatesAutoresizingMaskIntoConstraints = false;
        cardView.AddSubview(cardStack);

        searchBar.BackgroundColor = Palette.surface;
        searchBar.Layer.CornerRadius = 14;
        searchBar.Layer.BorderWidth = 1;
        searchBar.Layer.BorderColor = Palette.scoreTrack.CGColor;
        searchBar.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(searchBar);

        queryField.AttributedPlaceholder = new NSAttributedString(
            str: "Search images by meaning…",
            attributes: new UIStringAttributes(new NSDictionary(UIStringAttributeKey.ForegroundColor, Palette.onSurfaceMuted)));
        queryField.Font = UIFont.SystemFontOfSize(size: 16);
        queryField.TextColor = Palette.onSurface;
        queryField.TintColor = Palette.primary;
        queryField.BackgroundColor = UIColor.Clear;
        queryField.BorderStyle = UITextBorderStyle.None;
        queryField.ReturnKeyType = UIReturnKeyType.Search;
        queryField.ClearButtonMode = UITextFieldViewMode.WhileEditing;
        queryField.AutocorrectionType = UITextAutocorrectionType.No;
        queryField.Delegate = this;
        queryField.TranslatesAutoresizingMaskIntoConstraints = false;
        searchBar.AddSubview(queryField);

        searchButton.SetImage(UIImage.GetSystemImage(name: "magnifyingglass"), forState: UIControlState.Normal);
        searchButton.TintColor = Palette.primary;
        searchButton.TouchUpInside += (_, _) => SearchTapped();
        searchButton.TranslatesAutoresizingMaskIntoConstraints = false;
        searchBar.AddSubview(searchButton);

        suggestionScrollView.ShowsHorizontalScrollIndicator = false;
        suggestionScrollView.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(suggestionScrollView);

        suggestionStack.Axis = UILayoutConstraintAxis.Horizontal;
        suggestionStack.Spacing = 8;
        suggestionStack.TranslatesAutoresizingMaskIntoConstraints = false;
        suggestionScrollView.AddSubview(suggestionStack);

        for (int index = 0; index <  suggestions.Length; index++)
        {
            string suggestion = suggestions[index];
            var configuration = UIButtonConfiguration.PlainButtonConfiguration;
            configuration.AttributedTitle = new NSAttributedString(
                suggestion, attributes: new UIStringAttributes(new NSDictionary(UIStringAttributeKey.Font, UIFont.SystemFontOfSize(size: 13))));
            configuration.BaseForegroundColor = Palette.onSurface;
            configuration.ContentInsets = new NSDirectionalEdgeInsets(
                top: 6, leading: 14, bottom: 6, trailing: 14);
            configuration.Background.BackgroundColor = Palette.surface;
            configuration.Background.CornerRadius = 15;
            configuration.Background.StrokeColor = Palette.scoreTrack;
            configuration.Background.StrokeWidth = 1;

            var chip = new UIButton(type: UIButtonType.System);
            chip.Configuration = configuration;
            chip.Tag = index;
            chip.TouchUpInside += (sender, _) => SuggestionTapped(sender);
            suggestionStack.AddArrangedSubview(chip);
        }

        resultsLabel.Text = "Results";
        resultsLabel.Font = UIFont.SystemFontOfSize(size: 13, weight: UIFontWeight.Semibold);
        resultsLabel.TextColor = Palette.onSurfaceMuted;
        resultsLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(resultsLabel);

        tableView.DataSource = this;
        tableView.Delegate = this;
        tableView.RegisterClassForCellReuse(typeof(SearchResultCell), reuseIdentifier: SearchResultCell.Identifier);
        tableView.SeparatorStyle = UITableViewCellSeparatorStyle.None;
        tableView.BackgroundColor = UIColor.Clear;
        tableView.RowHeight = 96;
        tableView.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag;
        tableView.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(tableView);

        emptyLabel.Text = "Index the sample images to start searching.";
        emptyLabel.Font = UIFont.SystemFontOfSize(size: 14);
        emptyLabel.TextColor = Palette.onSurfaceMuted;
        emptyLabel.TextAlignment = UITextAlignment.Center;
        emptyLabel.Lines = 0;
        emptyLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(emptyLabel);

        NSLayoutConstraint.ActivateConstraints([
            headerView.TopAnchor.ConstraintEqualTo(anchor: View.TopAnchor),
            headerView.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor),
            headerView.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor),
            headerView.BottomAnchor.ConstraintEqualTo(
                anchor: View.SafeAreaLayoutGuide.TopAnchor, constant: 60),

            titleLabel.LeadingAnchor.ConstraintEqualTo(anchor: headerView.LeadingAnchor, constant: 20),
            titleLabel.TrailingAnchor.ConstraintEqualTo(anchor: headerView.TrailingAnchor, constant: -20),
            titleLabel.BottomAnchor.ConstraintEqualTo(anchor: headerView.BottomAnchor, constant: -22),

            cardView.TopAnchor.ConstraintEqualTo(anchor: headerView.BottomAnchor, constant: -12),
            cardView.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor, constant: 16),
            cardView.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor, constant: -16),

            cardStack.TopAnchor.ConstraintEqualTo(anchor: cardView.TopAnchor, constant: 16),
            cardStack.BottomAnchor.ConstraintEqualTo(anchor: cardView.BottomAnchor, constant: -16),
            cardStack.LeadingAnchor.ConstraintEqualTo(anchor: cardView.LeadingAnchor, constant: 16),
            cardStack.TrailingAnchor.ConstraintEqualTo(anchor: cardView.TrailingAnchor, constant: -16),

            statusSpinner.CenterXAnchor.ConstraintEqualTo(anchor: spinnerBox.CenterXAnchor),
            statusSpinner.CenterYAnchor.ConstraintEqualTo(anchor: spinnerBox.CenterYAnchor),

            searchBar.TopAnchor.ConstraintEqualTo(anchor: cardView.BottomAnchor, constant: 16),
            searchBar.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor, constant: 16),
            searchBar.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor, constant: -16),
            searchBar.HeightAnchor.ConstraintEqualTo(constant: 48),

            queryField.LeadingAnchor.ConstraintEqualTo(anchor: searchBar.LeadingAnchor, constant: 16),
            queryField.TrailingAnchor.ConstraintEqualTo(anchor: searchButton.LeadingAnchor, constant: -4),
            queryField.TopAnchor.ConstraintEqualTo(anchor: searchBar.TopAnchor),
            queryField.BottomAnchor.ConstraintEqualTo(anchor: searchBar.BottomAnchor),

            searchButton.TrailingAnchor.ConstraintEqualTo(anchor: searchBar.TrailingAnchor, constant: -8),
            searchButton.CenterYAnchor.ConstraintEqualTo(anchor: searchBar.CenterYAnchor),
            searchButton.WidthAnchor.ConstraintEqualTo(constant: 36),
            searchButton.HeightAnchor.ConstraintEqualTo(constant: 36),

            suggestionScrollView.TopAnchor.ConstraintEqualTo(anchor: searchBar.BottomAnchor, constant: 10),
            suggestionScrollView.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor, constant: 16),
            suggestionScrollView.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor, constant: -16),
            suggestionScrollView.HeightAnchor.ConstraintEqualTo(constant: 34),

            suggestionStack.TopAnchor.ConstraintEqualTo(anchor: suggestionScrollView.TopAnchor),
            suggestionStack.BottomAnchor.ConstraintEqualTo(anchor: suggestionScrollView.BottomAnchor),
            suggestionStack.LeadingAnchor.ConstraintEqualTo(anchor: suggestionScrollView.LeadingAnchor),
            suggestionStack.TrailingAnchor.ConstraintEqualTo(anchor: suggestionScrollView.TrailingAnchor),
            suggestionStack.HeightAnchor.ConstraintEqualTo(anchor: suggestionScrollView.HeightAnchor),

            resultsLabel.TopAnchor.ConstraintEqualTo(anchor: suggestionScrollView.BottomAnchor, constant: 16),
            resultsLabel.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor, constant: 20),
            resultsLabel.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor, constant: -20),

            tableView.TopAnchor.ConstraintEqualTo(anchor: resultsLabel.BottomAnchor, constant: 8),
            tableView.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor, constant: 12),
            tableView.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor, constant: -12),
            tableView.BottomAnchor.ConstraintEqualTo(anchor: View.SafeAreaLayoutGuide.BottomAnchor),

            emptyLabel.CenterXAnchor.ConstraintEqualTo(anchor: tableView.CenterXAnchor),
            emptyLabel.CenterYAnchor.ConstraintEqualTo(anchor: tableView.CenterYAnchor),
            emptyLabel.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor, constant: 32),
            emptyLabel.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor, constant: -32),
        ]);
    }

    // MARK: - Actions

    private void GpuSwitched()
    {
        useGPU = gpuSwitch.On;
        Reload();
    }

    private void IndexTapped()
    {
        EmbedSampleImages();
    }

    private void SearchTapped()
    {
        RunSearch();
    }

    private void SuggestionTapped(Object obj)
    {
        var sender = obj as UIButton;
        queryField.Text = suggestions[sender.Tag];
        RunSearch();
    }

    private void RunSearch()
    {
        if (isBusy)
        {
            Show(toast: "Busy — wait for the current step to finish");
            return;
        }
        queryField.ResignFirstResponder();
        var query = queryField.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(query))
        {
            Show(toast: "Enter a search query first");
            return;
        }
        SearchImages(query: query);
    }

    // MARK: - Engine work

    // Loads the model (first run only), opens the store and empties it. Switching accelerators is a
    // full reset: no index, no query, no results.
    private void Reload()
    {
        SetIndexed(false);
        var accelerator = useGPU ? "GPU" : "CPU";
        SetBusy(true, status: $"Loading model on {accelerator}…");
        queryField.Text = "";
        indexProgressView.Hidden = true;
        indexProgressView.Progress = 0;
        results = [];
        tableView.ReloadData();
        UpdateEmptyState();

        retrieverQueue.DispatchAsync(() =>
        {
            try
            {
                this.helper.PrepareEmbedder(useGPU: this.useGPU);
                this.helper.OpenStore();
                // Start from a known-empty store without deleting files under a live store.
                this.helper.Clear();
                DispatchQueue.MainQueue.DispatchAsync(() =>
                {
                    this.SetBusy(false, status: $"Ready on {accelerator} · store is empty");
                    this.SetIndexed(false);
                });
            }
            catch(Exception error)
            {
                Console.WriteLine($"Initialization failed {accelerator}: {error} — {error.Message}");
                DispatchQueue.MainQueue.DispatchAsync(() =>
                    this.SetBusy(false, status: $"Initialization failed: {error.Message}"));
            }
        });
    }

    private void EmbedSampleImages()
    {
        if (!helper.IsReady || isBusy)
        {
            Show(toast: "Still getting ready, please wait…");
            return;
        }
        SetBusy(true, status: "Indexing…");
        indexProgressView.Hidden = false;
        indexProgressView.Progress = 0;
        var startedAt = CAAnimation.CurrentMediaTime();

        retrieverQueue.DispatchAsync(() =>
        {
            try
            {
                for (int index = 0; index < this.sampleImages.Length; index++)
                {
                    var name = this.sampleImages[index];
                    var path = ImagePath(name: name);
                    if (path == null) continue;
                    DispatchQueue.MainQueue.DispatchAsync(() =>
                    {
                        this.statusLabel.Text =
                            $"Embedding {index + 1}/{this.sampleImages.Length}: "
                            + name.Replace(oldChar: '_', newChar: ' ');
                        this.indexProgressView.SetProgress(
                            (float)index / (float)this.sampleImages.Length, animated: true);
                    });
                    this.helper.EmbedImage(id: name, path: path);
                }
                var seconds = CAAnimation.CurrentMediaTime() - startedAt;
                DispatchQueue.MainQueue.DispatchAsync(() =>
                {
                    this.indexProgressView.SetProgress(1, animated: true);
                    // Let the bar finish animating, then tuck it away again.
                    DispatchQueue.MainQueue.DispatchAfter(when: new DispatchTime(DispatchTime.Now, 600000000), () =>
                        this.indexProgressView.Hidden = true);
                    this.SetBusy(
                        false,
                        status: string.Format(
                            "Indexed {0} images in {1:F1}s on {2} · ready to search",
                            this.sampleImages.Length, seconds, this.useGPU ? "GPU" : "CPU"));
                    this.SetIndexed(true);
                });
            }
            catch(Exception error)
            {
                Console.WriteLine($"Indexing failed: {error} — {error.Message}");
                DispatchQueue.MainQueue.DispatchAsync(() =>
                {
                    this.indexProgressView.Hidden = true;
                    this.SetBusy(false, status: $"Indexing failed: {error.Message}");
                });
            }
        });
    }

    private void SearchImages(string query)
    {
        if (!helper.IsReady)
        {
            Show(toast: "Still getting ready, please wait…");
            return;
        }
        SetBusy(true, status: $"Searching for {query}…");
        var startedAt = CAAnimation.CurrentMediaTime();

        retrieverQueue.DispatchAsync(() =>
        {
            try
            {
                var results = this.helper.SearchImages(query: query, limit: 5);
                var millis = (CAAnimation.CurrentMediaTime() - startedAt) * 1000;
                DispatchQueue.MainQueue.DispatchAsync(() =>
                {
                    this.results = results;
                    this.SetBusy(
                        false,
                        status: string.Format(
                            "{0} results for {1} in {2:F0}ms", results.Length, query, millis));
                    this.tableView.ReloadData();
                    this.UpdateEmptyState();
                });
            }
            catch(Exception error)
            {
                Console.WriteLine($"Search failed: {error} — {error.Message}");
                DispatchQueue.MainQueue.DispatchAsync(() =>
                    this.SetBusy(false, status: $"Search failed: {error.Message}"));
            }
        });
    }

    // MARK: - UI state

    // Shows/hides the small spinner next to the status line and disables input while busy.
    private void SetBusy(bool busy, string status)
    {
        isBusy = busy;
        if (busy)
            statusSpinner.StartAnimating();
        else
            statusSpinner.StopAnimating();
        indexButton.Enabled = !busy;
        indexButton.Alpha = busy ? 0.5f : 1;
        gpuSwitch.Enabled = !busy;
        statusLabel.Text = status;
        RefreshSearchControls();
    }

    // Indexing is a one-shot action per store: once the samples are in, the index button goes away
    // and the search UI takes over.
    private void SetIndexed(bool indexed)
    {
        isIndexed = indexed;
        indexButton.Hidden = indexed;
        RefreshSearchControls();
        UpdateEmptyState();
    }

    // Searching only makes sense once something has been indexed and nothing else is running.
    private void RefreshSearchControls()
    {
        var enabled = isIndexed && !isBusy;
        searchBar.Alpha = enabled ? 1 : 0.5f;
        queryField.Enabled = enabled;
        searchButton.Enabled = enabled;
        suggestionScrollView.Alpha = enabled ? 1 : 0.5f;
        foreach (var view in suggestionStack.ArrangedSubviews)
            view.UserInteractionEnabled = enabled;
    }

    private void UpdateEmptyState()
    {
        emptyLabel.Hidden = results.Length > 0;
        emptyLabel.Text =
            isIndexed
            ? "Search for something like “a piece of fruit”." : "Index the sample images to start searching.";
    }

    private void Show(string toast)
    {
        var alert = UIAlertController.Create(title: null, message: toast, preferredStyle: UIAlertControllerStyle.Alert);
        PresentViewController(alert, animated: true, null);
        DispatchQueue.MainQueue.DispatchAfter(when: new DispatchTime(DispatchTime.Now, 1200000000), () => alert.DismissViewController(animated: true, null));
    }

    // MARK: - Images

    static string ImagePath(string name)
    {
        return NSBundle.MainBundle.PathForResource(name: name, ofType: "jpg");
    }

    private UIImage Thumbnail(string name)
    {
        if (thumbnailCache.TryGetValue(name, out var cached))
            return cached;
        var path = ImagePath(name: name);
        if (path == null) return null;
        var image = new UIImage(filename: path);
        thumbnailCache[name] = image;
        return image;
    }
}

// MARK: - UITextFieldDelegate

public partial class ViewController : IUITextFieldDelegate
{
    public bool ShouldReturn(UITextField textField)
    {
        RunSearch();
        return true;
    }
}

// MARK: - UITableViewDataSource

public partial class ViewController : IUITableViewDataSource, IUITableViewDelegate
{
    public nint RowsInSection(UITableView tableView, nint section) =>
        results.Length;

    public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
    {
        var cell =
            tableView.DequeueReusableCell(reuseIdentifier: SearchResultCell.Identifier, indexPath: indexPath)
            as SearchResultCell;
        var result = results[indexPath.Row];
        cell.Configure(
            rank: indexPath.Row + 1,
            recordId: result.RecordId,
            score: result.Score,
            image: Thumbnail(name: result.RecordId));
        return cell;
    }
}

// A rank badge, the matching photo and the similarity the retriever reported for it.
public class SearchResultCell : UITableViewCell
{
    public const string Identifier = "SearchResultCell";

    private UIView container = new();
    private UIImageView thumbnailView = new();
    private UILabel rankLabel = new();
    private UILabel titleLabel = new();
    private UILabel scoreLabel = new();
    private UIProgressView scoreBar = new(style: UIProgressViewStyle.Default);

    public SearchResultCell(UITableViewCellStyle style, string reuseIdentifier) :
        base(style: style, reuseIdentifier: reuseIdentifier)
    {
        SetupLayout();
    }

    public SearchResultCell(NativeHandle handle) : base(handle)
    {
        SetupLayout();
    }

    private void SetupLayout()
    {
        SelectionStyle = UITableViewCellSelectionStyle.None;
        BackgroundColor = UIColor.Clear;
        ContentView.BackgroundColor = UIColor.Clear;

        container.BackgroundColor = Palette.surface;
        container.Layer.CornerRadius = 14;
        container.TranslatesAutoresizingMaskIntoConstraints = false;
        ContentView.AddSubview(container);

        thumbnailView.ContentMode = UIViewContentMode.ScaleAspectFill;
        thumbnailView.ClipsToBounds = true;
        thumbnailView.Layer.CornerRadius = 10;
        thumbnailView.BackgroundColor = Palette.surfaceMuted;
        thumbnailView.TranslatesAutoresizingMaskIntoConstraints = false;
        container.AddSubview(thumbnailView);

        rankLabel.Font = UIFont.SystemFontOfSize(size: 12, weight: UIFontWeight.Bold);
        rankLabel.TextColor = UIColor.White;
        rankLabel.TextAlignment = UITextAlignment.Center;
        rankLabel.BackgroundColor = Palette.primary;
        rankLabel.Layer.CornerRadius = 11;
        rankLabel.Layer.MasksToBounds = true;
        rankLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        container.AddSubview(rankLabel);

        titleLabel.Font = UIFont.SystemFontOfSize(size: 16, weight: UIFontWeight.Semibold);
        titleLabel.TextColor = Palette.onSurface;
        titleLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        container.AddSubview(titleLabel);

        scoreLabel.Font = UIFont.MonospacedDigitSystemFontOfSize(size: 13, weight: (float) UIFontWeight.Regular);
        scoreLabel.TextColor = Palette.onSurfaceMuted;
        scoreLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        container.AddSubview(scoreLabel);

        scoreBar.ProgressTintColor = Palette.primary;
        scoreBar.TrackTintColor = Palette.scoreTrack;
        scoreBar.TranslatesAutoresizingMaskIntoConstraints = false;
        container.AddSubview(scoreBar);

        NSLayoutConstraint.ActivateConstraints([
            container.TopAnchor.ConstraintEqualTo(anchor: ContentView.TopAnchor, constant: 4),
            container.BottomAnchor.ConstraintEqualTo(anchor: ContentView.BottomAnchor, constant: -4),
            container.LeadingAnchor.ConstraintEqualTo(anchor: ContentView.LeadingAnchor, constant: 4),
            container.TrailingAnchor.ConstraintEqualTo(anchor: ContentView.TrailingAnchor, constant: -4),

            thumbnailView.LeadingAnchor.ConstraintEqualTo(anchor: container.LeadingAnchor, constant: 10),
            thumbnailView.CenterYAnchor.ConstraintEqualTo(anchor: container.CenterYAnchor),
            thumbnailView.WidthAnchor.ConstraintEqualTo(constant: 68),
            thumbnailView.HeightAnchor.ConstraintEqualTo(constant: 68),

            rankLabel.TopAnchor.ConstraintEqualTo(anchor: thumbnailView.TopAnchor, constant: -6),
            rankLabel.LeadingAnchor.ConstraintEqualTo(anchor: thumbnailView.LeadingAnchor, constant: -6),
            rankLabel.WidthAnchor.ConstraintEqualTo(constant: 22),
            rankLabel.HeightAnchor.ConstraintEqualTo(constant: 22),

            titleLabel.TopAnchor.ConstraintEqualTo(anchor: thumbnailView.TopAnchor, constant: 4),
            titleLabel.LeadingAnchor.ConstraintEqualTo(anchor: thumbnailView.TrailingAnchor, constant: 14),
            titleLabel.TrailingAnchor.ConstraintEqualTo(anchor: container.TrailingAnchor, constant: -14),

            scoreLabel.TopAnchor.ConstraintEqualTo(anchor: titleLabel.BottomAnchor, constant: 6),
            scoreLabel.LeadingAnchor.ConstraintEqualTo(anchor: titleLabel.LeadingAnchor),
            scoreLabel.TrailingAnchor.ConstraintEqualTo(anchor: container.TrailingAnchor, constant: -14),

            scoreBar.TopAnchor.ConstraintEqualTo(anchor: scoreLabel.BottomAnchor, constant: 8),
            scoreBar.LeadingAnchor.ConstraintEqualTo(anchor: titleLabel.LeadingAnchor),
            scoreBar.TrailingAnchor.ConstraintEqualTo(anchor: container.TrailingAnchor, constant: -14),
        ]);
    }

    public void Configure(int rank, string recordId, double score, UIImage image)
    {
        rankLabel.Text = $"{rank}";
        titleLabel.Text = recordId.Replace(oldChar: '_', newChar: ' ').ToUpper();
        scoreLabel.Text = string.Format("Similarity {0:F4}", score);
        // Scores are cosine similarities in [-1, 1]; map to a 0-1 bar.
        scoreBar.Progress = (float)(Math.Max(0, Math.Min(1, (score + 1) / 2)));
        thumbnailView.Image = image;
    }
}

// Reproduces the horizontal gradient the Android sample uses in its header.
class GradientView: UIView
{
    public GradientView() : base() { }

    public UIColor[] Colors { get => field;
        set { field = value; gradientLayer.Colors = value.Select(it => it.CGColor).ToArray(); }
    }

    [Export("layerClass")]
    public static Class LayerClass() =>
        new Class(typeof(CAGradientLayer));

    private CAGradientLayer gradientLayer => Layer as CAGradientLayer;

    GradientView(CGRect frame) :
        base(frame: frame)
    {
        gradientLayer.StartPoint = new CGPoint(x: 0, y: 0.5);
        gradientLayer.EndPoint = new CGPoint(x: 1, y: 0.5);
    }
}

// The MediaPipe sample palette, matching `colors.xml` in the Android app.
public record Palette
{
    public static UIColor primary = new(red: (float) 0x00 / 255, green: (float) 0x7F / 255, blue: (float) 0x8B / 255, alpha: 1);
    public static UIColor primaryVariant = new(red: (float) 0x12 / 255, green: (float) 0xB5 / 255, blue: (float) 0xCB / 255, alpha: 1);
    public static UIColor background = new(red: (float) 0xF2 / 255, green: (float) 0xF4 / 255, blue: (float) 0xF5 / 255, alpha: 1);
    public static UIColor surface = UIColor.White;
    public static UIColor surfaceMuted = new(red: (float) 0xED / 255, green: (float) 0xF3 / 255, blue: (float) 0xF4 / 255, alpha: 1);
    public static UIColor onSurface = new(red: (float) 0x1B / 255, green: (float) 0x24 / 255, blue: (float) 0x26 / 255, alpha: 1);
    public static UIColor onSurfaceMuted = new(red: (float) 0x5F / 255, green: (float) 0x6E / 255, blue: (float) 0x71 / 255, alpha: 1);
    public static UIColor scoreTrack = new(red: (float) 0xDC / 255, green: (float) 0xE6 / 255, blue: (float) 0xE8 / 255, alpha: 1);
}
