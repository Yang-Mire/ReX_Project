//  ---------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// 
//  The MIT License (MIT)
// 
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
//  ---------------------------------------------------------------------------------

using Microsoft.Graphics.Canvas;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.UI.Xaml.Shapes;
using Microsoft.Toolkit.Uwp.UI.Extensions;
using Windows.ApplicationModel;
using Windows.Foundation;

namespace Rex
{
    public sealed partial class DetailPage : Page
    {
        ImageCropper _imageCropper = new ImageCropper();
        ImageFileInfo item;
        CultureInfo culture = CultureInfo.CurrentCulture;
        bool canNavigateWithUnsavedChanges = false;
        BitmapImage ImageSource = null;
        bool CropOrSave = true;




        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            item = e.Parameter as ImageFileInfo;
            canNavigateWithUnsavedChanges = false;
            ImageSource = await item.GetImageSourceAsync();


            if (item != null)
            {
                ConnectedAnimation imageAnimation = ConnectedAnimationService.GetForCurrentView().GetAnimation("itemAnimation");
                if (imageAnimation != null)
                {

                    targetImage.Source = ImageSource;


                    imageAnimation.Completed += async (s, e_) =>
                    {
                        await LoadBrushAsync();
                        // This delay prevents a flicker that can happen when
                        // a large image is being loaded to the brush. It gives
                        // the image time to finish loading. (200 is ok, 400 to be safe.)

                        await Task.Delay(200);
                        targetImage.Source = null;
                    };
                    imageAnimation.TryStart(targetImage);

                }

                if (ImageSource.PixelHeight == 0 && ImageSource.PixelWidth == 0)
                {
                    // There is no editable image loaded. Disable zoom and edit
                    // to prevent other errors.
                    EditButton.IsEnabled = false;
                    ZoomButton.IsEnabled = false;
                };
            }
            else
            {
                // error
            }

