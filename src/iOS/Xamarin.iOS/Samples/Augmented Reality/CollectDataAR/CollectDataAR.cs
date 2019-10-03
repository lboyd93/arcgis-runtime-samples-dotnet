using CoreGraphics;
using CoreImage;
using Esri.ArcGISRuntime.ARToolkit;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Foundation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UIKit;

namespace ArcGISRuntimeXamarin.Samples.CollectDataAR
{
    [Register("CollectDataAR")]
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        "Collect data in AR",
        "Augmented reality",
        "Tap on real-world objects to collect data.",
        "")]
    [ArcGISRuntime.Samples.Shared.Attributes.OfflineData()]
    public class CollectDataAR : UIViewController
    {
        private ARSceneView _arView;
        private UILabel _helpLabel;
        private UIBarButtonItem _calibrateButton;
        private UIBarButtonItem _addButton;
        private CalibrationViewController _calibrationVC;
        private UISegmentedControl _realScalePicker;

        private bool _changingScale;

        private FeatureLayer _featureLayer;
        private ServiceFeatureTable _featureTable = new ServiceFeatureTable(new Uri("https://services2.arcgis.com/ZQgQTuoyBrtmoGdP/arcgis/rest/services/AR_Tree_Survey/FeatureServer/0"));

        private ArcGISTiledElevationSource _elevationSource;
        private Surface _elevationSurface;

        private GraphicsOverlay _graphicsOverlay;
        private SimpleMarkerSceneSymbol _tappedPointSymbol = new SimpleMarkerSceneSymbol(SimpleMarkerSceneSymbolStyle.Diamond, System.Drawing.Color.Orange, 0.5, 0.5, 0.5, SceneSymbolAnchorPosition.Center);

        // Location data source for AR and route tracking.
        private AdjustableLocationDataSource _locationSource = new AdjustableLocationDataSource();

        private bool _isCalibrating = false;

        private bool IsCalibrating
        {
            get => _isCalibrating;
            set
            {
                _isCalibrating = value;
                if (_isCalibrating)
                {
                    _arView.Scene.BaseSurface.Opacity = 0.5;
                    ShowCalibrationPopover();
                }
                else
                {
                    _arView.Scene.BaseSurface.Opacity = 0;
                    _calibrationVC.DismissViewController(true, null);
                }
            }
        }

        public override void LoadView()
        {
            View = new UIView { BackgroundColor = UIColor.White };

            UIToolbar toolbar = new UIToolbar();
            toolbar.TranslatesAutoresizingMaskIntoConstraints = false;

            _arView = new ARSceneView();
            _arView.TranslatesAutoresizingMaskIntoConstraints = false;

            _helpLabel = new UILabel();
            _helpLabel.TranslatesAutoresizingMaskIntoConstraints = false;
            _helpLabel.TextAlignment = UITextAlignment.Center;
            _helpLabel.TextColor = UIColor.White;
            _helpLabel.BackgroundColor = UIColor.FromWhiteAlpha(0, 0.6f);
            _helpLabel.Text = "Adjust calibration before starting";

            _calibrationVC = new CalibrationViewController(_arView, _locationSource);

            _calibrateButton = new UIBarButtonItem("Calibrate", UIBarButtonItemStyle.Plain, ToggleCalibration);
            _addButton = new UIBarButtonItem(UIBarButtonSystemItem.Add, AddButtonPressed) { Enabled = false };

            _realScalePicker = new UISegmentedControl("Roaming", "Local");
            _realScalePicker.SelectedSegment = 0;
            _realScalePicker.ValueChanged += RealScaleValueChanged;

            toolbar.Items = new[]
            {
                _calibrateButton,
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                new UIBarButtonItem(){CustomView = _realScalePicker},
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                _addButton
            };

            View.AddSubviews(_arView, toolbar, _helpLabel);

            NSLayoutConstraint.ActivateConstraints(new[]
            {
                _arView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                _arView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                _arView.TopAnchor.ConstraintEqualTo(View.TopAnchor),
                _arView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
                toolbar.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                toolbar.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                toolbar.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor),
                _helpLabel.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
                _helpLabel.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                _helpLabel.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                _helpLabel.HeightAnchor.ConstraintEqualTo(40)
            });
        }

        private async void RealScaleValueChanged(object sender, EventArgs e)
        {
            if (_changingScale)
            {
                return;
            }
            _changingScale = true;
            ((UISegmentedControl)sender).Enabled = false;
            if (((UISegmentedControl)sender).SelectedSegment == 0)
            {
                await _arView.StopTrackingAsync();
                await _arView.StartTrackingAsync(ARLocationTrackingMode.Continuous);
            }
            else
            {
                await _arView.StopTrackingAsync();
                await _arView.StartTrackingAsync(ARLocationTrackingMode.Initial);
            }
            ((UISegmentedControl)sender).Enabled = true;
            _changingScale = false;
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            Initialize();
        }

        private void ToggleCalibration(object sender, EventArgs e) => IsCalibrating = !IsCalibrating;

        private void Initialize()
        {
            // Create and add the scene.
            _arView.Scene = new Scene(Basemap.CreateImagery());

            // Add the location data source to the AR view.
            _arView.LocationDataSource = _locationSource;

            // Create and add the elevation source.
            _elevationSource = new ArcGISTiledElevationSource(new Uri("https://elevation3d.arcgis.com/arcgis/rest/services/WorldElevation3D/Terrain3D/ImageServer"));
            _elevationSurface = new Surface();
            _elevationSurface.ElevationSources.Add(_elevationSource);
            _arView.Scene.BaseSurface = _elevationSurface;

            // Configure the surface for AR: no navigation constraint and hidden by default.
            _elevationSurface.NavigationConstraint = NavigationConstraint.None;
            _elevationSurface.Opacity = 0;

            // Add a graphics overlay for displaying points in AR.
            _graphicsOverlay = new GraphicsOverlay();
            _graphicsOverlay.SceneProperties.SurfacePlacement = SurfacePlacement.Absolute;
            _graphicsOverlay.Renderer = new SimpleRenderer(_tappedPointSymbol);
            _arView.GraphicsOverlays.Add(_graphicsOverlay);

            

            _arView.GeoViewTapped += arViewTapped;
        }

        private void arViewTapped(object sender, GeoViewInputEventArgs e)
        {
            // Try to get the real-world position of that tapped AR plane.
            var planeLocation = _arView.ARScreenToLocation(e.Position);

            // Remove any existing graphics.
            _graphicsOverlay.Graphics.Clear();

            if (planeLocation != null)
            {
                _graphicsOverlay.Graphics.Add(new Graphic(planeLocation));
                _addButton.Enabled = true;
                _helpLabel.Text = "Placed relative to ARKit plane";
            }
            else
            {
                new UIAlertView("Error", "Din't find anything, try again.", (IUIAlertViewDelegate)null, "OK", null).Show();
                _addButton.Enabled = false;
            }
        }

        private void ShowCalibrationPopover()
        {
            // Show the table view in a popover.
            _calibrationVC.ModalPresentationStyle = UIModalPresentationStyle.Popover;
            _calibrationVC.PreferredContentSize = new CGSize(360, 120);
            UIPopoverPresentationController pc = _calibrationVC.PopoverPresentationController;
            if (pc != null)
            {
                pc.BarButtonItem = _calibrateButton;
                pc.PermittedArrowDirections = UIPopoverArrowDirection.Down;
                ppDelegate popoverDelegate = new ppDelegate();

                // Stop calibration when the popover closes.
                popoverDelegate.UserDidDismissPopover += (o, e) => IsCalibrating = false;
                pc.Delegate = popoverDelegate;
            }

            PresentViewController(_calibrationVC, true, null);
        }

        private async void AddButtonPressed(object sender, EventArgs e)
        {
            // Check if the user has already tapped a point.
            if (!_graphicsOverlay.Graphics.Any())
            {
                new UIAlertView("Error", "Didn't find anything, try again.", (IUIAlertViewDelegate)null, "OK", null).Show();
                return;
            }
            try
            {
                int healthValue = await getTreeHealthValue();

                CoreVideo.CVPixelBuffer coreVideoBuffer = _arView.ARSCNView.Session.CurrentFrame?.CapturedImage;
                if (coreVideoBuffer != null)
                {
                    var coreImage = new CIImage(coreVideoBuffer);
                    coreImage = coreImage.CreateByApplyingOrientation(ImageIO.CGImagePropertyOrientation.Right);
                    var imageRef = new CIContext().CreateCGImage(coreImage, new CGRect(0, 0, coreVideoBuffer.Height, coreVideoBuffer.Width));
                    var rotatedImage = new UIImage(imageRef);
                    CreateFeature(rotatedImage, healthValue);
                }
                else
                {
                    new UIAlertView("Error", "Didn't get image for tap.", (IUIAlertViewDelegate)null, "OK", null).Show();
                }
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }

        private async Task<int> getTreeHealthValue()
        {
            // Create a new copmletion source for the prompt.
            TaskCompletionSource<int> _healthCompletionSource = new TaskCompletionSource<int>();

            // Create prompt for health of the tree.
            UIAlertController prompt = UIAlertController.Create("Take picture and add tree", "How healthy is this tree?", UIAlertControllerStyle.ActionSheet);
            prompt.AddAction(UIAlertAction.Create("Dead", UIAlertActionStyle.Default, (o) => _healthCompletionSource.TrySetResult(0)));
            prompt.AddAction(UIAlertAction.Create("Distressed", UIAlertActionStyle.Default, (o) => _healthCompletionSource.TrySetResult(5)));
            prompt.AddAction(UIAlertAction.Create("Healthy", UIAlertActionStyle.Default, (o) => _healthCompletionSource.TrySetResult(10)));
            prompt.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, (o) => _healthCompletionSource.TrySetCanceled()));

            // Needed to prevent crash on iPad.
            UIPopoverPresentationController ppc = prompt.PopoverPresentationController;
            if (ppc != null)
            {
                ppc.DidDismiss += (s, ee) => _healthCompletionSource.TrySetCanceled();
            }

            // Present the prompt to the user.
            PresentViewController(prompt, true, null);

            return await _healthCompletionSource.Task;
        }

        private async Task CreateFeature(UIImage capturedImage, int healthValue)
        {
            _helpLabel.Text = "Adding feature...";

            try
            {
                // Get the geometry of the feature.
                MapPoint featurePoint = _graphicsOverlay.Graphics.First().Geometry as MapPoint;

                // Create attributes for the feature using the user selected health value.
                IEnumerable<KeyValuePair<string, object>> featureAttributes = new Dictionary<string, object>() { { "Health", (short)healthValue }, { "Height", 3.2 }, { "Diameter", 1.2 } };

                // Ensure that the feature table is loaded.
                if(_featureTable.LoadStatus != Esri.ArcGISRuntime.LoadStatus.Loaded )
                {
                    await _featureTable.LoadAsync();
                }

                // Create the new feature
                ArcGISFeature newFeature = _featureTable.CreateFeature(featureAttributes, featurePoint) as ArcGISFeature;

                // Convert the image into a byte array.
                Stream imageStream = capturedImage.AsJPEG().AsStream();
                byte[] attachmentData = new byte[imageStream.Length];
                imageStream.Read(attachmentData, 0, attachmentData.Length);

                // Add the attachment.
                // The contentType string is the MIME type for JPEG files, image/jpeg.
                await newFeature.AddAttachmentAsync("tree.jpg", "image/jpeg", attachmentData);

                // Add the newly created feature to the feature table.
                await _featureTable.AddFeatureAsync(newFeature);

                // Apply the edits to the service feature table.
                await _featureTable.ApplyEditsAsync();

                // Reset the user interface.
                _helpLabel.Text = "Tap to create a feature";
                _graphicsOverlay.Graphics.Clear();
                _addButton.Enabled = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                new UIAlertView("Error", "Could not create feature", (IUIAlertViewDelegate)null, "OK", null).Show();
            }
        }

        // Force popover to display on iPhone.
        private class ppDelegate : UIPopoverPresentationControllerDelegate
        {
            // Public event enables detection of popover close. When the popover closes, calibration should stop.
            public EventHandler UserDidDismissPopover;

            public override UIModalPresentationStyle GetAdaptivePresentationStyle(
                UIPresentationController forPresentationController) => UIModalPresentationStyle.None;

            public override UIModalPresentationStyle GetAdaptivePresentationStyle(UIPresentationController controller,
                UITraitCollection traitCollection) => UIModalPresentationStyle.None;

            public override void DidDismissPopover(UIPopoverPresentationController popoverPresentationController)
            {
                UserDidDismissPopover?.Invoke(this, EventArgs.Empty);
            }
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            _arView.StartTrackingAsync(ARLocationTrackingMode.Initial);
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);
            _arView.StopTrackingAsync();
        }
    }

    public class CalibrationViewController : UIViewController
    {
        private UISlider _headingSlider;
        private UISlider _elevationSlider;
        private UILabel elevationLabel;
        private UILabel headingLabel;
        private ARSceneView _arView;
        private AdjustableLocationDataSource _locationSource;
        private NSTimer _headingTimer;
        private NSTimer _elevationTimer;

        public CalibrationViewController(ARSceneView arView, AdjustableLocationDataSource locationSource)
        {
            _arView = arView;
            _locationSource = locationSource;
        }

        public override void LoadView()
        {
            // Create and add the container views.
            View = new UIView();

            UIStackView formContainer = new UIStackView();
            formContainer.TranslatesAutoresizingMaskIntoConstraints = false;
            formContainer.Spacing = 8;
            formContainer.LayoutMarginsRelativeArrangement = true;
            formContainer.Alignment = UIStackViewAlignment.Fill;
            formContainer.LayoutMargins = new UIEdgeInsets(8, 8, 8, 8);
            formContainer.Axis = UILayoutConstraintAxis.Vertical;
            formContainer.WidthAnchor.ConstraintEqualTo(300).Active = true;

            elevationLabel = new UILabel();
            elevationLabel.TranslatesAutoresizingMaskIntoConstraints = false;
            elevationLabel.Text = "Elevation";
            _elevationSlider = new UISlider { MinValue = -10, MaxValue = 10, Value = 0 };
            _elevationSlider.TranslatesAutoresizingMaskIntoConstraints = false;
            formContainer.AddArrangedSubview(getRowStackView(new UIView[] { _elevationSlider, elevationLabel }));

            headingLabel = new UILabel();
            headingLabel.TranslatesAutoresizingMaskIntoConstraints = false;
            headingLabel.Text = "Heading";
            _headingSlider = new UISlider { MinValue = -10, MaxValue = 10, Value = 0 };
            _headingSlider.TranslatesAutoresizingMaskIntoConstraints = false;
            formContainer.AddArrangedSubview(getRowStackView(new UIView[] { _headingSlider, headingLabel }));

            // Lay out container and scroll view.
            View.AddSubview(formContainer);
        }

        private UIStackView getRowStackView(UIView[] views)
        {
            UIStackView row = new UIStackView(views);
            row.TranslatesAutoresizingMaskIntoConstraints = false;
            row.Spacing = 8;
            row.Axis = UILayoutConstraintAxis.Horizontal;
            row.Distribution = UIStackViewDistribution.FillEqually;
            return row;
        }

        private void HeadingSlider_ValueChanged(object sender, EventArgs e)
        {
            if (_headingTimer == null)
            {
                // Use a timer to continuously update elevation while the user is interacting (joystick effect).
                _headingTimer = new NSTimer(NSDate.Now, 0.1, true, (timer) =>
                {
                    // Get the old camera.
                    Camera oldCamera = _arView.OriginCamera;

                    // Calculate the new heading by applying an offset to the old camera's heading.
                    var newHeading = oldCamera.Heading + this.JoystickConverter(_headingSlider.Value);

                    // Set the origin camera by rotating the existing camera to the new heading.
                    _arView.OriginCamera = oldCamera.RotateTo(newHeading, oldCamera.Pitch, oldCamera.Roll);


                    // Update the heading label.
                    headingLabel.Text = $"Heading: {(int)_arView.OriginCamera.Heading}";
                });
                NSRunLoop.Main.AddTimer(_headingTimer, NSRunLoopMode.Default);
            }
        }

        private void ElevationSlider_ValueChanged(object sender, EventArgs e)
        {
            if (_elevationTimer == null)
            {
                // Use a timer to continuously update elevation while the user is interacting (joystick effect).
                _elevationTimer = new NSTimer(NSDate.Now, 0.1, true, (timer) =>
                {
                    // Calculate the altitude offset
                    var newValue = _locationSource.AltitudeOffset += JoystickConverter(_elevationSlider.Value * 3.0);

                    // Set the altitude offset on the location data source.
                    _locationSource.AltitudeOffset = newValue;

                    // Update the label
                    elevationLabel.Text = $"Elevation: {(int)_locationSource.AltitudeOffset}m";
                });
                NSRunLoop.Main.AddTimer(_elevationTimer, NSRunLoopMode.Default);
            }
        }

        private double JoystickConverter(double value)
        {
            return Math.Pow(value, 2) / 25 * (value < 0 ? -1.0 : 1.0);
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);

            // Subscribe to events.
            _headingSlider.ValueChanged += HeadingSlider_ValueChanged;
            _headingSlider.TouchUpInside += TouchUpHeading;
            _headingSlider.TouchUpOutside += TouchUpHeading;

            _elevationSlider.ValueChanged += ElevationSlider_ValueChanged;
            _elevationSlider.TouchUpInside += TouchUpElevation;
            _elevationSlider.TouchUpOutside += TouchUpElevation;
        }

        private void TouchUpHeading(object sender, EventArgs e)
        {
            _headingTimer.Invalidate();
            _headingTimer = null;
            _headingSlider.Value = 0;
        }

        private void TouchUpElevation(object sender, EventArgs e)
        {
            _elevationTimer.Invalidate();
            _elevationTimer = null;
            _elevationSlider.Value = 0;
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);

            // Unsubscribe from events, per best practice.
            _headingSlider.ValueChanged -= HeadingSlider_ValueChanged;
            _headingSlider.TouchUpInside -= TouchUpHeading;
            _headingSlider.TouchUpOutside -= TouchUpHeading;

            _elevationSlider.ValueChanged -= ElevationSlider_ValueChanged;
            _elevationSlider.TouchUpInside -= TouchUpElevation;
            _elevationSlider.TouchUpOutside -= TouchUpElevation;
        }
    }
}