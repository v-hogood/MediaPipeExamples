// WARNING
//
// This file has been generated automatically by Visual Studio from the outlets and
// actions declared in your storyboard file.
// Manual changes to this file will not be maintained.
//
using System.CodeDom.Compiler;

namespace ImageClassification
{
    [Register ("RootViewController")]
    partial class RootViewController
    {
        // MARK: Storyboards Outlets

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        public UIView tabBarContainerView { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UITabBar runningModeTabbar { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        NSLayoutConstraint bottomSheetViewBottomSpace { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        NSLayoutConstraint bottomViewHeightConstraint { get; set; }

        void ReleaseDesignerOutlets()
        {
            tabBarContainerView?.Dispose();
            tabBarContainerView = null;

            runningModeTabbar?.Dispose();
            runningModeTabbar = null;

            bottomSheetViewBottomSpace?.Dispose();
            bottomSheetViewBottomSpace = null;

            bottomViewHeightConstraint?.Dispose();
            bottomViewHeightConstraint = null;
        }
    }
}
