// WARNING
//
// This file has been generated automatically by Visual Studio from the outlets and
// actions declared in your storyboard file.
// Manual changes to this file will not be maintained.
//
using System.CodeDom.Compiler;

namespace ImageClassification
{
    [Register("CameraViewController")]
    partial class CameraViewController
    {
        // MARK: Storyboard Outlets

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UIView previewView { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UILabel cameraUnavailableLabel { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UIButton resumeButton { get; set; }

        void ReleaseDesignerOutlets()
        {
            previewView?.Dispose();
            previewView = null;

            cameraUnavailableLabel?.Dispose();
            cameraUnavailableLabel = null;

            resumeButton?.Dispose();
            resumeButton = null;
        }
    }
}
