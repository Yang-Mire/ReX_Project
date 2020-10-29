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

using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel.Core;
using Windows.UI.ViewManagement;

namespace Rex
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public static MainPage Current;
        private ImageFileInfo persistedItem;

        public ObservableCollection<ImageFileInfo> Images { get; } = new ObservableCollection<ImageFileInfo>();
        public event PropertyChangedEventHandler PropertyChanged;


        public MainPage()
        {
            this.InitializeComponent();
            Current = this;

            // Edited By Yang
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

        /*
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
        */




        // If the image is edited and saved in the details page, this method gets called
        // so that the back navigation connected animation uses the correct image.
        public void UpdatePersistedItem(ImageFileInfo item)
        {
            persistedItem = item;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                AppViewBackButtonVisibility.Collapsed;

            if (Images.Count == 0)
            {
                await GetItemsAsync();
            }

            base.OnNavigatedTo(e);
        }

        // Called by the Loaded event of the ImageGridView.
        private async void StartConnectedAnimationForBackNavigation()
        {
            // Run the connected animation for navigation back to the main page from the detail page.
            if (persistedItem != null)
            {
                ImageGridView.ScrollIntoView(persistedItem);
                ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("backAnimation");
                if (animation != null)
                {
                    await ImageGridView.TryStartConnectedAnimationAsync(animation, persistedItem, "ItemImage");
                }
            }

        }

        private void ImageGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Prepare the connected animation for navigation to the detail page.
            persistedItem = e.ClickedItem as ImageFileInfo;
            ImageGridView.PrepareConnectedAnimation("itemAnimation", e.ClickedItem, "ItemImage");

            this.Frame.Navigate(typeof(DetailPage), e.ClickedItem);
        }

        private async Task GetItemsAsync()
        {
            QueryOptions options = new QueryOptions();
            options.FolderDepth = FolderDepth.Deep;
            options.FileTypeFilter.Add(".jpg");
            options.FileTypeFilter.Add(".png");
            options.FileTypeFilter.Add(".bmp");

            // Get the Pictures library. (Requires 'Pictures Library' capability.)
            //Windows.Storage.StorageFolder picturesFolder = Windows.Storage.KnownFolders.PicturesLibrary;
            // OR
            //Get the Sample pictures.
            StorageFolder appInstalledFolder = Package.Current.InstalledLocation;
            StorageFolder picturesFolder = await appInstalledFolder.GetFolderAsync("Assets\\WorkShop");

            var result = picturesFolder.CreateFileQueryWithOptions(options);

            IReadOnlyList<StorageFile> imageFiles = await result.GetFilesAsync();
            bool unsupportedFilesFound = false;
            foreach (StorageFile file in imageFiles)
            {
                // Only files on the local computer are supported. 
                // Files on OneDrive or a network location are excluded.
                if (file.Provider.Id == "computer")
                {
                    Images.Add(await LoadImageInfo(file));
                }
                else
                {
                    unsupportedFilesFound = true;
                }
            }

            if (unsupportedFilesFound == true)
            {
                ContentDialog unsupportedFilesDialog = new ContentDialog
                {
                    Title = "Unsupported images found",
                    Content = "This sample app only supports images stored locally on the computer. We found files in your library that are stored in OneDrive or another network location. We didn't load those images.",
                    CloseButtonText = "Ok"
                };

                ContentDialogResult resultNotUsed = await unsupportedFilesDialog.ShowAsync();
            }
        }


        public double ItemSize
        {
            get => _itemSize;
            set
            {
                if (_itemSize != value)
                {
                    _itemSize = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemSize)));
                }
            }
        }
        private double _itemSize;

        private void DetermineItemSize()
        {
            if (FitScreenToggle != null
                && FitScreenToggle.IsOn == true
                && ImageGridView != null
                && ZoomSlider != null)
            {
                // The 'margins' value represents the total of the margins around the
                // image in the grid item. 8 from the ItemTemplate root grid + 8 from
                // the ItemContainerStyle * (Right + Left). If those values change,
                // this value needs to be updated to match.
                int margins = (int)this.Resources["LargeItemMarginValue"] * 4;
                double gridWidth = ImageGridView.ActualWidth - (int)this.Resources["DefaultWindowSidePaddingValue"];
                double ItemWidth = ZoomSlider.Value + margins;
                // We need at least 1 column.
                int columns = (int)Math.Max(gridWidth / ItemWidth, 1);

                // Adjust the available grid width to account for margins around each item.
                double adjustedGridWidth = gridWidth - (columns * margins);

                ItemSize = (adjustedGridWidth / columns);
            }
            else
            {
                ItemSize = ZoomSlider.Value;
            }
        }

        private void ImageGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                var templateRoot = args.ItemContainer.ContentTemplateRoot as Grid;
                var image = (Image)templateRoot.FindName("ItemImage");

                image.Source = null;
            }

            if (args.Phase == 0)
            {
                args.RegisterUpdateCallback(ShowImage);
                args.Handled = true;
            }
        }

        private async void ShowImage(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Phase == 1)
            {
                // It's phase 1, so show this item's image.
                var templateRoot = args.ItemContainer.ContentTemplateRoot as Grid;
                var image = (Image)templateRoot.FindName("ItemImage");
                image.Opacity = 100;

                var item = args.Item as ImageFileInfo;

                try
                {
                    image.Source = await item.GetImageThumbnailAsync();
                }
                catch (Exception)
                {
                    // File could be corrupt, or it might have an image file
                    // extension, but not really be an image file.
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.UriSource = new Uri(image.BaseUri, "Assets/StoreLogo.png");
                    image.Source = bitmapImage;
                }
            }
        }



        /*  Edited by Yang */
        private async Task VoiceAssitant(String sayWhat)
        {
            MediaElement mediaElement = new MediaElement();
            var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();

            // 调用Windows语音播放相应内容
            Windows.Media.SpeechSynthesis.SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(sayWhat);
            mediaElement.SetSource(stream, stream.ContentType);
            mediaElement.Play();
        }


        /*  Edited by Yang */
        public async static Task<ImageFileInfo> LoadImageInfo(StorageFile file)
        {
            var properties = await file.Properties.GetImagePropertiesAsync();
            ImageFileInfo info = new ImageFileInfo(
                properties, file,
                file.DisplayName, file.DisplayType);

            /* 防止电脑速度跟不上图片缩略图的读取速度，100ms已经够用 */
            /* 性能好的电脑可以少一点 */
            await Task.Delay(100);

            return info;
        }

        /* Edited By Yang Fan */
        private async void AppBarButton2_Click(object sender, RoutedEventArgs e)
        {
            await VoiceAssitant("设置磁贴大小");
        }

        /* Edited By Yang Fan */
        private async void AppBarButton1_Click(object sender, RoutedEventArgs e)
        {
            await VoiceAssitant("添加文件");
        }


        /* Edited By Yang Fan */
        private async void Flyout1_Click(object sender, RoutedEventArgs e)
        {
            await VoiceAssitant("请选择图片");

            FileOpenPicker openFile = new FileOpenPicker();
            openFile.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openFile.ViewMode = PickerViewMode.List;
            openFile.FileTypeFilter.Add(".jpg");
            openFile.FileTypeFilter.Add(".png");
            openFile.FileTypeFilter.Add(".bmp");

            // 选取单个文件
            StorageFile file = await openFile.PickSingleFileAsync();
            if (file != null)
            {

                await VoiceAssitant("刚才你打开的图片是 " + file.Name);
                StorageFolder appInstalledFolder = Package.Current.InstalledLocation;
                StorageFolder destinationFolder = await appInstalledFolder.GetFolderAsync("Assets\\WorkShop");
                file = await file.CopyAsync(destinationFolder, file.Name, NameCollisionOption.GenerateUniqueName);

                //此处略有问题，亟待解决
                Images.Insert(0, await LoadImageInfo(file));
                //Images.Clear();

                //((global::Windows.UI.Xaml.Controls.GridView)this.ImageGridView).ContainerContentChanging += this.ImageGridView_ContainerContentChanging;

                //textBlockFileName.Text = "The picture you have opened is " + file.Name;
            }
            else
            {
                await VoiceAssitant("操作已取消");
                //textBlockFileName.Text = "The operation has been manually cancelled";
            }
        }

        /* Edited By Yang Fan */
        private async void Flyout2_Click(object sender, RoutedEventArgs e)
        {
            await VoiceAssitant("打开相机");

            // 调用相机UI
            CameraCaptureUI captureUI = new CameraCaptureUI();
            captureUI.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
            captureUI.PhotoSettings.AllowCropping = false;

            StorageFile photo = await captureUI.CaptureFileAsync(CameraCaptureUIMode.Photo);

            if (photo == null)
            {
                await VoiceAssitant("操作已取消");

                // 取消拍照
                return;
            }

            // 设定照片保存目录
            StorageFolder appInstalledFolder = Package.Current.InstalledLocation;
            StorageFolder destinationFolder = await appInstalledFolder.GetFolderAsync("Assets\\WorkShop");

            StorageFile newfile = await photo.CopyAsync(destinationFolder, "ProfilePhoto.jpg", NameCollisionOption.GenerateUniqueName);
            await photo.DeleteAsync();

            // 将新照片插入第一位
            Images.Insert(0, await LoadImageInfo(newfile));
            await VoiceAssitant("已保存照片" + newfile.Name);
        }

        /* Edited By Yang Fan */
        private async void DeleteSelectedImage(object sender, RoutedEventArgs e)
        {

            var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
            if (ImageGridView.SelectedItem != null)
            {
                StorageFile file = (ImageGridView.SelectedItem as ImageFileInfo).ImageFile;
                await VoiceAssitant("删除文件" + file.Name);
                Images.Remove(ImageGridView.SelectedItem as ImageFileInfo);
                await file.DeleteAsync();
            }
            else
            {
                await VoiceAssitant("未选中文件");
            }
        }

        private void ImageGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        /* Edited By Yang Fan */
        private void InitializeFrostedGlass(UIElement glassHost)
        {
            Visual hostVisual = ElementCompositionPreview.GetElementVisual(glassHost);
            Compositor compositor = hostVisual.Compositor;

            // 创造毛玻璃效果
            var glassEffect = new GaussianBlurEffect
            {
                BlurAmount = 1e-200f,
                BorderMode = EffectBorderMode.Hard,
                Source = new ArithmeticCompositeEffect
                {
                    MultiplyAmount = 0,
                    Source1Amount = 0.5f,
                    Source2Amount = 0.5f,
                    Source1 = new CompositionEffectSourceParameter("backdropBrush"),
                    Source2 = new ColorSourceEffect
                    {
                        Color = Color.FromArgb(255, 255, 255, 255)
                    }
                }
            };

            //  创建效果实例
            var effectFactory = compositor.CreateEffectFactory(glassEffect);
            var backdropBrush = compositor.CreateHostBackdropBrush();
            var effectBrush = effectFactory.CreateBrush();

            effectBrush.SetSourceParameter("backdropBrush", backdropBrush);

            // 创建一个虚拟容器存放毛玻璃效果
            var glassVisual = compositor.CreateSpriteVisual();
            glassVisual.Brush = effectBrush;

            //在visual tree中添加效果
            ElementCompositionPreview.SetElementChildVisual(glassHost, glassVisual);

            var bindSizeAnimation = compositor.CreateExpressionAnimation("hostVisual.Size");
            bindSizeAnimation.SetReferenceParameter("hostVisual", hostVisual);

            glassVisual.StartAnimation("Size", bindSizeAnimation);
        }

        private void Flyout0_Click(object sender, RoutedEventArgs e)
        {
            Content = new BlankCanvas();
        }
    }
}
