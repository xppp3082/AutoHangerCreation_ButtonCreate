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
    public class PipeHangerCreationV3 : IExternalCommand
    {
#if RELEASE2019
        public static DisplayUnitType unitType = DisplayUnitType.DUT_CENTIMETERS;
#else
        public static ForgeTypeId unitType = UnitTypeId.Centimeters;
#endif
        //DisplayUnitType unitType = DisplayUnitType.DUT_CENTIMETERS;
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

                string setupFamily = PIpeHangerSetting.Default.FamilySelected;
                string divideValueString = PIpeHangerSetting.Default.DivideValueSelected;
                if (PIpeHangerSetting.Default.DivideValueSelected == null || PIpeHangerSetting.Default.FamilySelected == null)
                {
                    message = "單管吊架設定未完成!!";
                    return Result.Failed;
                }

                //文字轉數字，英制轉公制
                double divideValue_doubleTemp = double.Parse(divideValueString);
                double divideValue_double = UnitUtils.ConvertToInternalUnits(divideValue_doubleTemp, unitType);
                if (divideValue_double == 0)
                {
                    message = "吊架間距不可為0!!";
                    return Result.Failed;
                }
                //int hangerCount = 0;
                //放置吊架
                using (Transaction tx = new Transaction(doc))
                {
                    tx.Start("放置單管吊架");
                    foreach (Element element in pickElements)
                    {
                        //先設定一個用來存放目標參數的Para
                        Parameter targetPara = null;
                        switch (element.Category.Name)
                        {
                            case "管":
                                targetPara = element.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                                break;
                            case "電管":
                                targetPara = element.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
                                break;
                            case "風管":
                                targetPara = element.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                                break;
                        }

                        if (targetPara == null)
                        {
                            MessageBox.Show("目前還暫不適用方形管件，請待後續更新");
                            continue;
                        }
                        double pipeDia = targetPara.AsDouble();
                        FamilySymbol targetSymbol = new pipeHanger().getFamilySymbol(doc, pipeDia);

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
                            XYZ centerPt_up = new XYZ(centerPt.X, centerPt.Y, centerPt.Z + 1);
                            XYZ rotateBase = new XYZ(0, centerPt.X, 0);
                            Line Axis = Line.CreateBound(centerPt, centerPt_up);
                            Element hanger = new pipeHanger().CreateHanger(uidoc.Document, centerPt, element, targetSymbol);
                            degrees = rotateBase.AngleTo(pipelineProject.Direction);
                            double a = degrees * 180 / (Math.PI);
                            double finalRotate = Math.Abs(half_PI - degrees);
                            if (a > 135 || a < 45)
                            {
                                finalRotate = -finalRotate;
                            }
                            //旋轉後校正位置
                            hanger.Location.Rotate(Axis, finalRotate);
                        }
                        else if (step >= 2)
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
                                Element hanger = new pipeHanger().CreateHanger(uidoc.Document, p1, element, targetSymbol);
                                XYZ p2 = new XYZ(p1.X, p1.Y, p1.Z + 1);
                                Line Axis = Line.CreateBound(p1, p2);
                                XYZ p3 = new XYZ(0, p1.X, 0); //測量吊架與管段之間的向量差異，取plane中的x向量
                                degrees = p3.AngleTo(pipelineProject.Direction);
                                double a = degrees * 180 / (Math.PI);
                                double finalRotate = Math.Abs(half_PI - degrees);
                                if (a > 135 || a < 45)
                                {
                                    finalRotate = -finalRotate;
                                }
                                //旋轉後校正位置
                                hanger.Location.Rotate(Axis, finalRotate);
                            }
                        }
                    }
                    tx.Commit();
                }
            }
            catch
            {
                //MessageBox.Show("執行失敗");
                return Result.Failed;
            }
            return Result.Succeeded;
        }



        class pipeHanger
        {
            //1.自動匯入元件檔的功能
            //2.自動判斷大小後選擇欲放置的吊架
            //public Family pipeHangerFamily(Document doc, double pipeDiameter)

            public FamilySymbol getFamilySymbol(Document doc, double pipeDiameter)
            {
                Family tagerFamily = null;
                string targetFamilyName = PIpeHangerSetting.Default.FamilySelected;
                ElementFilter FamilyFilter = new ElementClassFilter(typeof(Family));
                FilteredElementCollector hangerCollector = new FilteredElementCollector(doc);
                hangerCollector.WherePasses(FamilyFilter).ToElements();
                foreach (Family family in hangerCollector)
                {
                    if (family.Name == targetFamilyName)
                    {
                        tagerFamily = family;
                    }
                }

                //以管徑判斷，取得targetFamily下的管徑
                FamilySymbol targetSymbol = null;
                if (tagerFamily != null)
                {
                    foreach (ElementId hangId in tagerFamily.GetFamilySymbolIds())
                    {
                        FamilySymbol tempSymbol = doc.GetElement(hangId) as FamilySymbol;
                        double hangerDiameter = tempSymbol.LookupParameter("標稱直徑").AsDouble(); //利用標稱直徑的參數作為判斷依據
                        if (hangerDiameter == pipeDiameter)
                        {
                            targetSymbol = tempSymbol;
                        }
                    }
                    if (targetSymbol == null)
                    {
                        MessageBox.Show("預設的吊架沒有和管匹配的類型，請重新設定!!");
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
                        double moveDown = HangLevel.ProjectElevation; //取得該層樓高層
                        instance = doc.Create.NewFamilyInstance(location, targetFamily, HangLevel, StructuralType.NonStructural); //一定要宣告structural 類型? yes
                        double toMove2 = location.Z - moveDown;
#if RELEASE2019
 instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM).Set(toMove2); //因為給予instance reference level後，實體會基於level的高度上進行偏移，因此需要將偏移量再扣掉一次，非常重要 !!!!。
#else
                        instance.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM).Set(toMove2); //因為給予instance reference level後，實體會基於level的高度上進行偏移，因此需要將偏移量再扣掉一次，非常重要 !!!!。
#endif
                    }
                }
                return instance;
            }
        }
        public class PipeSelectionFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                if (element.Category.Name == "管" || element.Category.Name == "電管" || element.Category.Name == "風管")
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
