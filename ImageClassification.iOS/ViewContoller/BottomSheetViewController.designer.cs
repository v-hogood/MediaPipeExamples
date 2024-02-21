// WARNING
//
// This file has been generated automatically by Visual Studio from the outlets and
// actions declared in your storyboard file.
// Manual changes to this file will not be maintained.
//
using System.CodeDom.Compiler;
using ObjCRuntime;

namespace ImageClassification
{
    [Register("BottomSheetViewController")]
    partial class BottomSheetViewController
    {
        // MARK: Storyboard Outlets

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        public UILabel inferenceTimeLabel { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        public UILabel inferenceTimeNameLabel { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UIStepper thresholdStepper { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UILabel thresholdValueLabel { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UIStepper maxResultStepper { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UILabel maxResultLabel { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        public UIButton toggleBottomSheetButton { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UIButton choseModelButton { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UITableView tableView { get; set; }

        void ReleaseDesignerOutlets()
        {
            inferenceTimeLabel?.Dispose();
            inferenceTimeLabel = null;

            inferenceTimeNameLabel?.Dispose();
            inferenceTimeNameLabel = null;

            thresholdStepper?.Dispose();
            thresholdStepper = null;

            thresholdValueLabel?.Dispose();
            thresholdValueLabel = null;

            maxResultStepper?.Dispose();
            maxResultStepper = null;

            maxResultLabel?.Dispose();
            maxResultLabel = null;

            toggleBottomSheetButton?.Dispose();
            toggleBottomSheetButton = null;

            choseModelButton?.Dispose();
            choseModelButton = null;

            tableView?.Dispose();
            tableView = null;
        }
    }

    [Register("InfoCell")]
    public class InfoCell : UITableViewCell
    {
        public InfoCell(NativeHandle handle) : base(handle) { }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        public UILabel fieldNameLabel { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        public UILabel infoLabel { get; set; }

        void ReleaseDesignerOutlets()
        {
            fieldNameLabel?.Dispose();
            fieldNameLabel = null;

            infoLabel?.Dispose();
            infoLabel = null;
        }
    }
}
