using MediaPipeTasksText;
using static TextClassification.TextClassifierHelper;

namespace TextClassification;

public class TextClassifierHelper
{
    private MPPTextClassifier textClassifier;

    public TextClassifierHelper(Model model)
    {
        var modelPath = model.ModelPath();
        if (modelPath == null) return;
        NSError error;
        try
        {
            textClassifier = new MPPTextClassifier(modelPath: modelPath, out error);
        }
        catch(Exception e)
        {
            Console.WriteLine("TextClassifier init error: " + e.Message);
        }
    }

    public MPPTextClassifierResult Classify(string text)
    {
        if (textClassifier == null || text == null) return null;
        NSError error;
        var result = textClassifier.ClassifyText(text: text, out error);
        if (error != null)
        {
            Console.WriteLine("TextClassifier classify error: " + error);
        }
        return result;
    }

    public enum Model
    {
        MobileBert,
        AvgWordClassifier
    }
}

public static class Extensions
{
    public static string ToText(this Model model) =>
        model switch
        {
            Model.MobileBert => "Mobile Bert",
            Model.AvgWordClassifier => "Avg Word Classifier",
            _ => ""
        };

    public static Model ToModel(this string text) =>
        text switch
        {
            "Mobile Bert" => Model.MobileBert,
            "Avg Word Classifier" => Model.AvgWordClassifier,
            _ => Model.MobileBert
        };

    public static string ModelPath(this Model model) =>
        model switch
        {
            Model.MobileBert => NSBundle.MainBundle.PathForResource(
                "bert_classifier", ofType: "tflite"),
            Model.AvgWordClassifier => NSBundle.MainBundle.PathForResource(
                "average_word_classifier", ofType: "tflite"),
            _ => ""
        };
}
