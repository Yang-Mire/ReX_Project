using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage.Streams;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Rex
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class BlankCanvas : Page
    {
        public BlankCanvas()
        {
            this.InitializeComponent();

            // Initialize the InkCanvas
            inkCanvas.InkPresenter.InputDeviceTypes =
                Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                Windows.UI.Core.CoreInputDeviceTypes.Pen;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            HelperFunctions.UpdateCanvasSize(RootGrid, outputGrid, inkCanvas);
        }

        private void Flyout0_Click(object sender, RoutedEventArgs e)
        {
            Content = new MainPage();
        }
    }
    static class HelperFunctions
    {
        public static void UpdateCanvasSize(FrameworkElement root, FrameworkElement output, FrameworkElement inkCanvas)
        {
            output.Width = root.ActualWidth;
            output.Height = root.ActualHeight;
            inkCanvas.Width = root.ActualWidth;
            inkCanvas.Height = root.ActualHeight;
        }
    }


}
