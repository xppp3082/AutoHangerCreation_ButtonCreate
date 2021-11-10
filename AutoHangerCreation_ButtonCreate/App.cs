#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;


#endregion

namespace AutoHangerCreation_ButtonCreate
{
    class App : IExternalApplication
    {
        //用來創造按鈕 for 單管
        const string RIBBON_TAB = "Travis Tools";
        const string RIBBON_PANEL = "Ribbon Button Sample2";
        

        public Result OnStartup(UIControlledApplication a)
        {
            // get the ribbon tab
            try
            {
                a.CreateRibbonTab(RIBBON_TAB);
            }
            catch (Exception) { } //tab alreadt exists

            // get or create the panel
            RibbonPanel panel = null;
            List<RibbonPanel> panels = a.GetRibbonPanels(RIBBON_TAB); //在此要確保RIBBON_TAB在這行之前已經被創建
            foreach (RibbonPanel pnl in panels)
            {
                if (pnl.Name == RIBBON_PANEL)
                {
                    panel = pnl;
                    break;
                }
            }

            // couldn't find panel, create it
            if (panel == null)
            {
                panel = a.CreateRibbonPanel(RIBBON_TAB, RIBBON_PANEL);
            }

            // get the image for the button
            System.Drawing.Image img = Properties.Resources.pokemon4;
            ImageSource imgSrc = GetImageSource(img);

            // create the button data
            PushButtonData btnData = new PushButtonData(
                "MyButton",
                "Create Hanger",
                Assembly.GetExecutingAssembly().Location,
                "AutoHangerCreation_ButtonCreate.HangerCreation"//按鈕的全名-->要依照需要參照的command打入
                );
            {
                btnData.ToolTip = "Short description that is shown when you hover over the button";
                btnData.LongDescription = "Longer description shown when you hover over the button for a few seconds";
                btnData.LargeImage = imgSrc;
            };
            //add the button to the ribbon
            PushButton button = panel.AddItem(btnData) as PushButton;
            button.Enabled = true;

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }


        private BitmapSource GetImageSource(Image img)
        {
            //製作一個function專門來處理圖片
            BitmapImage bmp = new BitmapImage();

            using (MemoryStream ms = new MemoryStream())
            {
                img.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                bmp.BeginInit();

                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = null;
                bmp.StreamSource = ms;

                bmp.EndInit();
            }

            return bmp;
        }
    }
}
