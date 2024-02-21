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
    [Register("MediaLibraryViewController")]
    partial class MediaLibraryViewController
    {
        // MARK: Storyboard Outlets

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UIButton pickFromGalleryButton { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UIProgressView progressView { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UILabel imageEmptyLabel { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        UIImageView pickedImageView { get; set; }

        [Outlet]
        [GeneratedCode("iOS Designer", "1.0")]
        NSLayoutConstraint pickFromGalleryButtonBottomSpace { get; set; }

        void ReleaseDesignerOutlets()
        {
            pickFromGalleryButton?.Dispose();
            pickFromGalleryButton = null;

            progressView?.Dispose();
            progressView = null;

            imageEmptyLabel?.Dispose();
            imageEmptyLabel = null;

            pickedImageView?.Dispose();
            pickedImageView = null;

            pickFromGalleryButtonBottomSpace?.Dispose();
            pickFromGalleryButtonBottomSpace = null;
        }
    }
}
