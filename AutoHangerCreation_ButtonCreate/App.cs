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
        const string RIBBON_TAB = "【CEC MEP】";
        const string RIBBON_PANEL = "管吊架";
        

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

            // get the image for the button(放置單管吊架)
            System.Drawing.Image image_Single = Properties.Resources.單管_多管V2__轉換__02__96dpi;
            ImageSource imgSrc = GetImageSource(image_Single);

            //get the image for the button(放置多管吊架)
            System.Drawing.Image image_Multi = Properties.Resources.單管_多管V2__轉換__01__96dpi;
            ImageSource imgSrc2 = GetImageSource(image_Multi);

            //get the image for the button(調整吊架螺桿)
            System.Drawing.Image image_Adjust = Properties.Resources.吊桿長度調整_96DPI_01;
            ImageSource imgSrc3 = GetImageSource(image_Adjust);

            //第三種做Button按鈕圖片的方法，參考官網對於button製作的描述
            //Uri uriImage = new Uri(@"D:\Dropbox (CHC Group)\工作人生\組內專案\02.Revit API開發\01.自動放置吊架\ICON\單管&多管V2 [轉換]-01).96dpi.png");
            //BitmapImage largeImage = new BitmapImage(uriImage);

            // create the button data
            PushButtonData btnData = new PushButtonData(
                "MyButton_Single",
                "創建\n   單管吊架   ",
                Assembly.GetExecutingAssembly().Location,
                "AutoHangerCreation_ButtonCreate.HangerCreation"//按鈕的全名-->要依照需要參照的command打入
                );
            {
                btnData.ToolTip = "點選管段創建單管吊架";
                btnData.LongDescription = "點選需要創建的管段，生成單管吊架";
                btnData.LargeImage = imgSrc;
            };

            PushButtonData btnData2 = new PushButtonData("MyButton_Multi", "創建\n   多管吊架   ", Assembly.GetExecutingAssembly().Location, "AutoHangerCreation_ButtonCreate.PipeDistTest");
            {
                btnData2.ToolTip = "點選管段創建多管吊架";
                btnData2.LongDescription = "點選需要創建的管段，生成多管吊架，單次最多選擇八支管";
                btnData2.LargeImage = imgSrc2;
            }

            PushButtonData btnData3 = new PushButtonData("MyButton_Multi", "調整\n   螺桿長度   ", Assembly.GetExecutingAssembly().Location, "AutoHangerCreation_ButtonCreate.HangerToFloorDist");
            {
                btnData3.ToolTip = "點選需要調整的吊架";
                btnData3.LongDescription = "點選需要調整的吊架，調整螺桿長度連接至外參建築樓板";
                btnData3.LargeImage = imgSrc3;
            }

            //add the button to the ribbon
            PushButton button = panel.AddItem(btnData) as PushButton;
            PushButton button2 = panel.AddItem(btnData2) as PushButton;
            PushButton button3 = panel.AddItem(btnData3) as PushButton;

            //做完的button記得要Enable
            button.Enabled = true;
            button2.Enabled = true;
            button3.Enabled = true;
            panel.AddSeparator();

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