            if (this.Frame.CanGoBack)
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            }
            else
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            }

            base.OnNavigatedTo(e);

        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // If the photo has unsaved changes, we want to show a dialog
            // that warns the user before the navigation happens
            // To give the user a chance to view the dialog and respond,
            // we go ahead and cancel the navigation.
            // If the user wants to leave the page, we restart the
            // navigation. We use the canNavigateWithUnsavedChanges field to
            // track whether the user has been asked.
            if (item.NeedsSaved && !canNavigateWithUnsavedChanges)
            {
                // The item has unsaved changes and we haven't shown the
                // dialog yet. Cancel navigation and show the dialog.
                e.Cancel = true;
                ShowSaveDialog(e);
            }
            else
            {
                if (e.NavigationMode == NavigationMode.Back)
                {
                    ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("backAnimation", imgRect);
                }
                canNavigateWithUnsavedChanges = false;
                base.OnNavigatingFrom(e);
            }
        }

        /// <summary>
        /// Gives users a chance to save the image before navigating
        /// to a different page.
        /// </summary>
        private async void ShowSaveDialog(NavigatingCancelEventArgs e)
        {
            ContentDialog saveDialog = new ContentDialog()
            {
                Title = "Unsaved changes",
                Content = "You have unsaved changes that will be lost if you leave this page.",
                PrimaryButtonText = "Leave this page",
                SecondaryButtonText = "Stay"
            };
            ContentDialogResult result = await saveDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // The user decided to leave the page. Restart
                // the navigation attempt. 
                canNavigateWithUnsavedChanges = true;
                ResetEffects();
                Frame.Navigate(e.SourcePageType, e.Parameter);
            }
        }

        private void ZoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (MainImageScroller != null)
            {
                MainImageScroller.ChangeView(null, null, (float)e.NewValue);
            }
        }

        private void MainImageScroller_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            ZoomSlider.Value = ((ScrollViewer)sender).ZoomFactor;
        }


        private void UpdateZoomState()
        {
            if (MainImageScroller.ZoomFactor == 1)
            {
                FitToScreen();
            }
            else
            {
                ShowActualSize();
            }
        }




        private async void ExportImage()
        {
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            using (CanvasRenderTarget offscreen = new CanvasRenderTarget(
                device, item.ImageProperties.Width, item.ImageProperties.Height, 96))
            {
                using (IRandomAccessStream stream = await item.ImageFile.OpenReadAsync())
                using (CanvasBitmap image = await CanvasBitmap.LoadAsync(offscreen, stream, 96))
                {
                    ImageEffectsBrush.SetSource(image);

                    using (CanvasDrawingSession ds = offscreen.CreateDrawingSession())
                    {
                        ds.Clear(Windows.UI.Colors.Black);

                        var img = ImageEffectsBrush.Image;
                        ds.DrawImage(img);
                    }

                    var fileSavePicker = new FileSavePicker()
                    {
                        SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                        SuggestedSaveFile = item.ImageFile
                    };

                    fileSavePicker.FileTypeChoices.Add("JPEG files", new List<string>() { ".jpg" });

                    var outputFile = await fileSavePicker.PickSaveFileAsync();

                    if (outputFile != null)
                    {
                        using (IRandomAccessStream outStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            await offscreen.SaveAsync(outStream, CanvasBitmapFileFormat.Jpeg);
                        }

                        // Check whether this save is overwriting the original image.
                        // If it is, replace it in the list. Otherwise, insert it as a copy.
                        bool replace = false;
                        if (outputFile.IsEqual(item.ImageFile))
                        {
                            replace = true;
                        }

                        try
                        {
                            await LoadSavedImageAsync(outputFile, replace);
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("0x80070323"))
                            {
                                // The handle with which this oplock was associated has been closed.
                                // The oplock is now broken. (Exception from HRESULT: 0x80070323)
                                // This is a temporary condition, so just try again.
                                await LoadSavedImageAsync(outputFile, replace);
                            }
                        }
                    }
                }
            }
        }

        private async Task LoadSavedImageAsync(StorageFile imageFile, bool replaceImage)
        {
            item.NeedsSaved = false;
            var newItem = await MainPage.LoadImageInfo(imageFile);
            ResetEffects();

            // Get the index of the original image so we can
            // insert the saved image in the same place.
            var index = MainPage.Current.Images.IndexOf(item);

            item = newItem;
            this.Bindings.Update();

            UnloadObject(imgRect);
            FindName("imgRect");
            await LoadBrushAsync();

            // Insert the saved image into the collection.
            if (replaceImage == true)
            {
                MainPage.Current.Images.RemoveAt(index);
                MainPage.Current.Images.Insert(index, item);
            }
            else
            {
                MainPage.Current.Images.Insert(0, item);
            }


            // Replace the persisted image used for connected animation.
            MainPage.Current.UpdatePersistedItem(item);

            // Update BitMapImage used for small picture.
            ImageSource = await item.GetImageSourceAsync();
            SmallImage.Source = ImageSource;
        }

        private void FitToScreen()
        {
            var zoomFactor = (float)Math.Min(MainImageScroller.ActualWidth / item.ImageProperties.Width,
                MainImageScroller.ActualHeight / item.ImageProperties.Height);
            MainImageScroller.ChangeView(null, null, zoomFactor);
        }

        private void ShowActualSize()
        {
            MainImageScroller.ChangeView(null, null, 1);
        }

        private void ResetEffects()
        {
            item.Exposure =
                item.Blur =
                item.Tint =
                item.Temperature =
                item.Contrast = 0;
            item.Saturation = 1;
        }

        private void SmallImage_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            SmallImage.Source = ImageSource;
        }


        /* Edited By Yang Fan */
        private async Task VoiceAssitant(String sayWhat)
        {
            MediaElement mediaElement = new MediaElement();
            var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();

            // 调用Windows语音播放相应内容
            Windows.Media.SpeechSynthesis.SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(sayWhat);
            mediaElement.SetSource(stream, stream.ContentType);
            mediaElement.Play();
        }

        /* Edited By Yang Fan */
        private async void AppBarButtonZoom_Click(object sender, RoutedEventArgs e)
        {
            await VoiceAssitant("图片显示大小调整");
        }
        /* Edited By Yang Fan */
        public DetailPage()
        {
            this.InitializeComponent();
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            var view = ApplicationView.GetForCurrentView();

            //将标题栏的三个键背景设为透明
            view.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            //失去焦点时，将三个键背景设为透明
            view.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            //失去焦点时，将三个键前景色设为白色
            view.TitleBar.ButtonInactiveForegroundColor = Colors.White;
            //InitializeFrostedGlass(RelativePanelOne);
        }

        /* Edited By Yang Fan */
        // 毛玻璃效果，但由于在XAML中调用了亚克力，故未使用
        private void InitializeFrostedGlass(UIElement glassHost)
        {
            Visual hostVisual = ElementCompositionPreview.GetElementVisual(glassHost);
            Compositor compositor = hostVisual.Compositor;
            var backdropBrush = compositor.CreateHostBackdropBrush();
            var glassVisual = compositor.CreateSpriteVisual();
            glassVisual.Brush = backdropBrush;
            ElementCompositionPreview.SetElementChildVisual(glassHost, glassVisual);
            var bindSizeAnimation = compositor.CreateExpressionAnimation("hostVisual.Size");
            bindSizeAnimation.SetReferenceParameter("hostVisual", hostVisual);
            glassVisual.StartAnimation("Size", bindSizeAnimation);
        }

        /* Edited By Yang Fan */
        // 点击Edit按钮时的内容
        private async void ToggleEditState()
        {
            MainSplitView.IsPaneOpen = !MainSplitView.IsPaneOpen;
            await VoiceAssitant("编辑图片");
            FitToScreen();
        }

        /*  Edited by Yang */
        private async Task LoadBrushAsync()
        {
            //FitToScreen();
            using (IRandomAccessStream fileStream = await item.ImageFile.OpenReadAsync())
            {
                Size imageSize = new Size(MainImageScroller.ActualWidth, MainImageScroller.ActualHeight);
                ImageEffectsBrush.LoadImageFromStream(fileStream);
                FitToScreen();
            }
        }

        /* Edited By Yang */
        private async void AppBarButtonCrop_Click(object sender, RoutedEventArgs e)
        {
            // 第一次点击该按钮时裁剪，第二次保存
            // 这肯定不是最好的方法，但时间不太够，我以后想办法解决吧
            if (CropOrSave)
            {
                await VoiceAssitant("裁剪图片");
                CropOrSave = !CropOrSave;
                await ImageCropper.LoadImageFromFile(item.ImageFile);

            }
            else
            {
                await VoiceAssitant("保存图片");
                SaveCroppedImages();
                await Task.Delay(400);
                // 停止 ImageCropper
                await SetNULL();
            }

        }

        /* Edited By Yang */
        private async void SaveCroppedImages()
        {
            //_imageCropper.TrySetCroppedRegion(ImageCropper.CroppedRegion);

            /* 设定保存裁剪后图片的位置，默认为应用目录 */
            StorageFolder appInstalledFolder = Package.Current.InstalledLocation;
            StorageFolder destinationFolder = await appInstalledFolder.GetFolderAsync("Assets\\WorkShop");
            StorageFile croppedPhoto = await item.ImageFile.CopyAsync(destinationFolder, "Cropped_" + item.ImageFile.Name, NameCollisionOption.GenerateUniqueName);

            /* 在给定位置保存被裁剪的图片 */
            var fileStream = await croppedPhoto.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.None);
            await ImageCropper.SaveAsync(fileStream, BitmapFileFormat.Jpeg);
            CropOrSave = !CropOrSave;

            /* 给 fileStream 一些时间用来写入，这里设定为400ms */
            await Task.Delay(400);
            fileStream.Dispose();
            /* 将裁剪后的图片添加进MainPage中 */
            var newItem = await MainPage.LoadImageInfo(croppedPhoto);
            MainPage.Current.Images.Insert(0, newItem);

        }

        // 退出 ImageCropper 界面
        private async Task SetNULL() => ImageCropper.Source = null;

    }
}
