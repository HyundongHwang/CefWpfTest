using CefSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MyCefWpfTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.BtnCsharpToJs.Click += BtnCsharpToJs_Click;
            var exeDir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var htmlPath = System.IO.Path.Combine(exeDir, "test.html");
            this.Browser.Address = htmlPath;
            var cbObj = new MyCallback();
            this.Browser.RegisterJsObject("cbObj", cbObj);
        }

        private void BtnCsharpToJs_Click(object sender, RoutedEventArgs e)
        {
            var script = "jsFunc('hello');";
            this.Browser.GetMainFrame().ExecuteJavaScriptAsync(script);
        }
    }


    public class MyCallback
    {
        public void OnCallback(string param)
        {
            MessageBox.Show($"this is c# OnCallback function param : {param}");
        }
    }
}
