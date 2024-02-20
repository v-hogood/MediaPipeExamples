// WARNING
//
// This file has been generated automatically by Visual Studio from the outlets and
// actions declared in your storyboard file.
// Manual changes to this file will not be maintained.
//
using System.CodeDom.Compiler;

namespace TextClassification
{
    [Register ("ViewController")]
    partial class ViewController
    {
        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UITableView tableView { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UIButton chooseModelButton { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UIButton classifyButton { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UIButton clearButton { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UITextView inputTextView { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIView titleView { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UILabel inferenceTimeLabel { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        NSLayoutConstraint settingViewHeightLayoutConstraint { get; set; }

        void ReleaseDesignerOutlets()
        {
            tableView?.Dispose();
            tableView = null;

            chooseModelButton?.Dispose();
            chooseModelButton = null;

            classifyButton?.Dispose();
            classifyButton = null;

            clearButton?.Dispose();
            clearButton = null;

            inputTextView?.Dispose();
            inputTextView = null;

            titleView?.Dispose();
            titleView = null;

            inferenceTimeLabel?.Dispose();
            inferenceTimeLabel = null;

            settingViewHeightLayoutConstraint?.Dispose();
            settingViewHeightLayoutConstraint = null;
        }
    }
}