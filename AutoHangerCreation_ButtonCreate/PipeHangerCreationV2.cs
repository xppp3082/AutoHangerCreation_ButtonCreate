#region Namespaces
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI.Selection;
using System.Linq;
using System;

#endregion
namespace AutoHangerCreation_ButtonCreate
{
    //創造單管吊架
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class PipeHangerCreationV2 : IExternalCommand
    {
        DisplayUnitType unitType = DisplayUnitType.DUT_MILLIMETERS;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                //限制使用者只能選中管
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Autodesk.Revit.UI.Selection.ISelectionFilter pipeFilter = new PipeSelectionFilter();

                //點選要一起放置單管吊架的管段
                List<Element> pickElements = new List<Element>();
                Document doc = uidoc.Document;
                IList<Reference> pickElements_Refer = uidoc.Selection.PickObjects(ObjectType.Element, pipeFilter, $"請選擇欲放置吊架的管段");

                foreach (Reference reference in pickElements_Refer)
                {
                    Element element = doc.GetElement(reference.ElementId);
                    pickElements.Add(element);
                }

                StringBuilder st = new StringBuilder();

                //set up form and ask for user information -->創造表格並詢問資訊
                PipeHangerUI UserForm = new PipeHangerUI(commandData);
                UserForm.ShowDialog();
                //grab string values from form1 and convert to respectives types
                string divideValueString = UserForm.divideValue.ToString();

                //英制轉公制
                double divideValue_doubleTemp = double.Parse(divideValueString);
                double divideValue_double = UnitUtils.ConvertToInternalUnits(divideValue_doubleTemp, unitType);
                int hangerCount = 0;


                //載入元件檔
                using (Transaction tx = new Transaction(doc))
                {
                    tx.Start("載入檔案測試");
                    foreach (Element element in pickElements)
                    {
                        double pipeDia = element.LookupParameter("直徑").AsDouble();
                        double deciamlDia = UnitUtils.ConvertFromInternalUnits(pipeDia, unitType);
                        Family pipe_Hanger = new pipeHanger().pipeHangerFamily(doc, deciamlDia);
                        FamilySymbol familySymbol = new pipeHanger().findHangerSymbol(doc, pipe_Hanger, element);
                        //MessageBox.Show(deciamlDia.ToString());
                        //MessageBox.Show($"找到的元件名稱為:{pipe_Hanger.Name}");
                        //MessageBox.Show($"找到的元件名稱為:{pipe_Hanger.Name}，對應的大小為{familySymbol.Name}");

                        //找到管的location curve
                        LocationCurve pipeLocationCrv = element.Location as LocationCurve;
                        Curve pipeCurve = pipeLocationCrv.Curve;

                        XYZ pipeStart = pipeCurve.GetEndPoint(0);
                        XYZ pipeEnd = pipeCurve.GetEndPoint(1);

                        XYZ pipeEndAdjust = new XYZ(pipeEnd.X, pipeEnd.Y, pipeStart.Z);
                        Line pipelineProject = Line.CreateBound(pipeStart, pipeEndAdjust);

                        double pipeLength = pipeCurve.Length;

                        double param1 = pipeCurve.GetEndParameter(0);
                        double param2 = pipeCurve.GetEndParameter(1);

                        //計算要分割的數量 (不確定是否有四捨五入)
                        int step = (int)(pipeLength / divideValue_double);
                        double paramCalc = param1 + ((param2 - param1)
                          * divideValue_double / pipeLength);

                        //創造一個容器裝所有點資料(位於線上的)
                        IList<Point> pointList = new List<Point>();
                        IList<XYZ> locationList = new List<XYZ>();
                        XYZ evaluatedPoint = null;
                        var degrees = 0.0;
                        double half_PI = Math.PI / 2;

                        //如果除出來的階數<=1，則在中點創造一個吊架，如果>1，則依照階數分割
                        if (step <= 1)
                        {
                            XYZ centerPt = pipeCurve.Evaluate(0.5, true);
                            XYZ centerPt_up = new XYZ(centerPt.X, centerPt.Y, centerPt.Z+1);
                            XYZ rotateBase = new XYZ(0, centerPt.X, 0);
                            Line Axis = Line.CreateBound(centerPt, centerPt_up);
                            Element hanger = new pipeHanger().CreateHanger(uidoc.Document,centerPt, element, familySymbol);
                            degrees = rotateBase.AngleTo(pipelineProject.Direction);
                            hanger.Location.Rotate(Axis, half_PI-degrees);
                        }
                        else if (step > 2)
                        {
                            for (int i = 0; i < step; i++)
                            {
                                paramCalc = param1 + ((param2 - param1) * divideValue_double * (i + 1) / pipeLength);
                                if (pipeCurve.IsInside(paramCalc) == true)
                                {
                                    double normParam = pipeCurve.ComputeNormalizedParameter(paramCalc);

                                    evaluatedPoint = pipeCurve.Evaluate(normParam, true);
                                    Point locationPoint = Point.Create(evaluatedPoint);
                                    pointList.Add(locationPoint);
                                    locationList.Add(evaluatedPoint);
                                }
                            }

                            foreach (XYZ p1 in locationList)
                            {
                                Element hanger = new pipeHanger().CreateHanger(uidoc.Document, p1, element, familySymbol);
                                XYZ p2 = new XYZ(p1.X, p1.Y, p1.Z + 1);
                                Line Axis = Line.CreateBound(p1, p2);
                                XYZ p3 = new XYZ(0, p1.X, 0); //測量吊架與管段之間的向量差異，取plane中的x向量
                                degrees = p3.AngleTo(pipelineProject.Direction);

                                //旋轉後校正位置
                                hanger.Location.Rotate(Axis, half_PI - degrees);
                            }
                        }
                    }
                    tx.Commit();
                }
            }
            catch
            {
                return Result.Failed;
            }
            return Result.Succeeded;
        }



        class pipeHanger
        {
            //1.自動匯入元件檔的功能
            //2.自動判斷大小後選擇欲放置的吊架
            string smallHanger = "M_雙層管束_管附件";
            string largeHanger = "M_單管角鐵雙面孔吊架_管附件";

            public Family pipeHangerFamily(Document doc, double pipeDiameter)
            {
                double decimalDiameter = pipeDiameter;
                string targetFamName = "";
                Family hangerType = null;
                ElementFilter CategoryFilter = new ElementCategoryFilter(BuiltInCategory.OST_PipeAccessory);
                ElementFilter FamilyFilter = new ElementClassFilter(typeof(Family));
                LogicalAndFilter andFilter = new LogicalAndFilter(CategoryFilter, FamilyFilter);
                FilteredElementCollector hangerFamily = new FilteredElementCollector(doc);
                hangerFamily.WherePasses(FamilyFilter).ToElements();//這地方有點怪，無法使用andFilter
                bool symbolFound = false;
                if (decimalDiameter <= 105)
                {
                    targetFamName = smallHanger;
                }
                else if (decimalDiameter >= 120)
                {
                    targetFamName = largeHanger;
                }
                foreach (Family family in hangerFamily)
                {
                    if (family.Name == targetFamName)
                    {
                        symbolFound = true;
                        hangerType = family;
                        break;
                    }
                }
                //如果沒有找到，則自己加載
                //如果管徑較小，載入雙層管束
                //如果管徑較大，載入單管角鐵雙面孔吊架
                if (!symbolFound && decimalDiameter <= 105)
                {
                    string filePath = @"D:\Dropbox (CHC Group)\BIM\05 Common 共通\Revit元件資料庫\機電元件_202110\00 通用(M)\M_雙層管束_管附件.rfa";
                    Family targetFamily;
                    bool loadSuccess = doc.LoadFamily(filePath, out targetFamily);
                    if (loadSuccess)
                    {
                        hangerType = targetFamily;
                    }
                    else
                    {
                        MessageBox.Show("元件匯入失敗");
                    }
                }
                else if (!symbolFound && decimalDiameter >= 120)
                {
                    string filePath = @"D:\Dropbox (CHC Group)\BIM\05 Common 共通\Revit元件資料庫\機電元件_202110\00 通用(M)\M_單管角鐵雙面孔吊架_管附件.rfa";
                    Family targetFamily;
                    bool loadSuccess = doc.LoadFamily(filePath, out targetFamily);
                    if (loadSuccess)
                    {
                        hangerType = targetFamily;
                    }
                    else
                    {
                        MessageBox.Show("元件匯入失敗");
                    }
                }
                return hangerType;
            }
            public FamilySymbol findHangerSymbol(Document doc, Family hangerFamily, Element element)
            {
                FamilySymbol targetSymbol = null;//用來找目標familySymbol
                string pipeDia_temp = element.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsValueString();
                if (hangerFamily != null && hangerFamily.Name == smallHanger)
                {
                    foreach (ElementId hangId in hangerFamily.GetFamilySymbolIds())
                    {
                        FamilySymbol tempSymbol = doc.GetElement(hangId) as FamilySymbol;
                        if (pipeDia_temp == "40 mm")
                        {
                            if (tempSymbol.Name == "DN50(2\")x R3/8\"")
                            {
                                targetSymbol = tempSymbol;
                            }
                        }
                        else if (pipeDia_temp == "50 mm")
                        {
                            if (tempSymbol.Name == "DN50(2\")x R3/8\"")
                            {
                                targetSymbol = tempSymbol;
                            }
                        }
                        else if (pipeDia_temp == "65 mm")
                        {
                            if (tempSymbol.Name == "DN80(3\")x R1/2\"")
                            {
                                targetSymbol = tempSymbol;
                            }
                        }
                        else if (pipeDia_temp == "80 mm")
                        {
                            if (tempSymbol.Name == "DN80(3\")x R1/2\"")
                            {
                                targetSymbol = tempSymbol;
                            }
                        }
                        else if (pipeDia_temp == "100 mm")
                        {
                            if (tempSymbol.Name == "DN100(4\")x R1/2\"")
                            {
                                targetSymbol = tempSymbol;
                            }
                        }
                        else
                        {
                            if (tempSymbol.Name == "DN50(2\")x R3/8\"")
                            {
                                targetSymbol = tempSymbol;
                            }
                        }
                    }
                }
                else if (hangerFamily != null && hangerFamily.Name == largeHanger)
                {
                    foreach (ElementId hangId in hangerFamily.GetFamilySymbolIds())
                    {
                        FamilySymbol tempSymbol = doc.GetElement(hangId) as FamilySymbol;
                        if (pipeDia_temp == "125 mm")
                        {

                            if (tempSymbol.Name == "DN125(5\")-U⅜\"-L2\"x4mm-H10x37x53mm-R⅜\"")
                            {
                                targetSymbol = tempSymbol;
                            }
                        }
                        else if (pipeDia_temp == "150 mm")
                        {
                            if (tempSymbol.Name == "DN150(6\")-U⅜\"-L2\"x4mm-H10x37x53mm-R⅜\"")
                            {
                                targetSymbol = tempSymbol;
                            }
                        }
                        else if (pipeDia_temp == "200 mm")
                        {
                            if (tempSymbol.Name == "DN200(8\")-U⅜\"-L2½\"x6mm-H13x34x55mm-R½\"")
                            {
                                targetSymbol = tempSymbol;
                            }
                        }
                        else
                        {
                            MessageBox.Show("選取的管過大，無法使用單管吊架!!");
                        }
                    }
                }
                return targetSymbol;
            }
            public FamilyInstance CreateHanger(Document doc, XYZ location, Element element, FamilySymbol targetFamily)
            {
                //創造吊架
                FamilyInstance instance = null;
                if (targetFamily != null)
                {
                    targetFamily.Activate();

                    if (null != targetFamily)
                    {
                        MEPCurve pipCrv = element as MEPCurve; //選取管件，一定可以轉型MEPCurve
                        Level HangLevel = pipCrv.ReferenceLevel;

                        double moveDown = HangLevel.Elevation; //取得該層樓高層

                        instance = doc.Create.NewFamilyInstance(location, targetFamily, HangLevel, StructuralType.NonStructural); //一定要宣告structural 類型? yes
                        double toMove = (instance.LookupParameter("偏移").AsDouble()) - moveDown;
                        instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM).Set(toMove); //因為給予instance reference level後，實體會基於level的高度上進行偏移，因此需要將偏移量再扣掉一次，非常重要 !!!!。
                    }
                }
                return instance;
            }
        }



        public class PipeSelectionFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                if (element.Category.Name == "管")
                {
                    return true;
                }
                return false;
            }

            public bool AllowReference(Reference refer, XYZ point)
            {
                return false;
            }
        }
    }
}