using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Autodesk.Revit.DB.Structure;
using System;
using Autodesk.Revit.UI.Selection;
using System.Linq;

namespace AutoHangerCreation_ButtonCreate
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class PipeDistTest : IExternalCommand
    {
        DisplayUnitType unitType = DisplayUnitType.DUT_MILLIMETERS;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Autodesk.Revit.UI.Selection.ISelectionFilter pipeFilter = new PipeSelectionFilter();

            //點選要一起放置多管吊架的管段
            List<Element> pickElements = new List<Element>();
            Document doc = uidoc.Document;
            //管數量
            int pipeCount = 5;
            for (int i = 0; i < pipeCount; i++)
            {
                Reference pickElements_Refer = uidoc.Selection.PickObject(ObjectType.Element, pipeFilter, $"請選擇欲放置吊架的管段，還剩 {pipeCount - i} 隻管要選擇");
                Element element = doc.GetElement(pickElements_Refer.ElementId);
                pickElements.Add(element);
            }
            List<Element>sortElements= pickElements.OrderBy(x=>sortPIpeByRefer(x)).ToList();
            StringBuilder st = new StringBuilder();
            List<double> pipeDist = new List<double>();

            //取得管跟管之間計算管長
            for(int i = 0; i < (sortElements.Count-1); i++)
            {
                LocationCurve pipeCrv = sortElements[i].Location as LocationCurve;
                Curve crvtoCalculate = pipeCrv.Curve;
                XYZ p1 = crvtoCalculate.Evaluate(0.5, true);

                LocationCurve pipeCrv2 = sortElements[i + 1].Location as LocationCurve;
                Curve crvtoCalculate2 = pipeCrv2.Curve;
                XYZ p2 =new XYZ (crvtoCalculate2.Project(p1).XYZPoint.X, crvtoCalculate2.Project(p1).XYZPoint.Y,p1.Z);
                pipeDist.Add(p1.DistanceTo(p2));

                double newDist = Math.Round(p1.DistanceTo(p2) * 30.48,1);
                st.AppendLine(newDist.ToString());
            }
            

            //創造一個容器，裝取依序點選的管徑
            List<string> pipeDiameters = new List<string>();

            //角度測量測試
            XYZ basePt = new XYZ(0, 1, 0);
            List<double> pipeAngle = new List<double>();
            
            foreach (Element pipe in sortElements)
            {
               string pipeDia = pipe.LookupParameter("直徑").AsValueString();

                //LocationCurve locationCurve = pipe.Location as LocationCurve;
                //Curve calCurve = locationCurve.Curve;
                //XYZ calStr = calCurve.GetEndPoint(0);
                //XYZ calEnd = calCurve.GetEndPoint(1);
                //Line calCurveProject = Line.CreateBound(calStr, new XYZ(calEnd.X, calEnd.Y, calStr.Z));
                //double angleTest = basePt.AngleTo(calCurveProject.Direction) * (180 / Math.PI);

                pipeDiameters.Add(pipeDia);
                st.AppendLine(pipeDia.ToString());
            }

            MessageBox.Show(pipeDiameters.Count.ToString());
            MessageBox.Show("選中的管徑分別為:" + st.ToString());

            //開始放置吊架
            Transaction trans = new Transaction(doc);
            trans.Start("放置多管吊架");

            //創造表格把資訊載入
            Form1 form1 = new Form1(commandData);
            form1.ShowDialog();
            string divideValueString = form1.divideValue.ToString();
            double divideValue_Temp = double.Parse(divideValueString);
            double divideValue_Double = UnitUtils.ConvertToInternalUnits(divideValue_Temp, unitType);


            FamilySymbol multiHangerType = new MultiPipeHanger().FindhangerType(doc);
            LocationCurve locationCurve = sortElements[0].Location as LocationCurve;
            Curve pipeCurve = locationCurve.Curve;

            XYZ pipeStart = pipeCurve.GetEndPoint(0);
            XYZ pipeEnd = pipeCurve.GetEndPoint(1);
            XYZ pipeEndAdjust = new XYZ(pipeEnd.X,pipeEnd.Y,pipeStart.Z);
            Line pipeLineProject = Line.CreateBound(pipeStart, pipeEndAdjust);

            //這裡可能會有一點bug 因為第一條管不一定會是最長的
            double pipeLength = pipeCurve.Length;
            double param1 = pipeCurve.GetEndParameter(0);
            double param2 = pipeCurve.GetEndParameter(1);

            //計算分割的數量
            int step = (int)(pipeLength /divideValue_Double );
            double paramCalc = param1 + ((param2 - param1) * divideValue_Double / pipeLength);

            //創造一個容器裝所有點資料，提供給日後放置管吊架的依據
            IList<Point> pointList = new List<Point>();
            IList<XYZ> locationList = new List<XYZ>();
            XYZ evaluatedPoint = null;
            var degrees = 0.0;

            for (int i =0; i < step; i++)
            {
                paramCalc = param1 + ((param2 - param1) * divideValue_Double * (i + 1) / pipeLength);
                if (pipeCurve.IsInside(paramCalc) == true)
                {
                    double normParam = pipeCurve.ComputeNormalizedParameter(paramCalc);

                    evaluatedPoint = pipeCurve.Evaluate(normParam, true);
                    Point locationPoint = Point.Create(evaluatedPoint);
                    //XYZ evaluatedProject = new XYZ(evaluatedPoint.X, evaluatedPoint.Y, 0);
                    pointList.Add(locationPoint);
                    locationList.Add(evaluatedPoint);
                }
            }

            foreach (XYZ p1 in locationList)
            {
                Element hanger = new MultiPipeHanger().CreateMultiHanger(uidoc.Document, p1, sortElements[0], multiHangerType);
                XYZ p2 = new XYZ(p1.X, p1.Y, p1.Z + 1);
                Line Axis = Line.CreateBound(p1, p2);
                XYZ p3 = new XYZ(0, p1.X, 0); //測量吊架與管段之間的向量差異，取plane中的x向量

                degrees = p3.AngleTo(pipeLineProject.Direction);
                hanger.Location.Rotate(Axis, degrees); //旋轉吊架方法1
                for(int i = 0; i < pipeDiameters.Count; i++)
                {
                    hanger.LookupParameter($"管直徑0{i + 1}").SetValueString(pipeDiameters[i].ToString());
                }
                for(int i = 0; i < pipeDist.Count; i++)
                {
                    hanger.LookupParameter($"管間距0{i + 1}").Set(pipeDist[i]);
                }
                //做最後的調製，以第一個管為依據，將多管吊架對應至管底
                double originOffset = hanger.LookupParameter("偏移").AsDouble();
                double toMove = sortElements[0].LookupParameter("外徑").AsDouble()/2;
                hanger.LookupParameter("偏移").Set(originOffset - toMove);
            }
            string total = pointList.Count.ToString();
            MessageBox.Show("共產生" + total + "個多管吊架");
            trans.Commit();
            return Result.Succeeded;
        }
        class MultiPipeHanger
        {
            //創建多管class-->其中包含三項功能
            //1.找到family
            //2.蒐集各種管徑
            //3.寫入管徑參數
            //依點放置吊點+移動
            public FamilySymbol FindhangerType(Document doc)
            {
                //尋找吊架的族群(FamilySymbol)
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                collector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_PipeAccessory);
                IList<Element> all = collector.WhereElementIsElementType().ToElements();
                FamilySymbol targetFamily = null;

                foreach (Element e in all)
                {
                    FamilySymbol familySymbol = e as FamilySymbol;
                    try
                    {
                        if (familySymbol.Name == "多管吊架_管v9")
                        {
                            targetFamily = familySymbol;
                        }
                    }
                    catch (ArgumentNullException ex) when (targetFamily == null)
                    {
                        MessageBox.Show(ex.ToString());
                        MessageBox.Show("尚未匯入指定的多管吊架 !!!");
                    }
                }
                return targetFamily;
            }
            public FamilyInstance CreateMultiHanger(Document doc, XYZ location, Element element, FamilySymbol targetFamily)
            {
                FamilyInstance instance = null;
                if (targetFamily != null)
                {
                    targetFamily.Activate();
                    MEPCurve pipCrv = element as MEPCurve;
                    Level HangLevel = pipCrv.ReferenceLevel;

                    double movedown = HangLevel.Elevation;//取得該層樓高層
                    instance = doc.Create.NewFamilyInstance(location, targetFamily, HangLevel, StructuralType.NonStructural);
                    double toMove = (instance.LookupParameter("偏移").AsDouble()) - movedown;
                    instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM).Set(toMove);
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
        public double sortPIpeByRefer(Element element)
        {
            //製作一個排序的方法，提供給lambda使用
            XYZ basePt = new XYZ(0, 1, 0);
            LocationCurve locationCurve = element.Location as LocationCurve;
            Curve pipeCrv = locationCurve.Curve;
            XYZ calStr = pipeCrv.GetEndPoint(0);
            XYZ calEnd = pipeCrv.GetEndPoint(1);
            Line calCurveProject = Line.CreateBound(calStr, new XYZ(calEnd.X, calEnd.Y, calStr.Z));
            double angleTest = basePt.AngleTo(calCurveProject.Direction) * (180 / Math.PI);
            double sortRefer = 0.0;

            if (angleTest >= 45 && angleTest <= 135)
            {
                sortRefer = calStr.Y;
            }
            else if (angleTest >= 0 && angleTest < 45)
            {
                sortRefer = calStr.X;
            }
            else if (angleTest >= 145 && angleTest <= 180)
            {
                sortRefer = calStr.X;
            }
            return sortRefer;
        }
    }
}
