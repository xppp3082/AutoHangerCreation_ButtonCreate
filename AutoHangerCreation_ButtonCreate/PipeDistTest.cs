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
            int pipeCount = 7;
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
                //st.AppendLine(angleTest.ToString());
                st.AppendLine(pipeDia.ToString());
            }

            MessageBox.Show(pipeDiameters.Count.ToString());
            //MessageBox.Show("產生的夾角角度分別為:" + st.ToString());
            MessageBox.Show("選中的管徑分別為:" + st.ToString());



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
                        if (familySymbol.Name == "多管吊架_管v8")
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
